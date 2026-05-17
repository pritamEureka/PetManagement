using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Messaging;

/// <summary>
/// Per-recipient delivery+read tracking. Unique on (MessageId, UserId).
/// Inserted when a message lands on a connected client; ReadAt is set when
/// the recipient opens the conversation.
/// </summary>
public class MessageReadReceipt : BaseEntity
{
    public Guid MessageId { get; set; }
    public Message Message { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
