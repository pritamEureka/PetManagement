using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Pets.HealthPlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Care plans, exposure reports, typed health records, versioning/sealing, certificates, exports, insights, labs.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public class PetHealthPlusController : ControllerBase
{
    private readonly ICarePlanService _plans;
    private readonly IExposureService _exposure;
    private readonly IPetHealthRecordsService _records;
    private readonly IHealthRecordIntegrityService _integrity;
    private readonly IVaccinationCertificateService _certs;
    private readonly IFhirExportService _fhir;
    private readonly IPetInsightService _insights;

    public PetHealthPlusController(ICarePlanService plans, IExposureService exposure, IPetHealthRecordsService records,
        IHealthRecordIntegrityService integrity, IVaccinationCertificateService certs, IFhirExportService fhir, IPetInsightService insights)
    {
        _plans = plans; _exposure = exposure; _records = records; _integrity = integrity; _certs = certs; _fhir = fhir; _insights = insights;
    }

    // ---------- Care plan templates (vets publish) ----------

    [HttpGet("care-plan-templates")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<CarePlanTemplateDto>> Templates([FromQuery] AnimalType? species, [FromQuery] bool mine = false, CancellationToken ct = default)
        => _plans.ListTemplatesAsync(species, mine, ct);

    [HttpPost("care-plan-templates")]
    [Permission(Permissions.Vets.Edit)]
    public Task<CarePlanTemplateDto> CreateTemplate([FromBody] CarePlanTemplateInput input, CancellationToken ct) => _plans.SaveTemplateAsync(null, input, ct);

    [HttpPut("care-plan-templates/{id:guid}")]
    [Permission(Permissions.Vets.Edit)]
    public Task<CarePlanTemplateDto> UpdateTemplate(Guid id, [FromBody] CarePlanTemplateInput input, CancellationToken ct) => _plans.SaveTemplateAsync(id, input, ct);

    [HttpPost("care-plan-templates/{id:guid}/publish")]
    [Permission(Permissions.Vets.Edit)]
    public async Task<IActionResult> Publish(Guid id, [FromQuery] bool published = true, CancellationToken ct = default)
    {
        await _plans.SetTemplatePublishedAsync(id, published, ct);
        return NoContent();
    }

    [HttpDelete("care-plan-templates/{id:guid}")]
    [Permission(Permissions.Vets.Edit)]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
    {
        await _plans.DeleteTemplateAsync(id, ct);
        return NoContent();
    }

    // ---------- Pet care plans ----------

    [HttpGet("pets/{id:guid}/care-plans")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<CarePlanDto>> Plans(Guid id, CancellationToken ct) => _plans.ListPlansAsync(id, ct);

    [HttpGet("pets/{id:guid}/care-plans/summary")]
    [Permission(Permissions.Pets.View)]
    public Task<MissedCareSummaryDto> PlanSummary(Guid id, CancellationToken ct) => _plans.MissedSummaryAsync(id, ct);

    [HttpPost("pets/{id:guid}/care-plans")]
    [Permission(Permissions.Pets.Edit)]
    public Task<CarePlanDto> CreatePlan(Guid id, [FromBody] CustomPlanInput input, CancellationToken ct) => _plans.CreateCustomPlanAsync(id, input, ct);

    [HttpPost("pets/{id:guid}/care-plans/adopt")]
    [Permission(Permissions.Pets.Edit)]
    public Task<CarePlanDto> Adopt(Guid id, [FromBody] AdoptTemplateInput input, CancellationToken ct) => _plans.AdoptTemplateAsync(id, input, ct);

    [HttpPost("pets/{id:guid}/care-plans/{planId:guid}/items")]
    [Permission(Permissions.Pets.Edit)]
    public Task<CarePlanItemDto> AddPlanItem(Guid id, Guid planId, [FromBody] CustomPlanItemInput input, CancellationToken ct) => _plans.AddItemAsync(id, planId, input, ct);

    [HttpDelete("pets/{id:guid}/care-plans/{planId:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> ArchivePlan(Guid id, Guid planId, CancellationToken ct)
    {
        await _plans.ArchivePlanAsync(id, planId, ct);
        return NoContent();
    }

    [HttpPost("pets/{id:guid}/care-plan-items/{itemId:guid}/complete")]
    [Permission(Permissions.Pets.View)]
    public Task<CarePlanItemDto> CompleteItem(Guid id, Guid itemId, [FromBody] CompleteCareItemInput input, CancellationToken ct) => _plans.CompleteItemAsync(id, itemId, input, ct);

    [HttpPost("pets/{id:guid}/care-plan-items/{itemId:guid}/reschedule")]
    [Permission(Permissions.Pets.View)]
    public Task<CarePlanItemDto> RescheduleItem(Guid id, Guid itemId, [FromBody] RescheduleCareItemInput input, CancellationToken ct) => _plans.RescheduleItemAsync(id, itemId, input, ct);

    [HttpPost("pets/{id:guid}/care-plan-items/{itemId:guid}/assign")]
    [Permission(Permissions.Pets.Edit)]
    public Task<CarePlanItemDto> AssignItem(Guid id, Guid itemId, [FromBody] AssignCareItemInput input, CancellationToken ct) => _plans.AssignItemAsync(id, itemId, input, ct);

    // ---------- Exposure / emergency ----------

    [HttpGet("pets/exposure-substances")]
    [Permission(Permissions.Pets.View)]
    public IReadOnlyList<CommonSubstanceDto> Substances() => _exposure.CommonSubstances();

    [HttpGet("pets/{id:guid}/exposures")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<ExposureReportDto>> Exposures(Guid id, CancellationToken ct) => _exposure.ListAsync(id, ct);

    [HttpPost("pets/{id:guid}/exposures")]
    [Permission(Permissions.Pets.View)]
    [EnableRateLimiting("writes")]
    public Task<EscalationDto> ReportExposure(Guid id, [FromBody] ExposureReportInput input, CancellationToken ct) => _exposure.ReportAsync(id, input, ct);

    [HttpPost("pets/{id:guid}/exposures/{reportId:guid}/update")]
    [Permission(Permissions.Pets.View)]
    public Task<ExposureReportDto> UpdateExposure(Guid id, Guid reportId, [FromBody] ExposureUpdateInput input, CancellationToken ct) => _exposure.UpdateAsync(id, reportId, input, ct);

    [HttpGet("pets/{id:guid}/emergency-card")]
    [Permission(Permissions.Pets.View)]
    public Task<EmergencyCardDto> EmergencyCard(Guid id, CancellationToken ct) => _exposure.GetEmergencyCardAsync(id, ct);

    [HttpPost("pets/{id:guid}/emergency-card/links")]
    [Permission(Permissions.Pets.Edit)]
    [EnableRateLimiting("writes")]
    public Task<EmergencyCardLinkDto> CreateEmergencyLink(Guid id, [FromQuery] int validHours = 24, CancellationToken ct = default) => _exposure.CreateEmergencyCardLinkAsync(id, validHours, ct);

    [HttpDelete("pets/{id:guid}/emergency-card/links")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> RevokeEmergencyLinks(Guid id, CancellationToken ct)
    {
        await _exposure.RevokeEmergencyCardLinksAsync(id, ct);
        return NoContent();
    }

    // ---------- Allergies / diet ----------

    [HttpGet("pets/allergies/search")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<AllergyDto>> SearchAllergies([FromQuery] string q, CancellationToken ct) => _records.SearchMyAllergiesAsync(q, ct);

    [HttpGet("pets/{id:guid}/allergies")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<AllergyDto>> Allergies(Guid id, [FromQuery] string? q, [FromQuery] bool? active, CancellationToken ct) => _records.ListAllergiesAsync(id, q, active, ct);

    [HttpPost("pets/{id:guid}/allergies")]
    [Permission(Permissions.Pets.View)]
    public Task<AllergyDto> AddAllergy(Guid id, [FromBody] AllergyInput input, CancellationToken ct) => _records.AddAllergyAsync(id, input, ct);

    [HttpPut("pets/{id:guid}/allergies/{allergyId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> UpdateAllergy(Guid id, Guid allergyId, [FromBody] AllergyInput input, CancellationToken ct)
    {
        await _records.UpdateAllergyAsync(id, allergyId, input, ct);
        return NoContent();
    }

    [HttpDelete("pets/{id:guid}/allergies/{allergyId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> DeleteAllergy(Guid id, Guid allergyId, CancellationToken ct)
    {
        await _records.DeleteAllergyAsync(id, allergyId, ct);
        return NoContent();
    }

    [HttpGet("pets/{id:guid}/diet")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<DietEntryDto>> Diet(Guid id, [FromQuery] string? q, CancellationToken ct) => _records.ListDietAsync(id, q, ct);

    [HttpPost("pets/{id:guid}/diet")]
    [Permission(Permissions.Pets.View)]
    public Task<DietEntryDto> AddDiet(Guid id, [FromBody] DietEntryInput input, CancellationToken ct) => _records.AddDietAsync(id, input, ct);

    [HttpPut("pets/{id:guid}/diet/{entryId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> UpdateDiet(Guid id, Guid entryId, [FromBody] DietEntryInput input, CancellationToken ct)
    {
        await _records.UpdateDietAsync(id, entryId, input, ct);
        return NoContent();
    }

    [HttpDelete("pets/{id:guid}/diet/{entryId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> DeleteDiet(Guid id, Guid entryId, CancellationToken ct)
    {
        await _records.DeleteDietAsync(id, entryId, ct);
        return NoContent();
    }

    [HttpPost("pets/{id:guid}/health-text/import")]
    [Permission(Permissions.Pets.View)]
    public Task<ImportFromTextResult> ImportText(Guid id, CancellationToken ct) => _records.ImportFromTextAsync(id, ct);

    // ---------- Labs ----------

    [HttpGet("pets/{id:guid}/labs")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<LabResultDto>> Labs(Guid id, CancellationToken ct) => _records.ListLabsAsync(id, ct);

    [HttpPost("pets/{id:guid}/labs")]
    [Permission(Permissions.Pets.View)]
    public Task<LabResultDto> AddLab(Guid id, [FromBody] LabResultInput input, CancellationToken ct) => _records.AddLabAsync(id, input, ct);

    [HttpPut("pets/{id:guid}/labs/{labId:guid}")]
    [Permission(Permissions.Pets.View)]
    public Task<LabResultDto> UpdateLab(Guid id, Guid labId, [FromBody] LabResultInput input, CancellationToken ct) => _records.UpdateLabAsync(id, labId, input, ct);

    [HttpDelete("pets/{id:guid}/labs/{labId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> DeleteLab(Guid id, Guid labId, CancellationToken ct)
    {
        await _records.DeleteLabAsync(id, labId, ct);
        return NoContent();
    }

    // ---------- History / sealing / amendments ----------

    [HttpGet("pets/{id:guid}/record-seals")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<SealStateDto>> Seals(Guid id, CancellationToken ct) => _integrity.ListSealsAsync(id, ct);

    [HttpGet("pets/{id:guid}/records/{type}/{recordId:guid}/history")]
    [Permission(Permissions.Pets.View)]
    public Task<RecordHistoryDto> History(Guid id, HealthRecordType type, Guid recordId, CancellationToken ct) => _integrity.HistoryAsync(id, type, recordId, ct);

    [HttpPost("pets/{id:guid}/records/{type}/{recordId:guid}/seal")]
    [Permission(Permissions.Vets.Edit)]
    public Task<RecordSealDto> Seal(Guid id, HealthRecordType type, Guid recordId, CancellationToken ct) => _integrity.SealAsync(id, type, recordId, ct);

    [HttpPost("pets/{id:guid}/records/{type}/{recordId:guid}/amend")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> Amend(Guid id, HealthRecordType type, Guid recordId, [FromBody] AmendRecordInput input, CancellationToken ct)
    {
        await _integrity.AmendAsync(id, type, recordId, input, ct);
        return NoContent();
    }

    // ---------- Certificates / exports / insights ----------

    [HttpPost("pets/{id:guid}/vaccination-certificates")]
    [Permission(Permissions.Pets.View)]
    [EnableRateLimiting("writes")]
    public Task<CertificateIssueDto> IssueCertificate(Guid id, CancellationToken ct) => _certs.IssueAsync(id, ct);

    [HttpDelete("pets/{id:guid}/vaccination-certificates/{certId:guid}")]
    [Permission(Permissions.Pets.View)]
    public async Task<IActionResult> RevokeCertificate(Guid id, Guid certId, CancellationToken ct)
    {
        await _certs.RevokeAsync(id, certId, ct);
        return NoContent();
    }

    // Step-up guard for export actions is added centrally by another change; intentionally not added here.
    [HttpGet("pets/{id:guid}/export/fhir")]
    [Permission(Permissions.Pets.View)]
    [Produces("application/fhir+json")]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> ExportFhir(Guid id, CancellationToken ct)
        => Content(await _fhir.ExportAsync(id, ct), "application/fhir+json");

    [HttpGet("pets/{id:guid}/insights")]
    [Permission(Permissions.Pets.View)]
    public Task<PetInsightsDto> Insights(Guid id, CancellationToken ct) => _insights.GetAsync(id, ct);
}

/// <summary>Herd-level diagnosis/treatment with withdrawal-period tracking.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/livestock/treatments")]
[Authorize]
public class LivestockHealthController : ControllerBase
{
    private readonly ILivestockHealthService _svc;
    public LivestockHealthController(ILivestockHealthService svc) => _svc = svc;

    [HttpGet]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<LivestockTreatmentDto>> List([FromQuery] Guid? groupId, [FromQuery] Guid? petId, CancellationToken ct) => _svc.ListTreatmentsAsync(groupId, petId, ct);

    [HttpGet("withdrawal")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<WithdrawalStatusDto>> Withdrawal([FromQuery] Guid? groupId, CancellationToken ct) => _svc.WithdrawalStatusAsync(groupId, ct);

    [HttpPost]
    [Permission(Permissions.Pets.Edit)]
    public Task<LivestockTreatmentResultDto> Add([FromBody] LivestockTreatmentInput input, CancellationToken ct) => _svc.AddTreatmentAsync(input, ct);

    [HttpDelete("{id:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteTreatmentAsync(id, ct);
        return NoContent();
    }
}
