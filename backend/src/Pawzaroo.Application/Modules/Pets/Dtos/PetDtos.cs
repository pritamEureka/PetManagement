using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Application.Modules.Pets.Dtos;

/// <summary>How the current user relates to a pet. Ordered so "at least X" checks are numeric.</summary>
public enum PetAccessLevel { None = 0, Vet = 1, Viewer = 2, Caretaker = 3, CoOwner = 4, Owner = 5 }

// ---------- Pet profile ----------

public record UpsertPetInput(
    string Name, AnimalType AnimalType, string? Breed, Gender Gender, DateTime? BirthDate,
    decimal? WeightKg, string? Color, string? TagNumber, string? PrimaryPhotoUrl,
    string? Allergies, string? DietNotes, bool IsAvailableForAdoption,
    AnimalSize? Size = null, string? MicrochipNumber = null,
    Guid? SireId = null, Guid? DamId = null, Guid? GroupId = null);

public record PetSummaryDto(
    Guid Id, Guid OwnerId, string Name, AnimalType AnimalType, string? Breed, Gender Gender,
    DateTime? BirthDate, decimal? WeightKg, string? Color, string? TagNumber, string? PrimaryPhotoUrl,
    string? Allergies, string? DietNotes, bool IsAvailableForAdoption, AnimalSize? Size,
    string? MicrochipNumber, bool IsLost, Guid? GroupId, string? GroupName,
    PetAccessLevel MyAccess, int OverdueVaccinations, DateTime? NextVaccinationDue, DateTime CreatedAt);

public record PetPhotoDto(Guid Id, string Url, string? Caption, DateTime UploadedAt);

public record VaccinationDto(Guid Id, string VaccineName, DateTime AdministeredOn, DateTime? NextDueOn,
    string? AdministeredByVet, string? Notes, string? CertificateUrl);

public record MedicalRecordDto(Guid Id, Guid? AppointmentId, DateTime VisitDate, string? Diagnosis, string? Treatment,
    string? PrescriptionFileUrl, string? Notes, string? VetName, string? AttachmentUrl);

public record GroomingRecordDto(Guid Id, DateTime PerformedOn, string ServiceType, string? Provider, string? Notes,
    decimal? Cost, Guid? ServiceBookingId);

public record PetRefDto(Guid Id, string Name, string? Breed, string? PrimaryPhotoUrl);

public record PetDetailDto(
    Guid Id, Guid OwnerId, string OwnerName, string Name, AnimalType AnimalType, string? Breed, Gender Gender,
    DateTime? BirthDate, decimal? WeightKg, string? Color, string? TagNumber, string? PrimaryPhotoUrl,
    string? Allergies, string? DietNotes, bool IsAvailableForAdoption, AnimalSize? Size,
    string? MicrochipNumber, string? PublicCode, bool PublicProfileEnabled, bool IsLost,
    PetRefDto? Sire, PetRefDto? Dam, Guid? GroupId, string? GroupName,
    PetAccessLevel MyAccess, DateTime CreatedAt,
    IReadOnlyList<PetPhotoDto> Photos,
    IReadOnlyList<VaccinationDto> Vaccinations,
    IReadOnlyList<MedicalRecordDto> MedicalRecords,
    IReadOnlyList<GroomingRecordDto> GroomingRecords);

// ---------- Health records ----------

public record VaccinationInput(string VaccineName, DateTime AdministeredOn, DateTime? NextDueOn,
    string? AdministeredByVet, string? Notes, string? CertificateUrl);

public record MedicalRecordInput(DateTime VisitDate, string? Diagnosis, string? Treatment, string? Notes,
    string? VetName, string? AttachmentUrl, string? PrescriptionFileUrl);

public record GroomingRecordInput(DateTime PerformedOn, string ServiceType, string? Provider, string? Notes, decimal? Cost);

public record PetPhotoInput(string Url, string? Caption, bool MakePrimary = false);

public record VaccinePlanItem(string VaccineName, int AgeWeeks, string? Notes);

public record VaccineScheduleItemDto(string VaccineName, int AgeWeeks, DateTime? DueOn, string Status, DateTime? GivenOn, string? Notes);   // Status: "given" | "overdue" | "due" | "upcoming" | "unknown"

// ---------- Weight ----------

public record WeightLogInput(DateTime RecordedOn, decimal WeightKg, string? Notes);
public record WeightLogDto(Guid Id, DateTime RecordedOn, decimal WeightKg, string? Notes);
public record WeightRangeDto(decimal MinKg, decimal MaxKg, string Basis);
public record WeightHistoryDto(IReadOnlyList<WeightLogDto> Entries, WeightRangeDto? HealthyRange);

// ---------- Medications ----------

public record MedicationInput(string DrugName, string? Dose, string DoseTimes, DateTime StartDate,
    DateTime? EndDate, string? Instructions, bool IsActive = true);

public record MedicationDto(Guid Id, Guid PetId, Guid? PrescriptionId, string DrugName, string? Dose, string DoseTimes,
    DateTime StartDate, DateTime? EndDate, string? Instructions, bool IsActive, int DosesTaken, int DosesSkipped);

public record DoseLogInput(DateTime ScheduledFor, bool Skipped, string? Notes);

public record TodayDoseDto(Guid MedicationId, Guid PetId, string PetName, string DrugName, string? Dose,
    DateTime ScheduledFor, string Status);   // Status: "taken" | "skipped" | "due" | "upcoming"

// ---------- Caretakers ----------

public record InviteCaretakerInput(string Email, CaretakerRole Role, DateTime? ExpiresAt);
public record UpdateCaretakerInput(CaretakerRole Role, DateTime? ExpiresAt);
public record CaretakerDto(Guid Id, Guid UserId, string DisplayName, string Email, string? AvatarUrl,
    CaretakerRole Role, DateTime? ExpiresAt, DateTime CreatedAt);

// ---------- Daily care ----------

public record CareRoutineInput(CareActivityType ActivityType, string Title, TimeOnly? TimeOfDay, string? Details, bool IsActive = true);
public record CareRoutineDto(Guid Id, CareActivityType ActivityType, string Title, TimeOnly? TimeOfDay, string? Details, bool IsActive);
public record CareLogInput(CareActivityType ActivityType, Guid? RoutineId, DateTime? PerformedAt, string? Notes);
public record CareLogDto(Guid Id, CareActivityType ActivityType, Guid? RoutineId, DateTime PerformedAt,
    Guid PerformedByUserId, string PerformedByName, string? Notes);
public record CareChecklistItemDto(CareRoutineDto Routine, bool Done, DateTime? DoneAt, string? DoneBy);
public record CareTodayDto(IReadOnlyList<CareChecklistItemDto> Checklist, int StreakDays, IReadOnlyList<CareLogDto> RecentLogs);

// ---------- Expenses & insurance ----------

public record ExpenseInput(PetExpenseCategory Category, decimal Amount, DateTime SpentOn, string? Description);
public record ExpenseDto(Guid? Id, Guid PetId, string PetName, PetExpenseCategory Category, decimal Amount,
    DateTime SpentOn, string? Description, string Source);   // Source: "manual" | "appointment" | "service" | "grooming"
public record ExpenseMonthDto(string Month, decimal Total, IReadOnlyDictionary<string, decimal> ByCategory);
public record ExpenseSummaryDto(decimal Total, IReadOnlyList<ExpenseMonthDto> Months,
    IReadOnlyDictionary<string, decimal> ByCategory, IReadOnlyDictionary<string, decimal> ByPet,
    IReadOnlyList<ExpenseDto> Recent);

public record InsurancePolicyInput(string Provider, string PolicyNumber, string? CoverageSummary, decimal? CoverageLimit,
    decimal? Premium, DateTime StartDate, DateTime? EndDate, string? DocumentUrl);
public record InsurancePolicyDto(Guid Id, string Provider, string PolicyNumber, string? CoverageSummary,
    decimal? CoverageLimit, decimal? Premium, DateTime StartDate, DateTime? EndDate, string? DocumentUrl, bool IsActive);

// ---------- Sharing / export / public profile ----------

public record CreateShareLinkInput(int ValidDays, string? Label);
public record ShareLinkDto(Guid Id, string Token, string? Label, DateTime ExpiresAt, DateTime? RevokedAt, DateTime CreatedAt);

public record HealthReportDto(
    PetDetailDto Pet, IReadOnlyList<WeightLogDto> Weights, IReadOnlyList<MedicationDto> Medications,
    DateTime GeneratedAt, DateTime? ExpiresAt);

public record PublicProfileInput(bool Enabled);
public record PublicProfileStateDto(bool Enabled, string? Code);

public record PublicPetDto(string Code, string Name, AnimalType AnimalType, string? Breed, Gender Gender, string? Color,
    string? PrimaryPhotoUrl, string? Allergies, bool IsLost, string OwnerFirstName, Guid? OpenLostReportId);

public record SightingInput(string? Message, string? ContactInfo, double? Latitude, double? Longitude);

// ---------- Lineage / groups / livestock ----------

public record LineageNodeDto(Guid Id, string Name, string? Breed, Gender Gender, DateTime? BirthDate,
    LineageNodeDto? Sire, LineageNodeDto? Dam);
public record LineageDto(LineageNodeDto Root, IReadOnlyList<PetRefDto> Offspring);

public record PetGroupInput(string Name, AnimalType AnimalType, string? Purpose, string? Notes);
public record PetGroupDto(Guid Id, string Name, AnimalType AnimalType, string? Purpose, string? Notes,
    int MemberCount, IReadOnlyList<PetRefDto> Members);
public record GroupMembersInput(IReadOnlyList<Guid> PetIds);

public record ProductionLogInput(Guid? PetGroupId, Guid? PetId, ProductionType Type, decimal Quantity, string Unit,
    DateTime RecordedOn, string? Notes);
public record ProductionLogDto(Guid Id, Guid? PetGroupId, string? GroupName, Guid? PetId, string? PetName,
    ProductionType Type, decimal Quantity, string Unit, DateTime RecordedOn, string? Notes);

public record BreedingRecordInput(Guid DamId, Guid? SireId, string? SireDescription, DateTime MatedOn,
    DateTime? ExpectedDueOn, DateTime? OutcomeOn, int? OffspringCount, string? Notes);
public record BreedingRecordDto(Guid Id, Guid DamId, string DamName, Guid? SireId, string? SireName,
    string? SireDescription, DateTime MatedOn, DateTime? ExpectedDueOn, DateTime? OutcomeOn,
    int? OffspringCount, string? Notes);

// ---------- Dashboard / recommendations ----------

public record UpcomingCareItemDto(string Kind, Guid PetId, string PetName, string Title, DateTime DueOn,
    bool Overdue, string Url);    // Kind: "vaccination" | "medication" | "birthday" | "breeding"

public record RecommendedProductDto(Guid Id, string Name, decimal Price, decimal? DiscountPrice, string? ImageUrl, string Reason);
public record RecommendationsDto(string LifeStage, IReadOnlyList<string> CareTips,
    IReadOnlyList<VaccinePlanItem> SuggestedVaccines, IReadOnlyList<RecommendedProductDto> Products);
