using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Messaging;

public class Conversation : AuditableEntity
{
    public string? Title { get; set; }
    public bool IsGroup { get; set; }
    public string? ContextType { get; set; }  // adoption | vet | store | appointment | direct
    public Guid? ContextRefId { get; set; }
    public DateTime? LastMessageAt { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

public class ConversationParticipant : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastReadAt { get; set; }
    public int UnreadCount { get; set; }
    public bool IsMuted { get; set; }
    public bool IsArchived { get; set; }
    public bool HasLeft { get; set; }
}

public class Message : AuditableEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = default!;
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = default!;
    public MessageType Type { get; set; } = MessageType.Text;
    public string? Content { get; set; }
    public string? MediaUrl { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public Message? ReplyToMessage { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeletedForAll { get; set; }
}

public class UserBlock : BaseEntity
{
    public Guid BlockerId { get; set; }
    public User Blocker { get; set; } = default!;
    public Guid BlockedUserId { get; set; }
    public User BlockedUser { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}

public class MessageReport : AuditableEntity
{
    public Guid MessageId { get; set; }
    public Message Message { get; set; } = default!;
    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public bool Resolved { get; set; }
}
