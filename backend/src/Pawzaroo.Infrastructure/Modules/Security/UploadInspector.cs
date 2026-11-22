using System.Buffers.Binary;
using System.Text;

namespace Pawzaroo.Infrastructure.Modules.Security;

public record SniffResult(string? Mime, string? Extension);
public record ImageDimensions(int Width, int Height);

/// <summary>Pure byte-level helpers: MIME sniffing, polyglot detection, header dimensions, filename sanitising.</summary>
public static class UploadInspector
{
    public const long MaxImagePixels = 50_000_000;   // ~50 MP; blocks decompression bombs
    public const int MaxImageSide = 16_384;
    private const int PolyglotScanBytes = 1024;

    public static readonly IReadOnlyDictionary<string, long> MaxBytesByMime = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = 10L * 1024 * 1024,
        ["image/png"] = 10L * 1024 * 1024,
        ["image/webp"] = 10L * 1024 * 1024,
        ["image/gif"] = 10L * 1024 * 1024,
        ["application/pdf"] = 20L * 1024 * 1024,
        ["video/mp4"] = 200L * 1024 * 1024,
        ["video/webm"] = 200L * 1024 * 1024,
    };

    private static readonly byte[] PngSig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static SniffResult Sniff(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return new("image/jpeg", ".jpg");
        if (h.Length >= 8 && h[..8].SequenceEqual(PngSig)) return new("image/png", ".png");
        if (h.Length >= 6 && (h[..6].SequenceEqual("GIF87a"u8) || h[..6].SequenceEqual("GIF89a"u8))) return new("image/gif", ".gif");
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h.Slice(8, 4).SequenceEqual("WEBP"u8)) return new("image/webp", ".webp");
        if (h.Length >= 5 && h[..5].SequenceEqual("%PDF-"u8)) return new("application/pdf", ".pdf");
        if (h.Length >= 12 && h.Slice(4, 4).SequenceEqual("ftyp"u8)) return new("video/mp4", ".mp4");
        if (h.Length >= 4 && h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3) return new("video/webm", ".webm");
        return new(null, null);
    }

    /// <summary>Returns a reason when the content looks like an executable/script disguised as media; otherwise null.</summary>
    public static string? DetectPolyglot(ReadOnlySpan<byte> data, string mime)
    {
        var head = data[..Math.Min(data.Length, PolyglotScanBytes)];
        if (head.Length >= 2 && head[0] == 0x4D && head[1] == 0x5A) return "Executable (PE) header detected.";
        if (head.Length >= 4 && head[0] == 0x7F && head[1] == 0x45 && head[2] == 0x4C && head[3] == 0x46) return "Executable (ELF) header detected.";
        if (head.Length >= 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04) return "Archive header detected.";

        var text = Encoding.Latin1.GetString(head).ToLowerInvariant();
        // NOTE: markers must be long enough to be improbable in compressed media. A 2-byte marker such as "<%"
        // (ASP) matches ~1.6% of random 1 KB headers and would reject legitimate photos, so it is deliberately absent.
        foreach (var marker in new[] { "<script", "<?php", "<%@", "<%=", "#!/bin/", "<html", "<iframe", "javascript:", "this program cannot be run" })
            if (text.Contains(marker, StringComparison.Ordinal)) return $"Script/executable marker '{marker}' detected.";

        if (mime.StartsWith("image/", StringComparison.Ordinal) && HasTrailingZip(data)) return "Trailing archive detected.";
        return null;
    }

    private static bool HasTrailingZip(ReadOnlySpan<byte> data)
    {
        var start = Math.Max(4, data.Length - 65_536);
        if (start >= data.Length) return false;
        var tail = data[start..];
        ReadOnlySpan<byte> lfh = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        ReadOnlySpan<byte> eocd = new byte[] { 0x50, 0x4B, 0x05, 0x06 };
        return tail.IndexOf(lfh) >= 0 || tail.IndexOf(eocd) >= 0;
    }

    /// <summary>Reads width/height from PNG/GIF/JPEG/WebP headers; null if not determinable.</summary>
    public static ImageDimensions? ReadDimensions(ReadOnlySpan<byte> d, string mime)
    {
        switch (mime)
        {
            case "image/png":
                if (d.Length < 24) return null;
                return new((int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(d.Slice(16, 4)), int.MaxValue),
                           (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(d.Slice(20, 4)), int.MaxValue));
            case "image/gif":
                if (d.Length < 10) return null;
                return new(BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(6, 2)), BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(8, 2)));
            case "image/jpeg":
            {
                int i = 2;
                while (i + 9 < d.Length)
                {
                    if (d[i] != 0xFF) { i++; continue; }
                    var m = d[i + 1];
                    if (m == 0xFF) { i++; continue; }
                    if (m == 0xD8 || (m >= 0xD0 && m <= 0xD7) || m == 0x01 || m == 0x00) { i += 2; continue; }
                    int len = BinaryPrimitives.ReadUInt16BigEndian(d.Slice(i + 2, 2));
                    if (len < 2) return null;
                    if (m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC)
                        return new(BinaryPrimitives.ReadUInt16BigEndian(d.Slice(i + 7, 2)), BinaryPrimitives.ReadUInt16BigEndian(d.Slice(i + 5, 2)));
                    i += 2 + len;
                }
                return null;
            }
            case "image/webp":
            {
                if (d.Length < 30) return null;
                var fourcc = d.Slice(12, 4);
                if (fourcc.SequenceEqual("VP8X"u8))
                    return new(1 + (d[24] | d[25] << 8 | d[26] << 16), 1 + (d[27] | d[28] << 8 | d[29] << 16));
                if (fourcc.SequenceEqual("VP8L"u8) && d[20] == 0x2F)
                {
                    uint b = BinaryPrimitives.ReadUInt32LittleEndian(d.Slice(21, 4));
                    return new((int)(b & 0x3FFF) + 1, (int)((b >> 14) & 0x3FFF) + 1);
                }
                if (fourcc.SequenceEqual("VP8 "u8))
                    return new(BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(26, 2)) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(28, 2)) & 0x3FFF);
                return null;
            }
            default: return null;
        }
    }

    public static bool DimensionsAcceptable(ImageDimensions dim)
        => dim.Width > 0 && dim.Height > 0 && dim.Width <= MaxImageSide && dim.Height <= MaxImageSide
           && (long)dim.Width * dim.Height <= MaxImagePixels;

    /// <summary>Strips path parts and unsafe characters; keeps a short safe base name.</summary>
    public static string SanitizeFileName(string? name)
    {
        var n = Path.GetFileName((name ?? "").Replace('\\', '/'));
        var sb = new StringBuilder();
        foreach (var c in n)
            sb.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_');
        var s = sb.ToString().Trim('.', '_');
        var ext = Path.GetExtension(s);
        var stem = Path.GetFileNameWithoutExtension(s).Replace('.', '_');
        if (stem.Length > 60) stem = stem[..60];
        return stem.Length == 0 ? "file" + ext : stem + ext;
    }

    /// <summary>Random, non-guessable object key; the extension must come from the allow-list.</summary>
    public static string RandomKey(string prefix, string allowedExtension)
        => $"{prefix.TrimEnd('/')}/{DateTime.UtcNow:yyyy/MM}/{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}{allowedExtension.ToLowerInvariant()}";
}
