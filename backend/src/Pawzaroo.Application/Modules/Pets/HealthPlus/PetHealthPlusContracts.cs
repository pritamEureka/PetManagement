using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Application.Modules.Pets.HealthPlus;

// =====================================================================
// Care plans
// =====================================================================

public record CarePlanTemplateItemInput(CarePlanItemType Type, string Title, int OffsetDays, int? RepeatEveryDays, int? RepeatCount, string? Notes);
public record CarePlanTemplateInput(string Title, AnimalType Species, string? Breed, string? Description, bool IsPublished,
    IReadOnlyList<CarePlanTemplateItemInput> Items);
public record CarePlanTemplateItemDto(Guid Id, CarePlanItemType Type, string Title, int OffsetDays, int? RepeatEveryDays, int? RepeatCount, string? Notes);
public record CarePlanTemplateDto(Guid Id, string Title, AnimalType Species, string? Breed, string? Description, bool IsPublished,
    Guid PublishedByUserId, bool IsMine, DateTime CreatedAt, IReadOnlyList<CarePlanTemplateItemDto> Items);

public record CustomPlanItemInput(CarePlanItemType Type, string Title, DateTime DueDate, string? Notes, Guid? AssignedUserId);
public record CustomPlanInput(string Title, DateTime? StartDate, IReadOnlyList<CustomPlanItemInput> Items);
public record AdoptTemplateInput(Guid TemplateId, DateTime? StartDate, Guid? AssignedUserId);
public record CompleteCareItemInput(string? Notes, bool CreateRecord, Guid? LinkedRecordId, string? LinkedRecordType);
public record RescheduleCareItemInput(DateTime NewDueDate, string? Reason);
public record AssignCareItemInput(Guid? UserId);

public record CarePlanItemDto(Guid Id, Guid PlanId, CarePlanItemType Type, string Title, DateTime DueDate, DateTime? OriginalDueDate,
    CarePlanItemStatus Status, bool IsMissed, DateTime? CompletedAt, string? Notes, string? LinkedRecordType, Guid? LinkedRecordId,
    Guid? AssignedUserId, string? AssignedUserName);
public record CarePlanDto(Guid Id, Guid PetId, Guid? TemplateId, bool IsCustom, string Title, CarePlanStatus Status, DateTime StartDate,
    int Pending, int Done, int Missed, IReadOnlyList<CarePlanItemDto> Items);
public record MissedCareSummaryDto(int MissedCount, int DueSoonCount, IReadOnlyList<CarePlanItemDto> Missed, IReadOnlyList<CarePlanItemDto> DueSoon);

public interface ICarePlanService
{
    Task<IReadOnlyList<CarePlanTemplateDto>> ListTemplatesAsync(AnimalType? species, bool mineOnly, CancellationToken ct = default);
    Task<CarePlanTemplateDto> SaveTemplateAsync(Guid? id, CarePlanTemplateInput input, CancellationToken ct = default);
    Task SetTemplatePublishedAsync(Guid id, bool published, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CarePlanDto>> ListPlansAsync(Guid petId, CancellationToken ct = default);
    Task<CarePlanDto> CreateCustomPlanAsync(Guid petId, CustomPlanInput input, CancellationToken ct = default);
    Task<CarePlanDto> AdoptTemplateAsync(Guid petId, AdoptTemplateInput input, CancellationToken ct = default);
    Task<CarePlanItemDto> AddItemAsync(Guid petId, Guid planId, CustomPlanItemInput input, CancellationToken ct = default);
    Task<CarePlanItemDto> CompleteItemAsync(Guid petId, Guid itemId, CompleteCareItemInput input, CancellationToken ct = default);
    Task<CarePlanItemDto> RescheduleItemAsync(Guid petId, Guid itemId, RescheduleCareItemInput input, CancellationToken ct = default);
    Task<CarePlanItemDto> AssignItemAsync(Guid petId, Guid itemId, AssignCareItemInput input, CancellationToken ct = default);
    Task ArchivePlanAsync(Guid petId, Guid planId, CancellationToken ct = default);
    Task<MissedCareSummaryDto> MissedSummaryAsync(Guid petId, CancellationToken ct = default);
}

// =====================================================================
// Exposure / poison + emergency card
// =====================================================================

public record ExposureReportInput(string Substance, string? AmountEstimate, DateTime? OnsetAt, DateTime? ExposedAt, string? Symptoms);
public record ExposureUpdateInput(ExposureStatus? Status, string? Note);
public record ExposureEventDto(DateTime At, string Kind, string? Note);
public record ExposureReportDto(Guid Id, Guid PetId, string PetName, string Substance, string? AmountEstimate, DateTime? OnsetAt, DateTime ExposedAt,
    string? Symptoms, ExposureStatus Status, bool Escalated, DateTime CreatedAt, IReadOnlyList<ExposureEventDto> Timeline);

public record EscalationDto(
    bool Emergency, string Headline, string Message, string CallNowText, string EmergencyVetsLink,
    string GenericGuidance, string Disclaimer, IReadOnlyList<string> DoNow, IReadOnlyList<string> DoNot, ExposureReportDto Report);

public record CommonSubstanceDto(string Key, string Label, string Note);

public record EmergencyCardDto(
    string PetName, string Species, string? Breed, decimal? WeightKg, string? BirthDate, string? MicrochipNumber,
    IReadOnlyList<string> Allergies, IReadOnlyList<string> ActiveMedications, IReadOnlyList<string> Conditions,
    string? VetName, string? VetClinic, string? VetPhone, string? OwnerName, string? OwnerPhone, DateTime GeneratedAt, DateTime? ExpiresAt);
public record EmergencyCardLinkDto(Guid Id, string Token, string ViewPath, DateTime ExpiresAt);

public interface IExposureService
{
    IReadOnlyList<CommonSubstanceDto> CommonSubstances();
    Task<EscalationDto> ReportAsync(Guid petId, ExposureReportInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ExposureReportDto>> ListAsync(Guid petId, CancellationToken ct = default);
    Task<ExposureReportDto> UpdateAsync(Guid petId, Guid reportId, ExposureUpdateInput input, CancellationToken ct = default);

    Task<EmergencyCardDto> GetEmergencyCardAsync(Guid petId, CancellationToken ct = default);
    Task<EmergencyCardLinkDto> CreateEmergencyCardLinkAsync(Guid petId, int validHours, CancellationToken ct = default);
    Task RevokeEmergencyCardLinksAsync(Guid petId, CancellationToken ct = default);
    Task<EmergencyCardDto?> GetPublicEmergencyCardAsync(string token, CancellationToken ct = default);
}

// =====================================================================
// Allergies, diet, labs
// =====================================================================

public record AllergyInput(string Allergen, AllergyCategory Category, AllergySeverity Severity, string? Reaction, DateTime? NotedAt, bool Active);
public record AllergyDto(Guid Id, Guid PetId, string? PetName, string Allergen, AllergyCategory Category, AllergySeverity Severity,
    string? Reaction, DateTime NotedAt, bool Active);
public record DietEntryInput(string Food, string? Amount, string? Frequency, string? Notes, bool Active);
public record DietEntryDto(Guid Id, Guid PetId, string Food, string? Amount, string? Frequency, string? Notes, bool Active);
public record ImportFromTextResult(int AllergiesCreated, int DietEntriesCreated, int Skipped, IReadOnlyList<AllergyDto> Allergies, IReadOnlyList<DietEntryDto> Diet);

public record LabResultInput(string TestName, string? Value, string? Unit, string? ReferenceRange, LabResultFlag? Flag, DateTime TakenAt, string? DocumentUrl);
public record LabResultDto(Guid Id, Guid PetId, string TestName, string? Value, string? Unit, string? ReferenceRange, LabResultFlag Flag,
    DateTime TakenAt, string? DocumentUrl, string? OcrText, LabResultStatus Status, bool NeedsTranscription, string? StatusNote);

public interface IPetHealthRecordsService
{
    Task<IReadOnlyList<AllergyDto>> ListAllergiesAsync(Guid petId, string? q, bool? active, CancellationToken ct = default);
    Task<IReadOnlyList<AllergyDto>> SearchMyAllergiesAsync(string q, CancellationToken ct = default);
    Task<AllergyDto> AddAllergyAsync(Guid petId, AllergyInput input, CancellationToken ct = default);
    Task UpdateAllergyAsync(Guid petId, Guid id, AllergyInput input, CancellationToken ct = default);
    Task DeleteAllergyAsync(Guid petId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<DietEntryDto>> ListDietAsync(Guid petId, string? q, CancellationToken ct = default);
    Task<DietEntryDto> AddDietAsync(Guid petId, DietEntryInput input, CancellationToken ct = default);
    Task UpdateDietAsync(Guid petId, Guid id, DietEntryInput input, CancellationToken ct = default);
    Task DeleteDietAsync(Guid petId, Guid id, CancellationToken ct = default);

    /// <summary>Parses the legacy free-text Pet.Allergies / Pet.DietNotes into typed rows (idempotent: skips existing).</summary>
    Task<ImportFromTextResult> ImportFromTextAsync(Guid petId, CancellationToken ct = default);

    Task<IReadOnlyList<LabResultDto>> ListLabsAsync(Guid petId, CancellationToken ct = default);
    Task<LabResultDto> AddLabAsync(Guid petId, LabResultInput input, CancellationToken ct = default);
    Task<LabResultDto> UpdateLabAsync(Guid petId, Guid id, LabResultInput input, CancellationToken ct = default);
    Task DeleteLabAsync(Guid petId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Extracts text from an uploaded document. The default registration is a no-op that reports
/// <see cref="OcrResult.Available"/> = false — no OCR engine ships with the platform.
/// </summary>
public interface IDocumentOcr
{
    Task<OcrResult> ExtractTextAsync(string documentUrl, CancellationToken ct = default);
}

public record OcrResult(bool Available, string? Text, string? Message)
{
    public static OcrResult Unavailable { get; } = new(false, null, "OCR is not configured on this deployment. Please transcribe the values manually.");
}

// =====================================================================
// Versioning / sealing / certificates / FHIR / insights
// =====================================================================

public record AmendRecordInput(string Reason, VaccinationInputLite? Vaccination, MedicalRecordInputLite? Medical);
public record VaccinationInputLite(string VaccineName, DateTime AdministeredOn, DateTime? NextDueOn, string? AdministeredByVet, string? Notes, string? CertificateUrl);
public record MedicalRecordInputLite(DateTime VisitDate, string? Diagnosis, string? Treatment, string? Notes, string? VetName, string? AttachmentUrl, string? PrescriptionFileUrl);

public record RecordVersionDto(int Version, DateTime ChangedAt, Guid ChangedByUserId, string? ChangedByName, string? ChangeReason,
    IReadOnlyDictionary<string, string?> Snapshot);
public record RecordSealDto(HealthRecordType RecordType, Guid RecordId, Guid SealedByUserId, string? SealedByName, Guid? VetDoctorId,
    DateTime SealedAt, string SignatureHash, bool SignatureValid, bool ChangedSinceSeal);
public record RecordHistoryDto(HealthRecordType RecordType, Guid RecordId, bool Sealed, RecordSealDto? Seal, IReadOnlyList<RecordVersionDto> Versions);
public record SealStateDto(Guid RecordId, HealthRecordType RecordType, DateTime SealedAt, string? SealedByName);

public interface IHealthRecordIntegrityService
{
    Task<RecordHistoryDto> HistoryAsync(Guid petId, HealthRecordType type, Guid recordId, CancellationToken ct = default);
    Task<IReadOnlyList<SealStateDto>> ListSealsAsync(Guid petId, CancellationToken ct = default);
    Task<RecordSealDto> SealAsync(Guid petId, HealthRecordType type, Guid recordId, CancellationToken ct = default);
    Task AmendAsync(Guid petId, HealthRecordType type, Guid recordId, AmendRecordInput input, CancellationToken ct = default);
}

public record CertificateVaccineDto(string VaccineName, DateTime AdministeredOn, DateTime? NextDueOn, string? AdministeredByVet, bool Sealed);
public record CertificateIssueDto(Guid Id, string Token, string VerifyPath, string CertificatePath, DateTime IssuedAt);
public record CertificateVerifyDto(bool Valid, string Status, string? PetName, string? Species, string? Breed, string? MicrochipNumber,
    string? OwnerName, DateTime? IssuedAt, bool ContentChangedSinceIssue, IReadOnlyList<CertificateVaccineDto> Vaccinations, string Notice);

public interface IVaccinationCertificateService
{
    Task<CertificateIssueDto> IssueAsync(Guid petId, CancellationToken ct = default);
    Task<CertificateVerifyDto> VerifyAsync(string token, CancellationToken ct = default);
    Task RevokeAsync(Guid petId, Guid certificateId, CancellationToken ct = default);
}

public interface IFhirExportService
{
    /// <summary>FHIR R4 Bundle (collection) as JSON text.</summary>
    Task<string> ExportAsync(Guid petId, CancellationToken ct = default);
}

public enum InsightSeverity { Info = 0, Watch = 1, Alert = 2 }
public record PetInsightDto(string Kind, InsightSeverity Severity, string Title, string Detail, string? Metric);
public record PetInsightsDto(Guid PetId, DateTime GeneratedAt, IReadOnlyList<PetInsightDto> Insights, string Disclaimer);

public interface IPetInsightService
{
    Task<PetInsightsDto> GetAsync(Guid petId, CancellationToken ct = default);
}

// =====================================================================
// Herd-level livestock treatment
// =====================================================================

public record LivestockTreatmentInput(Guid? PetId, Guid? PetGroupId, string Diagnosis, string Treatment, DateTime AdministeredOn,
    int MeatWithdrawalDays, int MilkWithdrawalDays, string? Notes);
public record LivestockTreatmentDto(Guid Id, Guid BatchId, Guid PetId, string PetName, Guid? PetGroupId, string Diagnosis, string Treatment,
    DateTime AdministeredOn, int MeatWithdrawalDays, int MilkWithdrawalDays, DateTime? MeatSafeOn, DateTime? MilkSafeOn, string? Notes);
public record WithdrawalStatusDto(Guid PetId, string PetName, Guid? GroupId, DateTime? MeatSafeOn, DateTime? MilkSafeOn, bool MeatBlocked, bool MilkBlocked);
public record LivestockTreatmentResultDto(int Animals, Guid BatchId, IReadOnlyList<LivestockTreatmentDto> Treatments);

public interface ILivestockHealthService
{
    Task<LivestockTreatmentResultDto> AddTreatmentAsync(LivestockTreatmentInput input, CancellationToken ct = default);
    Task<IReadOnlyList<LivestockTreatmentDto>> ListTreatmentsAsync(Guid? groupId, Guid? petId, CancellationToken ct = default);
    Task<IReadOnlyList<WithdrawalStatusDto>> WithdrawalStatusAsync(Guid? groupId, CancellationToken ct = default);
    Task DeleteTreatmentAsync(Guid id, CancellationToken ct = default);
}
