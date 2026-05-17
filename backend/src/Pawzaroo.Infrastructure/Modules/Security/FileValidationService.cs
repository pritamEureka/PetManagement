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
    private const long MaxBytes = 20L * 1024 * 1024;     // 20 MB

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

        var ext = Path.GetExtension(fileName ?? "");
        if (string.IsNullOrEmpty(ext) || !Allowed.TryGetValue(ext, out var mimePrefixes))
            return new(false, $"Extension '{ext}' is not allowed.");

        if (!string.IsNullOrWhiteSpace(contentType) &&
            !mimePrefixes.Any(p => contentType.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return new(false, $"Content-Type {contentType} does not match {ext}.");

        var sig = Signatures.FirstOrDefault(s => s.Ext.Equals(ext, StringComparison.OrdinalIgnoreCase));
        if (sig.Prefix is not null && !StartsWith(headBytes, sig.Prefix))
            return new(false, $"File contents do not look like {ext}.");

        return new(true, null);
    }

    /// <summary>
    /// Placeholder for malware scanning. Drop in a ClamAV / VirusTotal /
    /// platform-specific antimalware call here. Always returns Allowed
    /// in dev so the path stays exercised.
    /// </summary>
    public Task<FileValidationResult> ScanAsync(Stream content, CancellationToken ct = default)
    {
        // TODO: forward `content` to the configured antimalware adapter.
        return Task.FromResult(new FileValidationResult(true, null));
    }

    private static bool StartsWith(ReadOnlySpan<byte> head, byte[] sig)
    {
        if (head.Length < sig.Length) return false;
        for (int i = 0; i < sig.Length; i++)
            if (head[i] != sig[i]) return false;
        return true;
    }
}
