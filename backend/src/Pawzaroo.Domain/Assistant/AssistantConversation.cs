using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Assistant;

/// <summary>A private chat thread between one user and the care assistant.</summary>
public class AssistantConversation : AuditableEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = "New conversation";
    public Guid? PetId { get; set; }
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    /// <summary>True once any assistant reply in the thread was an emergency escalation.</summary>
    public bool HasEscalation { get; set; }
    public ICollection<AssistantMessage> Messages { get; set; } = new List<AssistantMessage>();
}

public class AssistantMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public AssistantConversation Conversation { get; set; } = default!;
    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = "user";
    public string Content { get; set; } = default!;
    /// <summary>JSON array of { sourceType, id, title, url }; null for user messages.</summary>
    public string? CitationsJson { get; set; }
    public bool Escalated { get; set; }
    public string? Intent { get; set; }
    /// <summary>JSON array of image URLs the user attached (no vision model reads them; they are for human vet review).</summary>
    public string? AttachmentsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
