using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Adoption;

/// <summary>Pipeline stage layered over <see cref="AdoptionRequest"/>; does not change AdoptionRequestStatus semantics.</summary>
public enum AdoptionApplicationStage
{
    Submitted = 0, ReferenceCheck = 1, HomeVisit = 2, Decision = 3, Adopted = 4, FollowUp = 5, Declined = 6, Withdrawn = 7
}

public enum AdoptionReferenceStatus { Pending = 0, Contacted = 1, Verified = 2, Failed = 3 }
public enum AdoptionHomeVisitStatus { Scheduled = 0, Completed = 1, Cancelled = 2, NoShow = 3 }
public enum AdoptionFollowUpStatus { Due = 0, Completed = 1, Skipped = 2 }
public enum FosterOutcome { Ongoing = 0, FosterFailed = 1, ConvertedToAdoption = 2, Returned = 3 }
public enum AdopterExperience { None = 0, Some = 1, Experienced = 2 }
public enum ActivityLevel { Low = 0, Moderate = 1, High = 2 }
public enum ListingRiskKind { DuplicateTitle = 0, DuplicatePhoto = 1, DuplicatePhone = 2, ExtremeFee = 3, NewAccountManyListings = 4, StockPhoto = 5 }

public class AdoptionRequestStageEvent : BaseEntity
{
    public Guid RequestId { get; set; }
    public AdoptionRequest Request { get; set; } = default!;
    public AdoptionApplicationStage Stage { get; set; }
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
    public Guid? ByUserId { get; set; }
    public string? Note { get; set; }
    /// <summary>SLA deadline for leaving this stage (set for Decision).</summary>
    public DateTime? DueAt { get; set; }
    public DateTime? OverdueFlaggedAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }
}

public class AdoptionReference : BaseEntity
{
    public Guid RequestId { get; set; }
    public AdoptionRequest Request { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Contact { get; set; } = default!;
    public string? Relationship { get; set; }
    public AdoptionReferenceStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AdoptionHomeVisit : BaseEntity
{
    public Guid RequestId { get; set; }
    public AdoptionRequest Request { get; set; } = default!;
    public DateTime ScheduledAt { get; set; }
    public AdoptionHomeVisitStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AdoptionFollowUp : BaseEntity
{
    public Guid ListingId { get; set; }
    public AdoptionListing Listing { get; set; } = default!;
    public Guid AdopterId { get; set; }
    public int DayOffset { get; set; }
    public DateTime DueAt { get; set; }
    public AdoptionFollowUpStatus Status { get; set; }
    public string? Notes { get; set; }
    /// <summary>1 (poor) .. 5 (thriving).</summary>
    public int? WelfareScore { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }
}

public class FosterPlacement : BaseEntity
{
    public Guid ListingId { get; set; }
    public AdoptionListing Listing { get; set; } = default!;
    public Guid? PetId { get; set; }
    public Guid FosterUserId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public FosterOutcome Outcome { get; set; }
    public string? Notes { get; set; }
}

public class AdopterProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public int HouseholdSize { get; set; } = 1;
    /// <summary>Comma separated ages in years, e.g. "3,7".</summary>
    public string? ChildrenAges { get; set; }
    /// <summary>JSON array of {type,count}.</summary>
    public string? ExistingPetsJson { get; set; }
    public AdopterExperience Experience { get; set; }
    public HomeEnvironment? HomeType { get; set; }
    public bool HasYard { get; set; }
    public int TravelDistanceKm { get; set; } = 25;
    public ActivityLevel Activity { get; set; } = ActivityLevel.Moderate;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AdoptionListingRiskFlag : BaseEntity
{
    public Guid ListingId { get; set; }
    public AdoptionListing Listing { get; set; } = default!;
    public ListingRiskKind Kind { get; set; }
    public string Detail { get; set; } = default!;
    public int Severity { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
