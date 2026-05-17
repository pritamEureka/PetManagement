using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Domain.Adoption;

public class AdoptionListing : AuditableEntity
{
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = default!;
    public Guid? PetId { get; set; }
    public Pet? Pet { get; set; }

    // Core identity
    public string Title { get; set; } = default!;
    public string? PetName { get; set; }
    public string? Description { get; set; }

    // Animal attributes
    public AnimalType AnimalType { get; set; }
    public string? Breed { get; set; }
    public int? AgeMonths { get; set; }
    public Gender Gender { get; set; }
    public AnimalSize? Size { get; set; }
    public string? Color { get; set; }

    // Health
    public bool Vaccinated { get; set; }
    public string? VaccinationDetails { get; set; }
    public bool NeuteredSpayed { get; set; }
    public string? HealthCondition { get; set; }

    // Behavior
    public string? BehaviorNotes { get; set; }
    public bool? GoodWithChildren { get; set; }     // null = unknown
    public bool? GoodWithOtherPets { get; set; }    // null = unknown

    // Logistics
    public string? Location { get; set; }
    public decimal AdoptionFee { get; set; }
    public string? ReasonForListing { get; set; }
    public ContactPreference ContactPreference { get; set; }

    // Workflow
    public AdoptionListingStatus Status { get; set; } = AdoptionListingStatus.Draft;
    public string? AdminNotes { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }
    public DateTime? AdoptedAt { get; set; }
    public Guid? AdoptedByUserId { get; set; }
    public User? AdoptedByUser { get; set; }

    public ICollection<AdoptionListingPhoto> Photos { get; set; } = new List<AdoptionListingPhoto>();
    public ICollection<AdoptionRequest> Requests { get; set; } = new List<AdoptionRequest>();
}

public class AdoptionListingPhoto : BaseEntity
{
    public Guid AdoptionListingId { get; set; }
    public AdoptionListing AdoptionListing { get; set; } = default!;
    public string Url { get; set; } = default!;
    public int OrderIndex { get; set; }
}

public class AdoptionRequest : AuditableEntity
{
    public Guid AdoptionListingId { get; set; }
    public AdoptionListing AdoptionListing { get; set; } = default!;
    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = default!;
    public string Message { get; set; } = default!;
    public AdoptionRequestStatus Status { get; set; } = AdoptionRequestStatus.Pending;
}

/// <summary>
/// "I'm looking to adopt" wanted-post — independent of any specific listing.
/// Goes through the same approval workflow as listings.
/// </summary>
public class AdoptionWantedPost : AuditableEntity
{
    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = default!;

    public AnimalType AnimalType { get; set; }
    public string? Breed { get; set; }
    public int? PreferredAgeMonthsMin { get; set; }
    public int? PreferredAgeMonthsMax { get; set; }
    public AnimalSize? PreferredSize { get; set; }
    public string? PreferredLocation { get; set; }

    public string? ExperienceWithPets { get; set; }
    public HomeEnvironment? HomeEnvironment { get; set; }
    public string? OtherPetsAtHome { get; set; }
    public string? ReasonForAdoption { get; set; }
    public ContactPreference ContactPreference { get; set; }

    public string? Description { get; set; }
    public AdoptionListingStatus Status { get; set; } = AdoptionListingStatus.PendingApproval;
    public string? AdminNotes { get; set; }
}

public class SavedAdoptionListing : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public Guid AdoptionListingId { get; set; }
    public AdoptionListing AdoptionListing { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
