using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Infrastructure.Modules.Security;

namespace Pawzaroo.Infrastructure.Storage;

public record UploadConfirmResult(bool Ok, int Status, string? Code, string? Reason, string? Url);

/// <summary>
/// Post-upload verification for direct-to-S3 (presigned PUT) uploads, where the API never saw the bytes.
/// Reads the object back, validates it and DELETES it when invalid; for small JPEG/PNG it also rewrites the
/// object with EXIF/GPS metadata stripped.
/// </summary>
public static class UploadConfirmer
{
    public const int HeadBytes = 64 * 1024;
    public const long RewriteMaxBytes = 8L * 1024 * 1024;

    public static async Task<UploadConfirmResult> ConfirmAsync(
        IObjectStorage storage, IFileValidationService validation, Guid userId,
        string? key, string? declaredContentType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains("..") || key.Contains('\\')
            || !key.StartsWith($"u/{userId}/", StringComparison.Ordinal))
            return new(false, 403, "forbidden", "You can only confirm your own uploads.", null);

        var head = await storage.HeadAsync(key, ct);
        if (head is null) return new(false, 404, "not_found", "Upload not found. Was the PUT completed?", null);

        var declared = (declaredContentType ?? "").Split(';')[0].Trim();
        var stored = (head.ContentType ?? "").Split(';')[0].Trim();

        async Task<UploadConfirmResult> Reject(string reason)
        {
            await storage.DeleteAsync(key, ct);
            return new(false, 422, "invalid_file", reason, null);
        }

        if (stored.Length > 0 && !string.Equals(stored, declared, StringComparison.OrdinalIgnoreCase))
            return await Reject($"Stored content-type '{stored}' does not match declared '{declared}'.");

        var fileName = key[(key.LastIndexOf('/') + 1)..];
        var headBytes = await storage.ReadRangeAsync(key, 0, (int)Math.Min(head.Size, HeadBytes), ct);

        var v = validation.Validate(fileName, declared, head.Size, headBytes);
        if (!v.Allowed) return await Reject(v.Reason ?? "File rejected.");

        var scan = await validation.ScanAsync(new MemoryStream(headBytes), ct);
        if (!scan.Allowed) return await Reject(scan.Reason ?? "File rejected by scanner.");

        var sniff = UploadInspector.Sniff(headBytes);
        var rewritable = sniff.Mime is "image/jpeg" or "image/png" && head.Size <= RewriteMaxBytes;
        if (rewritable)
        {
            var full = await storage.ReadRangeAsync(key, 0, (int)head.Size, ct);
            var processed = UploadPipeline.Process(fileName, declared, full);
            if (!processed.Allowed) return await Reject(processed.Reason ?? "File rejected.");
            var clean = processed.CleanBytes!;
            if (!clean.AsSpan().SequenceEqual(full))
            {
                using var ms = new MemoryStream(clean);
                var url = await storage.UploadAsync(key, ms, sniff.Mime!, ct);
                return new(true, 200, null, null, url);
            }
        }

        return new(true, 200, null, null, storage.GetUrl(key));
    }
}
