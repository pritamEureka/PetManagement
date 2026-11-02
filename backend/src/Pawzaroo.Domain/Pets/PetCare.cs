using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Pets;

public enum CaretakerRole { Viewer = 0, Caretaker = 1, CoOwner = 2 }

public enum CareActivityType { Feeding = 0, Walk = 1, Medication = 2, Litter = 3, Bath = 4, Play = 5, Training = 6, Other = 99 }

public enum PetExpenseCategory { Food = 0, Vet = 1, Grooming = 2, Medicine = 3, Accessories = 4, Insurance = 5, Services = 6, Training = 7, Other = 99 }

public enum ProductionType { Milk = 0, Eggs = 1, Wool = 2, Meat = 3, Other = 99 }

/// <summary>Point-in-time weight measurement; Pet.WeightKg mirrors the latest entry.</summary>
public class PetWeightLog : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public DateTime RecordedOn { get; set; }
    public decimal WeightKg { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Medication course. Created manually or derived from a vet Prescription's
/// ItemsJson. DoseTimes is a comma list of local "HH:mm" times used by the
/// reminder job.
/// </summary>
public class PetMedication : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public Guid? PrescriptionId { get; set; }
    public string DrugName { get; set; } = default!;
    public string? Dose { get; set; }
    public string DoseTimes { get; set; } = "09:00";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Instructions { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<MedicationDoseLog> Doses { get; set; } = new List<MedicationDoseLog>();
}

public class MedicationDoseLog : BaseEntity
{
    public Guid MedicationId { get; set; }
    public PetMedication Medication { get; set; } = default!;
    public DateTime ScheduledFor { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    public Guid LoggedByUserId { get; set; }
    public bool Skipped { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Shared access to a pet for family members, sitters and walkers.</summary>
public class PetCaretaker : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public CaretakerRole Role { get; set; }
    public Guid InvitedById { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Recurring daily routine (feeding at 08:00, evening walk, ...).</summary>
public class PetCareRoutine : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public CareActivityType ActivityType { get; set; }
    public string Title { get; set; } = default!;
    public TimeOnly? TimeOfDay { get; set; }
    public string? Details { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PetCareLog : BaseEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public Guid? RoutineId { get; set; }
    public PetCareRoutine? Routine { get; set; }
    public CareActivityType ActivityType { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public Guid PerformedByUserId { get; set; }
    public User PerformedBy { get; set; } = default!;
    public string? Notes { get; set; }
}

public class PetExpense : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public PetExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public DateTime SpentOn { get; set; }
    public string? Description { get; set; }
}

public class PetInsurancePolicy : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public string Provider { get; set; } = default!;
    public string PolicyNumber { get; set; } = default!;
    public string? CoverageSummary { get; set; }
    public decimal? CoverageLimit { get; set; }
    public decimal? Premium { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? DocumentUrl { get; set; }
}

/// <summary>Time-limited, revocable read-only link to a pet's health record.</summary>
public class PetHealthShareLink : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? Label { get; set; }
}

public class PetOwnershipTransfer : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public Guid? AdoptionListingId { get; set; }
    public DateTime TransferredAt { get; set; } = DateTime.UtcNow;
    public bool IncludesHealthHistory { get; set; } = true;
}

/// <summary>Herd / flock / litter grouping for livestock and breeders.</summary>
public class PetGroup : AuditableEntity
{
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = default!;
    public string Name { get; set; } = default!;
    public AnimalType AnimalType { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public ICollection<Pet> Members { get; set; } = new List<Pet>();
}

public class ProductionLog : AuditableEntity
{
    public Guid OwnerId { get; set; }
    public Guid? PetGroupId { get; set; }
    public PetGroup? PetGroup { get; set; }
    public Guid? PetId { get; set; }
    public Pet? Pet { get; set; }
    public ProductionType Type { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = default!;
    public DateTime RecordedOn { get; set; }
    public string? Notes { get; set; }
}

public class BreedingRecord : AuditableEntity
{
    public Guid OwnerId { get; set; }
    public Guid DamId { get; set; }
    public Pet Dam { get; set; } = default!;
    public Guid? SireId { get; set; }
    public Pet? Sire { get; set; }
    public string? SireDescription { get; set; }
    public DateTime MatedOn { get; set; }
    public DateTime? ExpectedDueOn { get; set; }
    public DateTime? OutcomeOn { get; set; }
    public int? OffspringCount { get; set; }
    public string? Notes { get; set; }
}
