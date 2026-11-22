using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Pets;

// =====================================================================
// Preventive care plans
// =====================================================================

public enum CarePlanItemType { Vaccination = 0, Medication = 1, Appointment = 2, Grooming = 3, ParasitePrevention = 4, LivestockTreatment = 5 }

public enum CarePlanItemStatus { Pending = 0, Done = 1, Missed = 2, Rescheduled = 3 }

public enum CarePlanStatus { Active = 0, Completed = 1, Archived = 2 }

/// <summary>Vet-published care plan blueprint that owners can adopt for a pet.</summary>
public class CarePlanTemplate : AuditableEntity
{
    public string Title { get; set; } = default!;
    public AnimalType Species { get; set; }
    public string? Breed { get; set; }
    public string? Description { get; set; }
    public Guid PublishedByUserId { get; set; }
    public bool IsPublished { get; set; }
    public ICollection<CarePlanTemplateItem> Items { get; set; } = new List<CarePlanTemplateItem>();
}

public class CarePlanTemplateItem : BaseEntity
{
    public Guid TemplateId { get; set; }
    public CarePlanTemplate Template { get; set; } = default!;
    public CarePlanItemType Type { get; set; }
    public string Title { get; set; } = default!;
    /// <summary>Days after the plan's start date that the (first) occurrence is due.</summary>
    public int OffsetDays { get; set; }
    /// <summary>When set the item repeats every N days after the first occurrence.</summary>
    public int? RepeatEveryDays { get; set; }
    /// <summary>Total occurrences including the first (only with RepeatEveryDays). Null = 12 months' worth.</summary>
    public int? RepeatCount { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}

public class PetCarePlan : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public Guid? TemplateId { get; set; }
    public CarePlanTemplate? Template { get; set; }
    public bool IsCustom { get; set; }
    public string Title { get; set; } = default!;
    public CarePlanStatus Status { get; set; } = CarePlanStatus.Active;
    public DateTime StartDate { get; set; }
    public ICollection<PetCarePlanItem> Items { get; set; } = new List<PetCarePlanItem>();
}

public class PetCarePlanItem : AuditableEntity
{
    public Guid PlanId { get; set; }
    public PetCarePlan Plan { get; set; } = default!;
    public Guid PetId { get; set; }
    public CarePlanItemType Type { get; set; }
    public string Title { get; set; } = default!;
    public DateTime DueDate { get; set; }
    public DateTime? OriginalDueDate { get; set; }
    public CarePlanItemStatus Status { get; set; } = CarePlanItemStatus.Pending;
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? Notes { get; set; }
    /// <summary>Optional link to an existing record (VaccinationRecord, GroomingRecord, PetMedication, ...).</summary>
    public string? LinkedRecordType { get; set; }
    public Guid? LinkedRecordId { get; set; }
    public Guid? AssignedUserId { get; set; }
    /// <summary>Reminder stage already sent: 1 = due-soon, 2 = missed. Reset on reschedule.</summary>
    public int? LastNotifiedStage { get; set; }
}

// =====================================================================
// Poison / exposure reporting + emergency card
// =====================================================================

public enum ExposureStatus { Reported = 0, VetContacted = 1, UnderObservation = 2, Resolved = 3 }

public class ExposureReport : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public Guid ReportedByUserId { get; set; }
    public string Substance { get; set; } = default!;
    public string? AmountEstimate { get; set; }
    public DateTime? OnsetAt { get; set; }
    public DateTime ExposedAt { get; set; } = DateTime.UtcNow;
    public string? Symptoms { get; set; }
    public ExposureStatus Status { get; set; } = ExposureStatus.Reported;
    public bool Escalated { get; set; }
    public ICollection<ExposureReportEvent> Timeline { get; set; } = new List<ExposureReportEvent>();
}

public class ExposureReportEvent : BaseEntity
{
    public Guid ExposureReportId { get; set; }
    public ExposureReport ExposureReport { get; set; } = default!;
    public DateTime At { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string Kind { get; set; } = default!;
    public string? Note { get; set; }
}

/// <summary>Short-lived anonymous link to a minimal "emergency health card" (separate from full health share links).</summary>
public class PetEmergencyCardLink : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

// =====================================================================
// Typed allergies / diet
// =====================================================================

public enum AllergyCategory { Food = 0, Drug = 1, Environmental = 2, Other = 99 }

public enum AllergySeverity { Mild = 0, Moderate = 1, Severe = 2, LifeThreatening = 3 }

public class PetAllergy : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public string Allergen { get; set; } = default!;
    public AllergyCategory Category { get; set; } = AllergyCategory.Other;
    public AllergySeverity Severity { get; set; } = AllergySeverity.Mild;
    public string? Reaction { get; set; }
    public DateTime NotedAt { get; set; } = DateTime.UtcNow;
    public bool Active { get; set; } = true;
}

public class PetDietEntry : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public string Food { get; set; } = default!;
    public string? Amount { get; set; }
    public string? Frequency { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; } = true;
}

// =====================================================================
// Record versioning, sealing, certificates
// =====================================================================

public enum HealthRecordType { Vaccination = 0, Medical = 1 }

/// <summary>State of a record BEFORE a change. ChangedByUserId is whoever superseded that state.</summary>
public class HealthRecordVersion : BaseEntity
{
    public HealthRecordType RecordType { get; set; }
    public Guid RecordId { get; set; }
    public int Version { get; set; }
    public string SnapshotJson { get; set; } = default!;
    public Guid ChangedByUserId { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class HealthRecordSeal : BaseEntity
{
    public HealthRecordType RecordType { get; set; }
    public Guid RecordId { get; set; }
    public Guid PetId { get; set; }
    public Guid SealedByUserId { get; set; }
    public Guid? VetDoctorId { get; set; }
    public DateTime SealedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Canonical snapshot that was signed (so the hash can be re-verified later).</summary>
    public string SnapshotJson { get; set; } = default!;
    public string SignatureHash { get; set; } = default!;
}

/// <summary>Verifiable (token-addressed) vaccination certificate issued for a pet.</summary>
public class VaccinationCertificate : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public string Token { get; set; } = default!;
    public Guid IssuedByUserId { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    /// <summary>SHA-256 of the vaccination list at issue time (lets the verifier flag later changes).</summary>
    public string ContentHash { get; set; } = default!;
}

// =====================================================================
// Lab results
// =====================================================================

public enum LabResultFlag { Unknown = 0, Normal = 1, Low = 2, High = 3, Critical = 4 }

/// <summary>Entered = typed in by a person; OcrUnavailable = document uploaded, no OCR engine configured, awaiting manual transcription.</summary>
public enum LabResultStatus { Entered = 0, OcrUnavailable = 1, OcrExtracted = 2, Transcribed = 3 }

public class PetLabResult : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public string TestName { get; set; } = default!;
    public string? Value { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    public LabResultFlag Flag { get; set; }
    public DateTime TakenAt { get; set; }
    public string? DocumentUrl { get; set; }
    public string? OcrText { get; set; }
    public LabResultStatus Status { get; set; }
}

// =====================================================================
// Herd-level treatment with withdrawal periods
// =====================================================================

public class LivestockTreatment : AuditableEntity
{
    public Guid OwnerId { get; set; }
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public Guid? PetGroupId { get; set; }
    /// <summary>Shared by every row produced by one group treatment.</summary>
    public Guid BatchId { get; set; }
    public string Diagnosis { get; set; } = default!;
    public string Treatment { get; set; } = default!;
    public DateTime AdministeredOn { get; set; }
    public int MeatWithdrawalDays { get; set; }
    public int MilkWithdrawalDays { get; set; }
    public DateTime? MeatSafeOn { get; set; }
    public DateTime? MilkSafeOn { get; set; }
    public string? Notes { get; set; }
}
