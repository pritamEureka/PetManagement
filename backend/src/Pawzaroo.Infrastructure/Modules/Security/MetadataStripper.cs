using System.Buffers.Binary;

namespace Pawzaroo.Infrastructure.Modules.Security;

/// <summary>Byte-level metadata (EXIF/GPS/XMP/text) removal for JPEG and PNG without re-encoding pixels.</summary>
public static class MetadataStripper
{
    private static readonly byte[] PngSig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly HashSet<string> DroppedPng = new() { "tEXt", "iTXt", "zTXt", "eXIf", "tIME" };

    /// <summary>Returns cleaned bytes, or null if the structure is malformed (caller should reject).</summary>
    public static byte[]? Strip(ReadOnlySpan<byte> data, string mime) => mime switch
    {
        "image/jpeg" => StripJpeg(data),
        "image/png" => StripPng(data),
        _ => data.ToArray()
    };

    public static byte[]? StripJpeg(ReadOnlySpan<byte> d)
    {
        if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) return null;
        var o = new MemoryStream(d.Length);
        o.WriteByte(0xFF); o.WriteByte(0xD8);
        int i = 2;
        while (i < d.Length)
        {
            if (d[i] != 0xFF) return null;
            while (i < d.Length && d[i] == 0xFF) i++;
            if (i >= d.Length) return null;
            var m = d[i++];
            if (m == 0xD9) { o.WriteByte(0xFF); o.WriteByte(0xD9); return o.ToArray(); }
            if (m == 0x01 || (m >= 0xD0 && m <= 0xD7)) { o.WriteByte(0xFF); o.WriteByte(m); continue; }
            if (i + 2 > d.Length) return null;
            int len = BinaryPrimitives.ReadUInt16BigEndian(d.Slice(i, 2));
            if (len < 2 || i + len > d.Length) return null;
            bool drop = m == 0xE1 || m == 0xED || m == 0xFE || (m >= 0xE3 && m <= 0xEF);
            if (!drop)
            {
                o.WriteByte(0xFF); o.WriteByte(m);
                o.Write(d.Slice(i, len));
            }
            i += len;
            if (m == 0xDA)
            {
                // Entropy-coded data: copy until the next real marker (not 0xFF00 stuffing or RSTn).
                int s = i;
                while (i + 1 < d.Length && !(d[i] == 0xFF && d[i + 1] != 0x00 && !(d[i + 1] >= 0xD0 && d[i + 1] <= 0xD7))) i++;
                if (i + 1 >= d.Length) return null;
                o.Write(d.Slice(s, i - s));
            }
        }
        return null;
    }

    public static byte[]? StripPng(ReadOnlySpan<byte> d)
    {
        if (d.Length < 8 || !d[..8].SequenceEqual(PngSig)) return null;
        var o = new MemoryStream(d.Length);
        o.Write(PngSig);
        int i = 8;
        bool sawIend = false;
        while (i + 12 <= d.Length)
        {
            uint len = BinaryPrimitives.ReadUInt32BigEndian(d.Slice(i, 4));
            if (len > int.MaxValue - 12 || i + 12L + len > d.Length) return null;
            var type = System.Text.Encoding.ASCII.GetString(d.Slice(i + 4, 4));
            int total = 12 + (int)len;
            if (!DroppedPng.Contains(type)) o.Write(d.Slice(i, total));
            i += total;
            if (type == "IEND") { sawIend = true; break; }
        }
        return sawIend ? o.ToArray() : null;
    }
}
