using Pawzaroo.Domain.Common;

namespace Pawzaroo.Application.Modules.Vet.Dtos;

public record SpecialtyDto(Guid Id, string Slug, string Name);

public record DoctorSummaryDto(
    Guid Id,
    Guid UserId,
    string Name,
    string? AvatarUrl,
    string? PrimarySpecialty,
    IReadOnlyList<string> Specialties,
    string? ClinicName,
    string? City,
    string? Country,
    decimal ConsultationFee,
    ConsultationType ConsultationType,
    bool OnlineAvailable,
    bool OfflineAvailable,
    decimal RatingAverage,
    int RatingCount,
    ApprovalStatus ApprovalStatus,
    IReadOnlyList<AnimalType> SupportedAnimalTypes);

public record DoctorDetailDto(
    Guid Id,
    Guid UserId,
    string Name,
    string? AvatarUrl,
    string LicenseNumber,
    string? Specialty,
    int? ExperienceYears,
    string? About,
    string? ClinicName,
    string? ClinicAddress,
    string? City,
    string? Country,
    decimal ConsultationFee,
    ConsultationType ConsultationType,
    bool OnlineAvailable,
    bool OfflineAvailable,
    bool AutoConfirmAppointments,
    int DefaultSlotMinutes,
    int CancellationCutoffHours,
    ApprovalStatus ApprovalStatus,
    string? AdminNotes,
    decimal RatingAverage,
    int RatingCount,
    IReadOnlyList<AnimalType> SupportedAnimalTypes,
    IReadOnlyList<SpecialtyDto> Specialties);

public record AvailabilityRuleDto(Guid Id, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime,
                                  int SlotMinutes, ConsultationType ConsultationType);
public record HolidayDto(Guid Id, DateOnly Date, string? Reason);
public record TimeSlotDto(Guid Id, DateTime StartUtc, DateTime EndUtc, ConsultationType ConsultationType, string Status);

public record CredentialDocumentDto(
    Guid Id, int Kind, string Title, string FileUrl,
    string? IssuingAuthority, string? DocumentNumber,
    DateOnly? IssuedOn, DateOnly? ExpiresOn,
    bool Verified, DateTime? VerifiedAt);

public record AppointmentSummaryDto(
    Guid Id,
    Guid DoctorId,
    string DoctorName,
    Guid PatientUserId,
    string PatientName,
    Guid? PetId,
    string? PetName,
    DateTime ScheduledAt,
    int DurationMinutes,
    ConsultationType Type,
    AppointmentStatus Status,
    PaymentStatus PaymentStatus,
    decimal Amount,
    string? MeetingLink,
    string? PrescriptionFileUrl,
    DateTime CreatedAt);

public record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);

// ---------- Inputs ----------

public record RegisterDoctorInput(
    string LicenseNumber,
    string? Specialty,
    int? ExperienceYears,
    string? About,
    string? ClinicName,
    string? ClinicAddress,
    string? City,
    string? Country,
    decimal ConsultationFee,
    ConsultationType ConsultationType,
    bool OnlineAvailable,
    bool OfflineAvailable,
    bool AutoConfirmAppointments,
    int DefaultSlotMinutes,
    int CancellationCutoffHours,
    IReadOnlyList<AnimalType> SupportedAnimalTypes,
    IReadOnlyList<Guid> SpecialtyIds);

public record UpdateDoctorInput(
    string? Specialty,
    int? ExperienceYears,
    string? About,
    string? ClinicName,
    string? ClinicAddress,
    string? City,
    string? Country,
    decimal ConsultationFee,
    ConsultationType ConsultationType,
    bool OnlineAvailable,
    bool OfflineAvailable,
    bool AutoConfirmAppointments,
    int DefaultSlotMinutes,
    int CancellationCutoffHours,
    IReadOnlyList<AnimalType> SupportedAnimalTypes,
    IReadOnlyList<Guid> SpecialtyIds);

public record AddCredentialInput(int Kind, string Title, string FileUrl, string? IssuingAuthority,
                                 string? DocumentNumber, DateOnly? IssuedOn, DateOnly? ExpiresOn);

public record AvailabilityRuleInput(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime,
                                    int SlotMinutes, ConsultationType ConsultationType);
public record HolidayInput(DateOnly Date, string? Reason);

public record GenerateSlotsInput(DateOnly FromDate, DateOnly ToDate);

public record BookAppointmentInput(
    Guid DoctorId,
    Guid TimeSlotId,
    Guid? PetId,
    ConsultationType Type,
    string? Symptoms);

public record RescheduleInput(Guid NewTimeSlotId);
public record CancelInput(string? Reason);
public record DecisionInput(string? AdminNotes);
public record PrescriptionInput(string FileUrl, string? Notes, string? ItemsJson, DateTime? ValidUntil);
public record FollowUpInput(string Notes);
public record ReviewInput(int Rating, string? Comment);

public record DoctorSearchInput(
    string? Cursor,
    int PageSize = 20,
    AnimalType? AnimalType = null,
    string? Specialty = null,
    Guid? SpecialtyId = null,
    string? City = null,
    ConsultationType? ConsultationType = null,
    decimal? MaxPrice = null,
    decimal? MinRating = null,
    bool? AvailableThisWeek = null,
    string? Sort = null);

public record CommissionReportRow(Guid DoctorId, string DoctorName, decimal GrossFees, decimal PlatformShare, int AppointmentCount);
