namespace Pawzaroo.Infrastructure.Storage;

public class StorageOptions
{
    public string Endpoint { get; set; } = default!;       // e.g. https://s3.amazonaws.com or http://minio:9000
    public string Region { get; set; } = "us-east-1";
    public string Bucket { get; set; } = "pawzaroo-media";
    public string AccessKey { get; set; } = default!;
    public string SecretKey { get; set; } = default!;
    public bool UsePathStyle { get; set; } = true;
    public string? PublicBaseUrl { get; set; }             // CDN/public host, optional
}
