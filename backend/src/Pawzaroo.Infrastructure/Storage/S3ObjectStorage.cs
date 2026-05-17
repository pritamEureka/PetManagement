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
        return PublicUrl(key);
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
        return Task.FromResult(new PresignedUpload(url, key, PublicUrl(key), expiry));
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
