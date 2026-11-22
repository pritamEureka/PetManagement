using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Veterinary;
using AppointmentEntity = Pawzaroo.Domain.Veterinary.Appointment;

namespace Pawzaroo.Application.Modules.ClinicPlus;

// ---------- Emergency ----------

public record EmergencyProfileDto(
    bool Is24x7, string? EmergencyPhone, string? ClinicName, string? ClinicAddress, string? City,
    double? Latitude, double? Longitude, bool AcceptsWalkInEmergency, DateTime? UpdatedAt);

public record UpsertEmergencyProfileInput(
    bool Is24x7, string? EmergencyPhone, string? ClinicName, string? ClinicAddress, string? City,
    double? Latitude, double? Longitude, bool AcceptsWalkInEmergency);

public record EmergencyVetDto(
    Guid DoctorId, string DoctorName, string? ClinicName, string? ClinicAddress, string? City,
    bool Is24x7, bool AcceptsWalkInEmergency, string? EmergencyPhone, string? TelUri,
    double? DistanceKm, bool CityMatch, bool IsOnline);

public interface IEmergencyVetService
{
    Task<EmergencyProfileDto?> GetMineAsync(CancellationToken ct = default);
    Task<EmergencyProfileDto> UpsertMineAsync(UpsertEmergencyProfileInput input, CancellationToken ct = default);
    Task<IReadOnlyList<EmergencyVetDto>> SearchAsync(double? lat, double? lng, string? city, AnimalType? animalType,
        int limit = 30, CancellationToken ct = default);
}

// ---------- Booking policy / deposits ----------

public record BookingPolicyDto(
    decimal? DepositPercent, decimal? DepositFixed, int CancellationWindowHours, decimal LateCancelFeePercent,
    decimal NoShowFeePercent, int AllowRescheduleHours, bool RequiresQuestionnaire);

public record DepositDto(
    Guid AppointmentId, decimal Amount, DepositStatus Status, decimal FeeAmount, decimal RefundAmount, string? Note, DateTime? ResolvedAt);

public record CancellationQuoteDto(
    Guid AppointmentId, bool IsLate, int WindowHours, decimal FeePercent, decimal Fee, decimal Forfeited, decimal Refund,
    decimal Outstanding, DateTime ScheduledAt);

public record CancellationOutcomeDto(
    Guid AppointmentId, bool IsLate, decimal Fee, decimal Forfeited, decimal Refund, decimal Outstanding, DepositStatus? DepositStatus);

public interface IBookingPolicyService
{
    Task<BookingPolicyDto?> GetMineAsync(CancellationToken ct = default);
    Task<BookingPolicyDto> UpsertMineAsync(BookingPolicyDto input, CancellationToken ct = default);
    Task<BookingPolicyDto?> GetForDoctorAsync(Guid doctorId, CancellationToken ct = default);

    /// <summary>Adds (does not save) a pending deposit for a freshly created appointment, when the doctor has a policy.</summary>
    Task ApplyOnBookingAsync(AppointmentEntity appt, CancellationToken ct = default);
    /// <summary>Marks the deposit paid. Called from the existing pay flow. Does not save.</summary>
    Task OnPaidAsync(AppointmentEntity appt, CancellationToken ct = default);
    /// <summary>Resolves the deposit for a cancellation (fee / refund). Does not save.</summary>
    Task<CancellationOutcomeDto> ApplyCancellationAsync(AppointmentEntity appt, bool byDoctor, bool waivePenalty, CancellationToken ct = default);
    Task ApplyNoShowAsync(AppointmentEntity appt, CancellationToken ct = default);
    /// <summary>Reject a reschedule inside the doctor's "allow reschedule" window (patient only).</summary>
    Task EnsureRescheduleAllowedAsync(AppointmentEntity appt, CancellationToken ct = default);

    Task<DepositDto?> GetDepositAsync(Guid appointmentId, CancellationToken ct = default);
    Task<CancellationQuoteDto> QuoteCancellationAsync(Guid appointmentId, CancellationToken ct = default);
    Task<CancellationOutcomeDto?> GetCancellationOutcomeAsync(Guid appointmentId, CancellationToken ct = default);
}

// ---------- Follow-ups ----------

public record RequestFollowUpInput(int InDays, string? Note);
public record FollowUpDto(
    Guid Id, Guid SourceAppointmentId, Guid DoctorId, string DoctorName, Guid? PetId, string? PetName,
    int InDays, DateOnly DueDate, string? Note, FollowUpStatus Status, Guid? FollowUpAppointmentId);
public record BookFollowUpInput(Guid TimeSlotId, string? Symptoms);

public interface IFollowUpService
{
    Task<FollowUpDto> RequestAsync(Guid sourceAppointmentId, RequestFollowUpInput input, CancellationToken ct = default);
    Task<IReadOnlyList<FollowUpDto>> MineAsync(CancellationToken ct = default);
    /// <summary>Doctor/pet pre-fill for the booking screen (status quo data from a completed appointment).</summary>
    Task<FollowUpDto> PrefillAsync(Guid sourceAppointmentId, CancellationToken ct = default);
    Task<Pawzaroo.Application.Modules.Vet.Dtos.AppointmentSummaryDto> BookAsync(Guid sourceAppointmentId, BookFollowUpInput input, CancellationToken ct = default);
    Task DismissAsync(Guid followUpId, CancellationToken ct = default);
}

// ---------- Doctor switching ----------

public record SwitchCandidateDto(Guid DoctorId, string DoctorName, string? Specialty, decimal Fee, decimal RatingAverage,
    Guid TimeSlotId, DateTime StartUtc, DateTime EndUtc);
public record SwitchDoctorInput(Guid NewDoctorId, Guid NewTimeSlotId);

public interface IDoctorSwitchService
{
    Task<IReadOnlyList<SwitchCandidateDto>> CandidatesAsync(Guid appointmentId, CancellationToken ct = default);
    Task<Pawzaroo.Application.Modules.Vet.Dtos.AppointmentSummaryDto> SwitchAsync(Guid appointmentId, SwitchDoctorInput input, CancellationToken ct = default);
}

// ---------- Shared calendars ----------

public record CreateCalendarShareInput(CalendarShareScope Scope, string? Label, int? ExpiresInDays);
public record CalendarShareDto(Guid Id, CalendarShareScope Scope, string? Label, string TokenHint, DateTime CreatedAt,
    DateTime? ExpiresAt, DateTime? RevokedAt, DateTime? LastAccessedAt, string? FeedPath);

public interface ICalendarShareService
{
    Task<CalendarShareDto> CreateAsync(CreateCalendarShareInput input, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarShareDto>> ListAsync(CancellationToken ct = default);
    Task RevokeAsync(Guid id, CancellationToken ct = default);
    /// <summary>Anonymous. Returns null when the token is unknown, revoked or expired.</summary>
    Task<string?> FeedAsync(string token, CancellationToken ct = default);
}

// ---------- Questionnaires ----------

public record QuestionDto(string Id, string Type, string Label, bool Required, IReadOnlyList<string>? Options);
public record QuestionnaireTemplateDto(Guid Id, string Title, IReadOnlyList<QuestionDto> Questions, bool IsActive);
public record UpsertQuestionnaireInput(string Title, IReadOnlyList<QuestionDto> Questions, bool IsActive);
public record AppointmentQuestionnaireDto(Guid AppointmentId, QuestionnaireTemplateDto? Template,
    IReadOnlyDictionary<string, string>? Answers, DateTime? SubmittedAt, bool Required);
public record SubmitQuestionnaireInput(IReadOnlyDictionary<string, string> Answers);

public interface IQuestionnaireService
{
    Task<QuestionnaireTemplateDto?> GetMyTemplateAsync(CancellationToken ct = default);
    Task<QuestionnaireTemplateDto> UpsertMyTemplateAsync(UpsertQuestionnaireInput input, CancellationToken ct = default);
    Task<AppointmentQuestionnaireDto> GetForAppointmentAsync(Guid appointmentId, CancellationToken ct = default);
    Task<AppointmentQuestionnaireDto> SubmitAsync(Guid appointmentId, SubmitQuestionnaireInput input, CancellationToken ct = default);
    /// <summary>Throws when the doctor requires the questionnaire and the patient has not submitted it. Used at check-in.</summary>
    Task EnsureCompletedIfRequiredAsync(Guid appointmentId, CancellationToken ct = default);
}

// ---------- Clinical encounter ----------

public record DiagnosisCodeDto(string Code, string System, string? Label);
public record LabResultDto(Guid? Id, string Test, string? Value, string? Unit, string? ReferenceRange, string? Flag, string? DocumentUrl);
public record EncounterAmendmentDto(int Version, string Reason, DateTime AmendedAt, string SignatureHash);
public record ReferralDto(Guid Id, Guid EncounterId, Guid? ToDoctorId, string? ToDoctorName, string? ExternalClinic, string Reason,
    ReferralStatus Status, bool ConsentToShareRecords, DateTime CreatedAt, string? PetName, Guid? PetId, string? FromDoctorName);
public record EncounterDto(
    Guid? Id, Guid AppointmentId, Guid? PetId, EncounterStatus Status, string? ChiefComplaint, string? Findings,
    string? Diagnosis, string? TreatmentPlan, IReadOnlyList<DiagnosisCodeDto> DiagnosisCodes, IReadOnlyList<LabResultDto> LabResults,
    int Version, DateTime? FinalizedAt, string? SignatureHash, IReadOnlyList<EncounterAmendmentDto> Amendments,
    IReadOnlyList<ReferralDto> Referrals, bool IsDoctor, DateTime? UpdatedAt);

public record SaveEncounterDraftInput(
    string? ChiefComplaint, string? Findings, string? Diagnosis, string? TreatmentPlan,
    IReadOnlyList<DiagnosisCodeDto>? DiagnosisCodes, IReadOnlyList<LabResultDto>? LabResults);
public record AmendEncounterInput(string Reason, SaveEncounterDraftInput Content);

public record CreateReferralInput(Guid? ToDoctorId, string? ExternalClinic, string Reason, bool ConsentToShareRecords);
public record RespondReferralInput(bool Accept);
public record GrantRecordConsentInput(Guid PetId, Guid GranteeDoctorId, string? Scope, int ExpiresInDays);
public record RecordConsentDto(Guid Id, Guid PetId, Guid GranteeDoctorId, string GranteeName, string Scope, DateTime ExpiresAt, DateTime? RevokedAt);

public interface IClinicalEncounterService
{
    Task<EncounterDto> GetAsync(Guid appointmentId, CancellationToken ct = default);
    Task<EncounterDto> SaveDraftAsync(Guid appointmentId, SaveEncounterDraftInput input, CancellationToken ct = default);
    Task<EncounterDto> FinalizeAsync(Guid appointmentId, CancellationToken ct = default);
    Task<EncounterDto> AmendAsync(Guid appointmentId, AmendEncounterInput input, CancellationToken ct = default);

    Task<ReferralDto> CreateReferralAsync(Guid appointmentId, CreateReferralInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ReferralDto>> IncomingReferralsAsync(CancellationToken ct = default);
    Task<ReferralDto> RespondReferralAsync(Guid referralId, RespondReferralInput input, CancellationToken ct = default);

    Task<RecordConsentDto> GrantConsentAsync(GrantRecordConsentInput input, CancellationToken ct = default);
    Task<IReadOnlyList<RecordConsentDto>> ListConsentsAsync(Guid petId, CancellationToken ct = default);
    Task RevokeConsentAsync(Guid consentId, CancellationToken ct = default);
    /// <summary>True when the doctor holds an active, unrevoked consent to read the pet's records.</summary>
    Task<bool> HasActiveConsentAsync(Guid petId, Guid doctorId, CancellationToken ct = default);
}

/// <summary>Hooks consumed by the appointment/prescription services.</summary>
public interface IClinicalGate
{
    /// <summary>Throws code "clinical_incomplete" when completion must be blocked. Records an audited override when allowed through.</summary>
    Task EnsureCanCompleteAsync(AppointmentEntity appt, string? overrideReason, CancellationToken ct = default);
    /// <summary>Throws when the appointment's encounter is finalized (prescriptions are sealed).</summary>
    Task EnsurePrescriptionEditableAsync(Guid appointmentId, CancellationToken ct = default);
}

public record CompleteInput(string? OverrideReason);

// ---------- Analytics ----------

public record AnalyticsPoint(string Period, decimal Revenue, int Appointments, int Completed, int NoShows, int Cancelled);
public record ClinicAnalyticsDto(
    DateOnly From, DateOnly To, string Granularity,
    decimal TotalRevenue, int TotalAppointments, int Completed, int NoShows, int Cancelled,
    double NoShowRate, double CapacityUtilization, int SlotsOffered, int SlotsBooked,
    double Return90Rate, int Return90Patients, double Return180Rate, int Return180Patients, int UniquePatients,
    IReadOnlyList<AnalyticsPoint> Series, IReadOnlyList<ClinicDoctorRow>? ClinicDoctors);
public record ClinicDoctorRow(Guid DoctorId, string DoctorName, decimal Revenue, int Appointments, double NoShowRate);

public interface IClinicAnalyticsService
{
    Task<ClinicAnalyticsDto> MineAsync(DateOnly? from, DateOnly? to, string? granularity, CancellationToken ct = default);
}
