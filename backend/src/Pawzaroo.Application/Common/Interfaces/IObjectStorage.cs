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
}

public record PresignedUpload(string Url, string Key, string PublicUrl, DateTime ExpiresAt);
