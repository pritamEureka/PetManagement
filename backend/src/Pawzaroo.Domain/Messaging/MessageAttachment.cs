using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Messaging;

public class MessageAttachment : BaseEntity
{
    public Guid MessageId { get; set; }
    public Message Message { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string MimeType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string? FileName { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
