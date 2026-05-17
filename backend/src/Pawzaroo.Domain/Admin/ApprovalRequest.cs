using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Admin;

public enum ApprovalEntityType { Doctor = 1, Store = 2, AdoptionListing = 3, ServiceProvider = 4, Product = 5, Refund = 6 }
public enum ApprovalDecision { Pending = 0, Approved = 1, Rejected = 2, NeedsMoreInfo = 3 }

/// <summary>
/// Unified admin approval inbox. Per-module entities still carry their own
/// ApprovalStatus column for hot reads; this table powers the global queue,
/// SLA timers, and audit (decided_by, decided_at, reason).
/// </summary>
public class ApprovalRequest : AuditableEntity
{
    public ApprovalEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public Guid SubmittedById { get; set; }
    public User SubmittedBy { get; set; } = default!;
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public Guid? DecidedById { get; set; }
    public User? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime? SlaDueAt { get; set; }
    /// <summary>JSONB snapshot of the submitted payload for diffing/auditing.</summary>
    public string? PayloadJson { get; set; }
}
