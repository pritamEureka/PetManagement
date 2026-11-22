using System.Collections.Frozen;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Services;

namespace Pawzaroo.Infrastructure.Modules.Security;

/// <summary>
/// Pre-upload validation:
///   1. Extension allowlist  (not denylist — denylists always lose).
///   2. MIME sniff via magic-byte prefix; reject if it disagrees with the
///      claimed content-type (catches "selfie.jpg" that's actually an .exe).
///   3. Size cap per content category.
///
/// <see cref="ScanAsync"/> is a hook for a real malware scanner (ClamAV, AWS
/// GuardDuty Malware Protection, Defender for Storage). Default impl is a
/// no-op so callers can wire it now and swap the implementation later.
/// </summary>
public class FileValidationService : IFileValidationService
{
    private const long MaxBytes = 200L * 1024 * 1024;    // global ceiling; per-type caps in UploadInspector
    private readonly IMalwareScanner _scanner;

    public FileValidationService(IMalwareScanner? scanner = null)
        => _scanner = scanner ?? new CompositeMalwareScanner();

    // Allowlist (lowercase ext -> set of legal MIME prefixes)
    private static readonly FrozenDictionary<string, string[]> Allowed =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"]  = new[] { "image/jpeg" },
            [".jpeg"] = new[] { "image/jpeg" },
            [".png"]  = new[] { "image/png" },
            [".webp"] = new[] { "image/webp" },
            [".gif"]  = new[] { "image/gif" },
            [".mp4"]  = new[] { "video/mp4" },
            [".webm"] = new[] { "video/webm" },
            [".pdf"]  = new[] { "application/pdf" }
        }.ToFrozenDictionary();

    // Magic-byte prefixes.
    private static readonly (string Ext, byte[] Prefix)[] Signatures =
    {
        (".jpg",  new byte[] { 0xFF, 0xD8, 0xFF }),
        (".jpeg", new byte[] { 0xFF, 0xD8, 0xFF }),
        (".png",  new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        (".gif",  new byte[] { 0x47, 0x49, 0x46, 0x38 }),  // GIF8
        (".webp", new byte[] { 0x52, 0x49, 0x46, 0x46 }),  // "RIFF" then "WEBP" at offset 8
        (".mp4",  new byte[] { 0x00, 0x00, 0x00 }),        // looser — first 4 bytes are box size
        (".pdf",  new byte[] { 0x25, 0x50, 0x44, 0x46 })   // %PDF
    };

    public FileValidationResult Validate(string fileName, string? contentType, long sizeBytes, ReadOnlySpan<byte> headBytes)
    {
        if (sizeBytes <= 0) return new(false, "Empty file.");
        if (sizeBytes > MaxBytes) return new(false, $"Exceeds {MaxBytes / (1024 * 1024)} MB limit.");

        var pre = ValidatePreflight(fileName, contentType);
        if (!pre.Allowed) return pre;

        var ext = Path.GetExtension(fileName ?? "");
        var sig = Signatures.FirstOrDefault(s => s.Ext.Equals(ext, StringComparison.OrdinalIgnoreCase));
        if (sig.Prefix is not null && !StartsWith(headBytes, sig.Prefix))
            return new(false, $"File contents do not look like {ext}.");

        // Stronger checks: sniffed MIME must agree with the declared one, per-type size cap, polyglots, pixel limits.
        var sniff = UploadInspector.Sniff(headBytes);
        if (sniff.Mime is null) return new(false, "File type could not be determined.");
        var declared = (contentType ?? "").Split(';')[0].Trim();
        if (!string.Equals(declared, sniff.Mime, StringComparison.OrdinalIgnoreCase))
            return new(false, $"Content-Type {declared} does not match file contents ({sniff.Mime}).");
        if (UploadInspector.MaxBytesByMime.TryGetValue(sniff.Mime, out var cap) && sizeBytes > cap)
            return new(false, $"Exceeds {cap / (1024 * 1024)} MB limit for {sniff.Mime}.");
        var poly = UploadInspector.DetectPolyglot(headBytes, sniff.Mime);
        if (poly is not null) return new(false, poly);
        if (sniff.Mime.StartsWith("image/", StringComparison.Ordinal))
        {
            var dim = UploadInspector.ReadDimensions(headBytes, sniff.Mime);
            if (dim is not null && !UploadInspector.DimensionsAcceptable(dim))
                return new(false, "Image dimensions exceed the allowed limit.");
        }

        return new(true, null);
    }

    public FileValidationResult ValidatePreflight(string fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName ?? "");
        if (string.IsNullOrEmpty(ext) || !Allowed.TryGetValue(ext, out var mimePrefixes))
            return new(false, $"Extension '{ext}' is not allowed.");

        if (string.IsNullOrWhiteSpace(contentType))
            return new(false, "Content-Type is required.");

        if (!mimePrefixes.Any(p => contentType.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return new(false, $"Content-Type {contentType} does not match {ext}.");

        return new(true, null);
    }

    /// <summary>
    /// Placeholder for malware scanning. Drop in a ClamAV / VirusTotal /
    /// platform-specific antimalware call here. Always returns Allowed
    /// in dev so the path stays exercised.
    /// </summary>
    public async Task<FileValidationResult> ScanAsync(Stream content, CancellationToken ct = default)
    {
        var r = await _scanner.ScanAsync(content, ct);
        return r.Clean ? new FileValidationResult(true, null) : new FileValidationResult(false, $"Malware scan failed: {r.Signature}");
    }

    private static bool StartsWith(ReadOnlySpan<byte> head, byte[] sig)
    {
        if (head.Length < sig.Length) return false;
        for (int i = 0; i < sig.Length; i++)
            if (head[i] != sig[i]) return false;
        return true;
    }
}
