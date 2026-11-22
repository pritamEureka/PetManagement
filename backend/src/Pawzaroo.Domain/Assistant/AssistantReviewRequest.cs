using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Assistant;

public enum AssistantReviewStatus { Pending, Reviewed }

/// <summary>A request for a human veterinarian to look at a symptom checklist and/or photos from the assistant.</summary>
public class AssistantReviewRequest : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid? PetId { get; set; }
    /// <summary>Routine | Soon | Urgent | Emergency (deterministic score at submission time).</summary>
    public string Urgency { get; set; } = "Routine";
    public string Summary { get; set; } = default!;
    public string? ChecklistJson { get; set; }
    public string? ImageUrlsJson { get; set; }
    public AssistantReviewStatus Status { get; set; } = AssistantReviewStatus.Pending;
    public string? VetNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
