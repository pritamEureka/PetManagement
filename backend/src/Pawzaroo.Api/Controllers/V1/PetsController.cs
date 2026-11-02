using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Pet profiles and everything hanging off them. Fine-grained access (owner /
/// co-owner / caretaker / viewer / treating vet) is enforced in the services via
/// IPetAccessService; the [Permission] attributes gate the feature as a whole.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/pets")]
[Authorize]
public class PetsController : ControllerBase
{
    private readonly IPetService _pets;
    private readonly IPetCareService _care;
    private readonly IPetFinanceService _finance;
    private readonly IPetSharingService _sharing;

    public PetsController(IPetService pets, IPetCareService care, IPetFinanceService finance, IPetSharingService sharing)
    {
        _pets = pets;
        _care = care;
        _finance = finance;
        _sharing = sharing;
    }

    // ---------- Profile ----------

    [HttpGet("mine")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<PetSummaryDto>> Mine(CancellationToken ct) => _pets.ListMineAsync(ct);

    [HttpGet("upcoming-care")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<UpcomingCareItemDto>> UpcomingCare([FromQuery] int days = 30, CancellationToken ct = default)
        => _pets.UpcomingCareAsync(days, ct);

    [HttpGet("vaccine-plans/{animalType}")]
    [Permission(Permissions.Pets.View)]
    public IReadOnlyList<VaccinePlanItem> VaccinePlan(AnimalType animalType) => _pets.GetVaccinePlan(animalType);

    [HttpGet("{id:guid}")]
    [Permission(Permissions.Pets.View)]
    public Task<PetDetailDto> Get(Guid id, CancellationToken ct) => _pets.GetAsync(id, ct);

    [HttpPost]
    [Permission(Permissions.Pets.Create)]
    [EnableRateLimiting("writes")]
    [Audit("Pets", "create", entityName: "Pet")]
    public async Task<IActionResult> Create([FromBody] UpsertPetInput input, CancellationToken ct)
    {
        var pet = await _pets.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = pet.Id, version = "1.0" }, pet);
    }

    [HttpPut("{id:guid}")]
    [Permission(Permissions.Pets.Edit)]
    [Audit("Pets", "update", entityName: "Pet")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertPetInput input, CancellationToken ct)
    {
        await _pets.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Permission(Permissions.Pets.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _pets.DeleteAsync(id, ct);
        return NoContent();
    }

    // ---------- Photos ----------

    [HttpPost("{id:guid}/photos")]
    [Permission(Permissions.Pets.Edit)]
    public Task<PetPhotoDto> AddPhoto(Guid id, [FromBody] PetPhotoInput input, CancellationToken ct) => _pets.AddPhotoAsync(id, input, ct);

    [HttpPost("{id:guid}/photos/{photoId:guid}/primary")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> SetPrimaryPhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        await _pets.SetPrimaryPhotoAsync(id, photoId, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> DeletePhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        await _pets.DeletePhotoAsync(id, photoId, ct);
        return NoContent();
    }

    // ---------- Health records ----------
    // Vets reach these through their treating-vet access, so the gate is pets.view
    // and the service decides whether this caller may write clinical data.

    [HttpPost("{id:guid}/vaccinations")]
    [Permission(Permissions.Pets.View)]
    public Task<VaccinationDto> AddVaccination(Guid id, [FromBody] VaccinationInput input, CancellationToken ct)
        => _pets.AddVaccinationAsync(id, input, ct);

    [HttpPut("{id:guid}/vaccinations/{recordId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> UpdateVaccination(Guid id, Guid recordId, [FromBody] VaccinationInput input, CancellationToken ct)
    {
        await _pets.UpdateVaccinationAsync(id, recordId, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/vaccinations/{recordId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> DeleteVaccination(Guid id, Guid recordId, CancellationToken ct)
    {
        await _pets.DeleteVaccinationAsync(id, recordId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/vaccine-schedule")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<VaccineScheduleItemDto>> VaccineSchedule(Guid id, CancellationToken ct)
        => _pets.GetVaccineScheduleAsync(id, ct);

    [HttpPost("{id:guid}/medical-records")]
    [Permission(Permissions.Pets.View)]
    public Task<MedicalRecordDto> AddMedicalRecord(Guid id, [FromBody] MedicalRecordInput input, CancellationToken ct)
        => _pets.AddMedicalRecordAsync(id, input, ct);

    [HttpPut("{id:guid}/medical-records/{recordId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> UpdateMedicalRecord(Guid id, Guid recordId, [FromBody] MedicalRecordInput input, CancellationToken ct)
    {
        await _pets.UpdateMedicalRecordAsync(id, recordId, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/medical-records/{recordId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> DeleteMedicalRecord(Guid id, Guid recordId, CancellationToken ct)
    {
        await _pets.DeleteMedicalRecordAsync(id, recordId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/grooming-records")]
    [Permission(Permissions.Pets.Edit)]
    public Task<GroomingRecordDto> AddGroomingRecord(Guid id, [FromBody] GroomingRecordInput input, CancellationToken ct)
        => _pets.AddGroomingRecordAsync(id, input, ct);

    [HttpPut("{id:guid}/grooming-records/{recordId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> UpdateGroomingRecord(Guid id, Guid recordId, [FromBody] GroomingRecordInput input, CancellationToken ct)
    {
        await _pets.UpdateGroomingRecordAsync(id, recordId, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/grooming-records/{recordId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> DeleteGroomingRecord(Guid id, Guid recordId, CancellationToken ct)
    {
        await _pets.DeleteGroomingRecordAsync(id, recordId, ct);
        return NoContent();
    }

    // ---------- Insights ----------

    [HttpGet("{id:guid}/recommendations")]
    [Permission(Permissions.Pets.View)]
    public Task<RecommendationsDto> Recommendations(Guid id, CancellationToken ct) => _pets.GetRecommendationsAsync(id, ct);

    [HttpGet("{id:guid}/lineage")]
    [Permission(Permissions.Pets.View)]
    public Task<LineageDto> Lineage(Guid id, [FromQuery] int generations = 3, CancellationToken ct = default)
        => _pets.GetLineageAsync(id, generations, ct);

    // ---------- Weight ----------

    [HttpGet("{id:guid}/weights")]
    [Permission(Permissions.Pets.View)]
    public Task<WeightHistoryDto> Weights(Guid id, CancellationToken ct) => _care.ListWeightsAsync(id, ct);

    [HttpPost("{id:guid}/weights")]
    [Permission(Permissions.Pets.View)]
    public Task<WeightLogDto> AddWeight(Guid id, [FromBody] WeightLogInput input, CancellationToken ct) => _care.AddWeightAsync(id, input, ct);

    [HttpDelete("{id:guid}/weights/{logId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> DeleteWeight(Guid id, Guid logId, CancellationToken ct)
    {
        await _care.DeleteWeightAsync(id, logId, ct);
        return NoContent();
    }

    // ---------- Medications ----------

    [HttpGet("medications/today")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<TodayDoseDto>> TodayDoses([FromQuery] int tzOffset = 0, CancellationToken ct = default)
        => _care.TodayDosesAsync(tzOffset, ct);

    [HttpGet("{id:guid}/medications")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<MedicationDto>> Medications(Guid id, CancellationToken ct) => _care.ListMedicationsAsync(id, ct);

    [HttpPost("{id:guid}/medications")]
    [Permission(Permissions.Pets.View)]
    public Task<MedicationDto> AddMedication(Guid id, [FromBody] MedicationInput input, CancellationToken ct)
        => _care.AddMedicationAsync(id, input, ct);

    [HttpPut("{id:guid}/medications/{medicationId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> UpdateMedication(Guid id, Guid medicationId, [FromBody] MedicationInput input, CancellationToken ct)
    {
        await _care.UpdateMedicationAsync(id, medicationId, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/medications/{medicationId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> DeleteMedication(Guid id, Guid medicationId, CancellationToken ct)
    {
        await _care.DeleteMedicationAsync(id, medicationId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/medications/{medicationId:guid}/doses")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> LogDose(Guid id, Guid medicationId, [FromBody] DoseLogInput input, CancellationToken ct)
    {
        await _care.LogDoseAsync(id, medicationId, input, ct);
        return NoContent();
    }

    // ---------- Caretakers ----------

    [HttpGet("{id:guid}/caretakers")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<CaretakerDto>> Caretakers(Guid id, CancellationToken ct) => _care.ListCaretakersAsync(id, ct);

    [HttpPost("{id:guid}/caretakers")]
    [Permission(Permissions.Pets.Edit)]
    [EnableRateLimiting("writes")]
    public Task<CaretakerDto> InviteCaretaker(Guid id, [FromBody] InviteCaretakerInput input, CancellationToken ct)
        => _care.InviteCaretakerAsync(id, input, ct);

    [HttpPut("{id:guid}/caretakers/{caretakerId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> UpdateCaretaker(Guid id, Guid caretakerId, [FromBody] UpdateCaretakerInput input, CancellationToken ct)
    {
        await _care.UpdateCaretakerAsync(id, caretakerId, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/caretakers/{caretakerId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> RemoveCaretaker(Guid id, Guid caretakerId, CancellationToken ct)
    {
        await _care.RemoveCaretakerAsync(id, caretakerId, ct);
        return NoContent();
    }

    // ---------- Daily care ----------

    [HttpGet("{id:guid}/care/today")]
    [Permission(Permissions.Pets.View)]
    public Task<CareTodayDto> CareToday(Guid id, [FromQuery] int tzOffset = 0, CancellationToken ct = default)
        => _care.TodayAsync(id, tzOffset, ct);

    [HttpGet("{id:guid}/routines")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<CareRoutineDto>> Routines(Guid id, CancellationToken ct) => _care.ListRoutinesAsync(id, ct);

    [HttpPost("{id:guid}/routines")]
    [Permission(Permissions.Pets.Edit)]
    public Task<CareRoutineDto> AddRoutine(Guid id, [FromBody] CareRoutineInput input, CancellationToken ct) => _care.AddRoutineAsync(id, input, ct);

    [HttpPut("{id:guid}/routines/{routineId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> UpdateRoutine(Guid id, Guid routineId, [FromBody] CareRoutineInput input, CancellationToken ct)
    {
        await _care.UpdateRoutineAsync(id, routineId, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/routines/{routineId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> DeleteRoutine(Guid id, Guid routineId, CancellationToken ct)
    {
        await _care.DeleteRoutineAsync(id, routineId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/care-logs")]
    [Permission(Permissions.Pets.View)]
    public Task<CareLogDto> AddCareLog(Guid id, [FromBody] CareLogInput input, CancellationToken ct) => _care.AddCareLogAsync(id, input, ct);

    [HttpDelete("{id:guid}/care-logs/{logId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> DeleteCareLog(Guid id, Guid logId, CancellationToken ct)
    {
        await _care.DeleteCareLogAsync(id, logId, ct);
        return NoContent();
    }

    // ---------- Expenses & insurance ----------

    [HttpGet("expenses/summary")]
    [Permission(Permissions.Pets.View)]
    public Task<ExpenseSummaryDto> ExpenseSummary([FromQuery] int months = 6, [FromQuery] Guid? petId = null, CancellationToken ct = default)
        => _finance.SummaryAsync(months, petId, ct);

    [HttpGet("{id:guid}/expenses")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<ExpenseDto>> Expenses(Guid id, CancellationToken ct) => _finance.ListExpensesAsync(id, ct);

    [HttpPost("{id:guid}/expenses")]
    [Permission(Permissions.Pets.Edit)]
    public Task<ExpenseDto> AddExpense(Guid id, [FromBody] ExpenseInput input, CancellationToken ct) => _finance.AddExpenseAsync(id, input, ct);

    [HttpDelete("{id:guid}/expenses/{expenseId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> DeleteExpense(Guid id, Guid expenseId, CancellationToken ct)
    {
        await _finance.DeleteExpenseAsync(id, expenseId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/insurance")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<InsurancePolicyDto>> Insurance(Guid id, CancellationToken ct) => _finance.ListPoliciesAsync(id, ct);

    [HttpPost("{id:guid}/insurance")]
    [Permission(Permissions.Pets.Edit)]
    public Task<InsurancePolicyDto> AddInsurance(Guid id, [FromBody] InsurancePolicyInput input, CancellationToken ct)
        => _finance.AddPolicyAsync(id, input, ct);

    [HttpPut("{id:guid}/insurance/{policyId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> UpdateInsurance(Guid id, Guid policyId, [FromBody] InsurancePolicyInput input, CancellationToken ct)
    {
        await _finance.UpdatePolicyAsync(id, policyId, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/insurance/{policyId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> DeleteInsurance(Guid id, Guid policyId, CancellationToken ct)
    {
        await _finance.DeletePolicyAsync(id, policyId, ct);
        return NoContent();
    }

    // ---------- Sharing & export ----------

    [HttpGet("{id:guid}/health-report")]
    [Permission(Permissions.Pets.View)]
    [Produces("text/html")]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> HealthReport(Guid id, CancellationToken ct)
        => Content(PetHealthReportRenderer.Render(await _sharing.GetHealthReportAsync(id, ct)), "text/html");

    [HttpGet("{id:guid}/share-links")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<ShareLinkDto>> ShareLinks(Guid id, CancellationToken ct) => _sharing.ListShareLinksAsync(id, ct);

    [HttpPost("{id:guid}/share-links")]
    [Permission(Permissions.Pets.Edit)]
    [EnableRateLimiting("writes")]
    public Task<ShareLinkDto> CreateShareLink(Guid id, [FromBody] CreateShareLinkInput input, CancellationToken ct)
        => _sharing.CreateShareLinkAsync(id, input, ct);

    [HttpDelete("{id:guid}/share-links/{linkId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> RevokeShareLink(Guid id, Guid linkId, CancellationToken ct)
    {
        await _sharing.RevokeShareLinkAsync(id, linkId, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/public-profile")]
    [Permission(Permissions.Pets.Edit)]
    public Task<PublicProfileStateDto> SetPublicProfile(Guid id, [FromBody] PublicProfileInput input, CancellationToken ct)
        => _sharing.SetPublicProfileAsync(id, input, ct);
}
