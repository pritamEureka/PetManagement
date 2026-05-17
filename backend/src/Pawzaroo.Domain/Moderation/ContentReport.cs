using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Moderation;

public enum ReportTargetType { Post = 1, Comment = 2, Message = 3, User = 4, AdoptionListing = 5, Product = 6, Doctor = 7, Store = 8 }
public enum ReportStatus     { Open = 0, UnderReview = 1, Resolved = 2, Dismissed = 3 }

/// <summary>
/// Polymorphic report record. Keeps an exhaustive moderation queue without one
/// reports-table-per-target. Specific tables (post_reports, message_reports)
/// still exist for hot paths; this is the unified admin view.
/// </summary>
public class ContentReport : AuditableEntity
{
    public ReportTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public string? Details { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public Guid? ResolvedById { get; set; }
    public User? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
}
