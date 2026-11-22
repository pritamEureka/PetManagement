namespace Pawzaroo.Infrastructure.Modules.Security;

public record UploadProcessResult(bool Allowed, string? Reason, byte[]? CleanBytes, string? Mime, string? Extension);

/// <summary>
/// Full in-memory upload check for server-side uploads: extension allow-list vs sniffed MIME vs declared
/// content-type, size per type, polyglot rejection, pixel limits, EICAR, and EXIF/GPS stripping.
/// </summary>
public static class UploadPipeline
{
    private static readonly Dictionary<string, string[]> ExtToMime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new[] { "image/jpeg" }, [".jpeg"] = new[] { "image/jpeg" },
        [".png"] = new[] { "image/png" }, [".webp"] = new[] { "image/webp" }, [".gif"] = new[] { "image/gif" },
        [".mp4"] = new[] { "video/mp4" }, [".webm"] = new[] { "video/webm" }, [".pdf"] = new[] { "application/pdf" }
    };

    public static UploadProcessResult Process(string fileName, string? declaredContentType, byte[] data)
    {
        static UploadProcessResult Fail(string r) => new(false, r, null, null, null);

        if (data.Length == 0) return Fail("Empty file.");
        var ext = Path.GetExtension(UploadInspector.SanitizeFileName(fileName));
        if (!ExtToMime.TryGetValue(ext ?? "", out var mimes)) return Fail($"Extension '{ext}' is not allowed.");

        var sniff = UploadInspector.Sniff(data);
        if (sniff.Mime is null) return Fail("File type could not be determined.");
        if (!mimes.Contains(sniff.Mime, StringComparer.OrdinalIgnoreCase)) return Fail($"File contents ({sniff.Mime}) do not match extension {ext}.");

        var declared = (declaredContentType ?? "").Split(';')[0].Trim();
        if (!string.Equals(declared, sniff.Mime, StringComparison.OrdinalIgnoreCase))
            return Fail($"Declared content-type '{declared}' does not match contents ({sniff.Mime}).");

        if (UploadInspector.MaxBytesByMime.TryGetValue(sniff.Mime, out var max) && data.Length > max)
            return Fail($"Exceeds {max / (1024 * 1024)} MB limit for {sniff.Mime}.");

        var poly = UploadInspector.DetectPolyglot(data, sniff.Mime);
        if (poly is not null) return Fail(poly);

        if (BuiltInMalwareScanner.ContainsEicar(data)) return Fail("Malware signature detected.");

        var bytes = data;
        if (sniff.Mime.StartsWith("image/", StringComparison.Ordinal))
        {
            var dim = UploadInspector.ReadDimensions(data, sniff.Mime);
            if (dim is null) return Fail("Image dimensions could not be read.");
            if (!UploadInspector.DimensionsAcceptable(dim)) return Fail("Image dimensions exceed the allowed limit.");
            var cleaned = MetadataStripper.Strip(data, sniff.Mime);
            if (cleaned is null) return Fail("Image is malformed.");
            bytes = cleaned;
        }
        return new(true, null, bytes, sniff.Mime, sniff.Extension);
    }
}
