using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Moderation;

public enum ModerationActionType
{
    Warn         = 0,
    Suspend      = 1,
    Ban          = 2,
    Hide         = 3,
    Restore      = 4,
    Approve      = 5,
    Reject       = 6,
    MarkSuspicious = 7,
    Escalate     = 8,
    Unhide       = 9
}

public enum ModerationTargetType { Post = 1, Comment = 2, Message = 3, User = 4, AdoptionListing = 5, Product = 6, Doctor = 7, Store = 8, Review = 9 }

/// <summary>
/// Immutable record of a moderation decision. Pairs with <see cref="ContentReport"/>
/// (a single report can produce zero or many actions) and with the optional
/// <see cref="UserSuspension"/> / <see cref="UserWarning"/> the action created.
/// </summary>
public class ModerationAction : AuditableEntity
{
    public ModerationActionType Action { get; set; }
    public ModerationTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }

    public Guid? ReportId { get; set; }
    public ContentReport? Report { get; set; }

    public Guid ModeratorId { get; set; }
    public User Moderator { get; set; } = default!;

    public string? Notes { get; set; }

    /// <summary>If this action created a hold / strike, link to it for audit trails.</summary>
    public Guid? RelatedSuspensionId { get; set; }
    public UserSuspension? RelatedSuspension { get; set; }
    public Guid? RelatedWarningId { get; set; }
    public UserWarning? RelatedWarning { get; set; }
}

/// <summary>
/// Admin action history — broader than moderation. Captures every privileged
/// state change (KYC approvals, commission updates, refunds, role grants...).
/// </summary>
public class AdminActionLog : BaseEntity
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public Guid AdminId { get; set; }
    public User Admin { get; set; } = default!;

    public string Action { get; set; } = default!;        // "store.approve" | "user.role.grant" | ...
    public string TargetType { get; set; } = default!;    // "Store" | "User" | "Order" | ...
    public string? TargetId { get; set; }
    public string? Reason { get; set; }
    public string? PayloadJson { get; set; }              // request body, redacted
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
