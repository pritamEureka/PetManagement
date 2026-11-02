using Pawzaroo.Application.Modules.Pets.Dtos;

namespace Pawzaroo.Application.Modules.Pets.Services;

/// <summary>
/// Single source of truth for "may the current user do X to this pet".
/// Owner > CoOwner > Caretaker > Viewer; vets with an appointment for the pet
/// get read access plus clinical-record writes; holders of pets.read_any get read.
/// </summary>
public interface IPetAccessService
{
    Task<PetAccessLevel> GetLevelAsync(Guid petId, CancellationToken ct = default);
    Task EnsureCanReadAsync(Guid petId, CancellationToken ct = default);
    Task EnsureCanLogCareAsync(Guid petId, CancellationToken ct = default);
    Task EnsureCanEditClinicalAsync(Guid petId, CancellationToken ct = default);
    Task EnsureCanEditAsync(Guid petId, CancellationToken ct = default);
    Task EnsureOwnerAsync(Guid petId, CancellationToken ct = default);
}

public interface IPetService
{
    Task<IReadOnlyList<PetSummaryDto>> ListMineAsync(CancellationToken ct = default);
    Task<PetDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PetDetailDto> CreateAsync(UpsertPetInput input, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpsertPetInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Photos
    Task<PetPhotoDto> AddPhotoAsync(Guid petId, PetPhotoInput input, CancellationToken ct = default);
    Task SetPrimaryPhotoAsync(Guid petId, Guid photoId, CancellationToken ct = default);
    Task DeletePhotoAsync(Guid petId, Guid photoId, CancellationToken ct = default);

    // Health records
    Task<VaccinationDto> AddVaccinationAsync(Guid petId, VaccinationInput input, CancellationToken ct = default);
    Task UpdateVaccinationAsync(Guid petId, Guid recordId, VaccinationInput input, CancellationToken ct = default);
    Task DeleteVaccinationAsync(Guid petId, Guid recordId, CancellationToken ct = default);
    Task<MedicalRecordDto> AddMedicalRecordAsync(Guid petId, MedicalRecordInput input, CancellationToken ct = default);
    Task UpdateMedicalRecordAsync(Guid petId, Guid recordId, MedicalRecordInput input, CancellationToken ct = default);
    Task DeleteMedicalRecordAsync(Guid petId, Guid recordId, CancellationToken ct = default);
    Task<GroomingRecordDto> AddGroomingRecordAsync(Guid petId, GroomingRecordInput input, CancellationToken ct = default);
    Task UpdateGroomingRecordAsync(Guid petId, Guid recordId, GroomingRecordInput input, CancellationToken ct = default);
    Task DeleteGroomingRecordAsync(Guid petId, Guid recordId, CancellationToken ct = default);

    // Species templates / recommendations
    IReadOnlyList<VaccinePlanItem> GetVaccinePlan(Domain.Common.AnimalType animalType);
    Task<IReadOnlyList<VaccineScheduleItemDto>> GetVaccineScheduleAsync(Guid petId, CancellationToken ct = default);
    Task<RecommendationsDto> GetRecommendationsAsync(Guid petId, CancellationToken ct = default);
    Task<IReadOnlyList<UpcomingCareItemDto>> UpcomingCareAsync(int days, CancellationToken ct = default);

    // Lineage
    Task<LineageDto> GetLineageAsync(Guid petId, int generations, CancellationToken ct = default);
}

public interface IPetCareService
{
    // Weight
    Task<WeightHistoryDto> ListWeightsAsync(Guid petId, CancellationToken ct = default);
    Task<WeightLogDto> AddWeightAsync(Guid petId, WeightLogInput input, CancellationToken ct = default);
    Task DeleteWeightAsync(Guid petId, Guid logId, CancellationToken ct = default);

    // Medications
    Task<IReadOnlyList<MedicationDto>> ListMedicationsAsync(Guid petId, CancellationToken ct = default);
    Task<MedicationDto> AddMedicationAsync(Guid petId, MedicationInput input, CancellationToken ct = default);
    Task UpdateMedicationAsync(Guid petId, Guid medicationId, MedicationInput input, CancellationToken ct = default);
    Task DeleteMedicationAsync(Guid petId, Guid medicationId, CancellationToken ct = default);
    Task LogDoseAsync(Guid petId, Guid medicationId, DoseLogInput input, CancellationToken ct = default);
    Task<IReadOnlyList<TodayDoseDto>> TodayDosesAsync(int tzOffsetMinutes, CancellationToken ct = default);

    // Caretakers
    Task<IReadOnlyList<CaretakerDto>> ListCaretakersAsync(Guid petId, CancellationToken ct = default);
    Task<CaretakerDto> InviteCaretakerAsync(Guid petId, InviteCaretakerInput input, CancellationToken ct = default);
    Task UpdateCaretakerAsync(Guid petId, Guid caretakerId, UpdateCaretakerInput input, CancellationToken ct = default);
    Task RemoveCaretakerAsync(Guid petId, Guid caretakerId, CancellationToken ct = default);

    // Routines & logs
    Task<IReadOnlyList<CareRoutineDto>> ListRoutinesAsync(Guid petId, CancellationToken ct = default);
    Task<CareRoutineDto> AddRoutineAsync(Guid petId, CareRoutineInput input, CancellationToken ct = default);
    Task UpdateRoutineAsync(Guid petId, Guid routineId, CareRoutineInput input, CancellationToken ct = default);
    Task DeleteRoutineAsync(Guid petId, Guid routineId, CancellationToken ct = default);
    Task<CareLogDto> AddCareLogAsync(Guid petId, CareLogInput input, CancellationToken ct = default);
    Task DeleteCareLogAsync(Guid petId, Guid logId, CancellationToken ct = default);
    Task<CareTodayDto> TodayAsync(Guid petId, int tzOffsetMinutes, CancellationToken ct = default);
}

public interface IPetFinanceService
{
    Task<IReadOnlyList<ExpenseDto>> ListExpensesAsync(Guid petId, CancellationToken ct = default);
    Task<ExpenseDto> AddExpenseAsync(Guid petId, ExpenseInput input, CancellationToken ct = default);
    Task DeleteExpenseAsync(Guid petId, Guid expenseId, CancellationToken ct = default);
    Task<ExpenseSummaryDto> SummaryAsync(int months, Guid? petId, CancellationToken ct = default);

    Task<IReadOnlyList<InsurancePolicyDto>> ListPoliciesAsync(Guid petId, CancellationToken ct = default);
    Task<InsurancePolicyDto> AddPolicyAsync(Guid petId, InsurancePolicyInput input, CancellationToken ct = default);
    Task UpdatePolicyAsync(Guid petId, Guid policyId, InsurancePolicyInput input, CancellationToken ct = default);
    Task DeletePolicyAsync(Guid petId, Guid policyId, CancellationToken ct = default);
}

public interface IPetSharingService
{
    Task<IReadOnlyList<ShareLinkDto>> ListShareLinksAsync(Guid petId, CancellationToken ct = default);
    Task<ShareLinkDto> CreateShareLinkAsync(Guid petId, CreateShareLinkInput input, CancellationToken ct = default);
    Task RevokeShareLinkAsync(Guid petId, Guid linkId, CancellationToken ct = default);

    /// <summary>Authenticated export for anyone with read access.</summary>
    Task<HealthReportDto> GetHealthReportAsync(Guid petId, CancellationToken ct = default);
    /// <summary>Anonymous export through an unexpired, unrevoked share token.</summary>
    Task<HealthReportDto?> GetSharedHealthReportAsync(string token, CancellationToken ct = default);

    Task<PublicProfileStateDto> SetPublicProfileAsync(Guid petId, PublicProfileInput input, CancellationToken ct = default);
    Task<PublicPetDto?> GetPublicPetAsync(string code, CancellationToken ct = default);
    Task ReportSightingAsync(string code, SightingInput input, CancellationToken ct = default);

    /// <summary>Moves the pet to a new owner (adoption hand-over). Caller authorises.</summary>
    Task TransferOwnershipAsync(Guid petId, Guid toUserId, Guid? adoptionListingId, bool includeHealthHistory, CancellationToken ct = default);
}

public interface ILivestockService
{
    Task<IReadOnlyList<PetGroupDto>> ListGroupsAsync(CancellationToken ct = default);
    Task<PetGroupDto> CreateGroupAsync(PetGroupInput input, CancellationToken ct = default);
    Task UpdateGroupAsync(Guid groupId, PetGroupInput input, CancellationToken ct = default);
    Task DeleteGroupAsync(Guid groupId, CancellationToken ct = default);
    Task SetMembersAsync(Guid groupId, GroupMembersInput input, CancellationToken ct = default);
    Task<int> BulkVaccinateAsync(Guid groupId, VaccinationInput input, CancellationToken ct = default);

    Task<IReadOnlyList<ProductionLogDto>> ListProductionAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<ProductionLogDto> AddProductionAsync(ProductionLogInput input, CancellationToken ct = default);
    Task DeleteProductionAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<BreedingRecordDto>> ListBreedingAsync(CancellationToken ct = default);
    Task<BreedingRecordDto> AddBreedingAsync(BreedingRecordInput input, CancellationToken ct = default);
    Task UpdateBreedingAsync(Guid id, BreedingRecordInput input, CancellationToken ct = default);
    Task DeleteBreedingAsync(Guid id, CancellationToken ct = default);
}
