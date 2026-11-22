namespace Pawzaroo.Application.Common.Interfaces;

public interface IObjectStorage
{
    /// <summary>Upload raw bytes; returns a publicly accessible URL.</summary>
    Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Issue a presigned PUT URL the client can upload to directly.</summary>
    Task<PresignedUpload> CreatePresignedUploadAsync(string key, string contentType, TimeSpan? expiresIn = null, CancellationToken ct = default);

    /// <summary>Issue a presigned GET URL for a private object.</summary>
    Task<string> CreatePresignedDownloadAsync(string key, TimeSpan? expiresIn = null, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Object metadata without downloading it; null when the object does not exist.</summary>
    Task<ObjectHead?> HeadAsync(string key, CancellationToken ct = default);

    /// <summary>Reads <paramref name="length"/> bytes starting at <paramref name="from"/> (fewer at end of object).</summary>
    Task<byte[]> ReadRangeAsync(string key, long from, int length, CancellationToken ct = default);

    /// <summary>Final URL for an object (public, or an expiring presigned GET when the bucket is private-by-default).</summary>
    string GetUrl(string key);
}

public record ObjectHead(long Size, string? ContentType, DateTime? LastModified);

public record PresignedUpload(string Url, string Key, string PublicUrl, DateTime ExpiresAt);
