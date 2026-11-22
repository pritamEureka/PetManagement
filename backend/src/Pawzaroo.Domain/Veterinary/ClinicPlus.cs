using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Veterinary;

// ---------- Emergency / 24-7 discovery ----------

/// <summary>Typed emergency availability for a doctor/clinic. One row per doctor.</summary>
public class DoctorEmergencyProfile : AuditableEntity
{
    public Guid DoctorId { get; set; }
    public bool Is24x7 { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? ClinicName { get; set; }
    public string? ClinicAddress { get; set; }
    public string? City { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool AcceptsWalkInEmergency { get; set; }
}

// ---------- Booking policy / deposits ----------

public class DoctorBookingPolicy : AuditableEntity
{
    public Guid DoctorId { get; set; }
    /// <summary>0-100. Ignored when <see cref="DepositFixed"/> is set.</summary>
    public decimal? DepositPercent { get; set; }
    public decimal? DepositFixed { get; set; }
    public int CancellationWindowHours { get; set; } = 24;
    public decimal LateCancelFeePercent { get; set; }
    public decimal NoShowFeePercent { get; set; }
    /// <summary>Rescheduling is only allowed at least this many hours before the visit (0 = always).</summary>
    public int AllowRescheduleHours { get; set; }
    public bool RequiresQuestionnaire { get; set; }
}

public enum DepositStatus { Pending = 0, Paid = 1, Refunded = 2, Forfeited = 3 }

public class AppointmentDeposit : AuditableEntity
{
    public Guid AppointmentId { get; set; }
    public decimal Amount { get; set; }
    public DepositStatus Status { get; set; } = DepositStatus.Pending;
    public Guid? PaymentId { get; set; }
    /// <summary>Cancellation / no-show fee charged (informational; may exceed what was forfeited).</summary>
    public decimal FeeAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public string? Note { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

// ---------- Follow-ups ----------

public enum FollowUpStatus { Requested = 0, Booked = 1, Dismissed = 2 }

public class AppointmentFollowUp : AuditableEntity
{
    public Guid SourceAppointmentId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid PatientUserId { get; set; }
    public Guid? PetId { get; set; }
    public int InDays { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Note { get; set; }
    public FollowUpStatus Status { get; set; } = FollowUpStatus.Requested;
    public Guid? FollowUpAppointmentId { get; set; }
}

// ---------- Shared calendars ----------

public enum CalendarShareScope { Patient = 0, Doctor = 1 }

public class CalendarShare : AuditableEntity
{
    public Guid OwnerUserId { get; set; }
    /// <summary>SHA-256 (hex) of the secret token. The token itself is shown once, at creation.</summary>
    public string TokenHash { get; set; } = default!;
    public string TokenHint { get; set; } = default!;
    public CalendarShareScope Scope { get; set; }
    public string? Label { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
}

// ---------- Pre-visit questionnaires ----------

public class QuestionnaireTemplate : AuditableEntity
{
    public Guid DoctorId { get; set; }
    public string Title { get; set; } = default!;
    /// <summary>JSON array: [{ id, type: text|yesno|choice|number, label, required, options? }]</summary>
    public string QuestionsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
}

public class AppointmentQuestionnaireResponse : AuditableEntity
{
    public Guid AppointmentId { get; set; }
    public Guid TemplateId { get; set; }
    public string AnswersJson { get; set; } = "{}";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Clinical record ----------

public enum EncounterStatus { Draft = 0, Finalized = 1, Amended = 2 }

public class ClinicalEncounter : AuditableEntity
{
    public Guid AppointmentId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid? PetId { get; set; }
    public EncounterStatus Status { get; set; } = EncounterStatus.Draft;
    public string? ChiefComplaint { get; set; }
    public string? Findings { get; set; }
    public string? Diagnosis { get; set; }
    public string? TreatmentPlan { get; set; }
    public int Version { get; set; } = 1;
    public DateTime? FinalizedAt { get; set; }
    public Guid? FinalizedByDoctorId { get; set; }
    public string? SignatureHash { get; set; }
    public ICollection<EncounterDiagnosisCode> DiagnosisCodes { get; set; } = new List<EncounterDiagnosisCode>();
    public ICollection<EncounterLabResult> LabResults { get; set; } = new List<EncounterLabResult>();
}

public class EncounterDiagnosisCode : AuditableEntity
{
    public Guid EncounterId { get; set; }
    public string Code { get; set; } = default!;
    /// <summary>VeNom | SNOMED | Other</summary>
    public string System { get; set; } = "VeNom";
    public string? Label { get; set; }
}

public class EncounterAmendment : AuditableEntity
{
    public Guid EncounterId { get; set; }
    public int Version { get; set; }
    public string SnapshotJson { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public DateTime AmendedAt { get; set; } = DateTime.UtcNow;
    public Guid AmendedByDoctorId { get; set; }
    public string SignatureHash { get; set; } = default!;
}

/// <summary>Side table marking a prescription as sealed (immutable) once its encounter is finalized.</summary>
public class PrescriptionSeal : AuditableEntity
{
    public Guid PrescriptionId { get; set; }
    public Guid EncounterId { get; set; }
    public DateTime SealedAt { get; set; } = DateTime.UtcNow;
    public string ContentHash { get; set; } = default!;
}

public class EncounterLabResult : AuditableEntity
{
    public Guid EncounterId { get; set; }
    public string Test { get; set; } = default!;
    public string? Value { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    /// <summary>Normal | Low | High | Critical</summary>
    public string? Flag { get; set; }
    public string? DocumentUrl { get; set; }
}

/// <summary>Audit trail for completing an appointment without a finalized encounter.</summary>
public class EncounterCompletionOverride : AuditableEntity
{
    public Guid AppointmentId { get; set; }
    public Guid DoctorId { get; set; }
    public string Reason { get; set; } = default!;
}

public enum ReferralStatus { Pending = 0, Accepted = 1, Declined = 2, Completed = 3, Cancelled = 4 }

public class Referral : AuditableEntity
{
    public Guid EncounterId { get; set; }
    public Guid FromDoctorId { get; set; }
    public Guid? ToDoctorId { get; set; }
    public string? ExternalClinic { get; set; }
    public string Reason { get; set; } = default!;
    public ReferralStatus Status { get; set; } = ReferralStatus.Pending;
    public bool ConsentToShareRecords { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public class RecordShareConsent : AuditableEntity
{
    public Guid PetId { get; set; }
    public Guid GrantedByUserId { get; set; }
    public Guid GranteeDoctorId { get; set; }
    /// <summary>Summary | FullRecord</summary>
    public string Scope { get; set; } = "FullRecord";
    public Guid? ReferralId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
