using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Pets;

public enum SightingStatus { Pending = 0, Verified = 1, Rejected = 2 }

public enum EvidenceKind { Photo = 0, Document = 1 }

/// <summary>Extra matching details kept off the main report table (one row per report).</summary>
public class LostFoundDetailExtra : BaseEntity
{
    public Guid ReportId { get; set; }
    public string? Markings { get; set; }
    public string? CollarDescription { get; set; }
    public string? Sex { get; set; }
    public string? AgeEstimate { get; set; }
    public string? MicrochipNumber { get; set; }
    public DateTime? TimeWindowStart { get; set; }
    public DateTime? TimeWindowEnd { get; set; }
    /// <summary>Set when the worker has nulled the exact coordinates on the report.</summary>
    public DateTime? LocationExpiredAt { get; set; }
}

public class LostFoundSighting : BaseEntity
{
    public Guid ReportId { get; set; }
    public Guid ReporterUserId { get; set; }
    public DateTime SeenAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Description { get; set; } = default!;
    public string? PhotoUrl { get; set; }
    public SightingStatus Status { get; set; } = SightingStatus.Pending;
    public Guid? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class LostFoundEvidence : BaseEntity
{
    public Guid ReportId { get; set; }
    public string Url { get; set; } = default!;
    public EvidenceKind Kind { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Geofence a user wants alerts for. Matched against the fuzzed public coordinates.</summary>
public class LostFoundAlertSubscription : BaseEntity
{
    public Guid UserId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusKm { get; set; }
    public AnimalType? AnimalType { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
