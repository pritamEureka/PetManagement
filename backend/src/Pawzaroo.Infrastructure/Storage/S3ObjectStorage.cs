using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Infrastructure.Storage;

public class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly IAmazonS3 _s3;
    private readonly StorageOptions _opts;

    public S3ObjectStorage(IOptions<StorageOptions> options)
    {
        _opts = options.Value;
        var config = new AmazonS3Config
        {
            ServiceURL = _opts.Endpoint,
            ForcePathStyle = _opts.UsePathStyle,
            AuthenticationRegion = _opts.Region
        };
        _s3 = new AmazonS3Client(new BasicAWSCredentials(_opts.AccessKey, _opts.SecretKey), config);
    }

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opts.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            DisablePayloadSigning = true
        }, ct);
        return ResolveUrl(key);
    }

    public Task<PresignedUpload> CreatePresignedUploadAsync(string key, string contentType, TimeSpan? expiresIn = null, CancellationToken ct = default)
    {
        var expiry = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(15));
        var url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _opts.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = expiry,
            ContentType = contentType
        });
        return Task.FromResult(new PresignedUpload(url, key, ResolveUrl(key), expiry));
    }

    public Task<string> CreatePresignedDownloadAsync(string key, TimeSpan? expiresIn = null, CancellationToken ct = default)
    {
        var url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _opts.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(15))
        });
        return Task.FromResult(url);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => _s3.DeleteObjectAsync(_opts.Bucket, key, ct);

    public async Task<ObjectHead?> HeadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var m = await _s3.GetObjectMetadataAsync(_opts.Bucket, key, ct);
            return new ObjectHead(m.ContentLength, m.Headers.ContentType, m.LastModified.ToUniversalTime());
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<byte[]> ReadRangeAsync(string key, long from, int length, CancellationToken ct = default)
    {
        if (length <= 0) return Array.Empty<byte>();
        using var resp = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _opts.Bucket,
            Key = key,
            ByteRange = new ByteRange(from, from + length - 1)
        }, ct);
        using var ms = new MemoryStream();
        await resp.ResponseStream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public string GetUrl(string key) => ResolveUrl(key);

    /// <summary>Public URL, or an expiring presigned GET when Storage:PrivateByDefault is on.</summary>
    private string ResolveUrl(string key)
    {
        if (!_opts.PrivateByDefault) return PublicUrl(key);
        var minutes = Math.Clamp(_opts.SignedUrlMinutes, 1, 7 * 24 * 60);
        return _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _opts.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(minutes)
        });
    }

    private string PublicUrl(string key)
    {
        if (!string.IsNullOrWhiteSpace(_opts.PublicBaseUrl))
            return $"{_opts.PublicBaseUrl.TrimEnd('/')}/{key}";
        var endpoint = _opts.Endpoint.TrimEnd('/');
        return _opts.UsePathStyle
            ? $"{endpoint}/{_opts.Bucket}/{key}"
            : $"{endpoint.Replace("://", $"://{_opts.Bucket}.")}/{key}";
    }

    public void Dispose() => _s3.Dispose();
}
