using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Vet.Dtos;
using Pawzaroo.Application.Modules.Vet.Services;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vets")]
public class VetsController : ControllerBase
{
    private readonly IVetDoctorService _doctors;
    private readonly IDoctorAvailabilityService _availability;
    private readonly IDoctorReviewService _reviews;

    public VetsController(IVetDoctorService doctors, IDoctorAvailabilityService availability, IDoctorReviewService reviews)
    {
        _doctors = doctors;
        _availability = availability;
        _reviews = reviews;
    }

    // ---------- Discovery ----------

    [HttpGet]
    [AllowAnonymous]
    public Task<CursorPage<DoctorSummaryDto>> Search(
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20,
        [FromQuery] AnimalType? animalType = null, [FromQuery] string? specialty = null,
        [FromQuery] Guid? specialtyId = null, [FromQuery] string? city = null,
        [FromQuery] ConsultationType? type = null, [FromQuery] decimal? maxPrice = null,
        [FromQuery] decimal? minRating = null, [FromQuery] bool? availableThisWeek = null,
        [FromQuery] string? sort = null, CancellationToken ct = default)
        => _doctors.SearchAsync(new DoctorSearchInput(cursor, pageSize, animalType, specialty,
            specialtyId, city, type, maxPrice, minRating, availableThisWeek, sort), ct);

    [HttpGet("specialties")]
    [AllowAnonymous]
    public Task<IReadOnlyList<SpecialtyDto>> Specialties(CancellationToken ct) => _doctors.ListSpecialtiesAsync(ct);

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<DoctorDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var d = await _doctors.GetAsync(id, ct);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<DoctorDetailDto>> Mine(CancellationToken ct)
    {
        var d = await _doctors.GetMyProfileAsync(ct);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpGet("{id:guid}/reviews")]
    [AllowAnonymous]
    public Task<IReadOnlyList<object>> Reviews(Guid id, CancellationToken ct)
        => _reviews.ListForDoctorAsync(id, ct);

    [HttpGet("{id:guid}/slots")]
    [AllowAnonymous]
    public Task<IReadOnlyList<TimeSlotDto>> Slots(Guid id,
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] ConsultationType? type = null, CancellationToken ct = default)
        => _availability.ListSlotsAsync(id, from, to, type, availableOnly: true, ct);

    // ---------- Doctor registration / profile ----------

    [HttpPost("register")]
    [Authorize]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> Register([FromBody] RegisterDoctorInput input, CancellationToken ct)
    {
        var id = await _doctors.RegisterAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id, version = "1.0" }, new { id });
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> Update([FromBody] UpdateDoctorInput input, CancellationToken ct)
    {
        await _doctors.UpdateAsync(input, ct);
        return NoContent();
    }

    // ---------- Credentials ----------

    [HttpPost("me/credentials")]
    [Authorize]
    public async Task<IActionResult> AddCredential([FromBody] AddCredentialInput input, CancellationToken ct)
    {
        var id = await _doctors.AddCredentialAsync(input, ct);
        return Ok(new { id });
    }

    [HttpGet("me/credentials")]
    [Authorize]
    public Task<IReadOnlyList<CredentialDocumentDto>> MyCredentials(CancellationToken ct) => _doctors.ListCredentialsAsync(null, ct);

    [HttpGet("{id:guid}/credentials")]
    [Authorize]
    [Permission(Permissions.Vets.Approve)]
    public Task<IReadOnlyList<CredentialDocumentDto>> Credentials(Guid id, CancellationToken ct)
        => _doctors.ListCredentialsAsync(id, ct);

    [HttpPost("credentials/{credentialId:guid}/verify")]
    [Authorize]
    [Permission(Permissions.Vets.Approve)]
    public async Task<IActionResult> VerifyCredential(Guid credentialId, [FromQuery] bool verified = true, CancellationToken ct = default)
    {
        await _doctors.VerifyCredentialAsync(credentialId, verified, ct);
        return NoContent();
    }

    // ---------- Admin moderation ----------

    [HttpPost("{id:guid}/approve")]
    [Authorize]
    [Permission(Permissions.Vets.Approve)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] DecisionInput input, CancellationToken ct)
    {
        await _doctors.ApproveAsync(id, input.AdminNotes, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize]
    [Permission(Permissions.Vets.Reject)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] DecisionInput input, CancellationToken ct)
    {
        await _doctors.RejectAsync(id, input.AdminNotes, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize]
    [Permission(Permissions.Vets.Suspend)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] DecisionInput input, CancellationToken ct)
    {
        await _doctors.SuspendAsync(id, input.AdminNotes, ct);
        return NoContent();
    }

    [HttpGet("admin/commission-report")]
    [Authorize]
    [Permission(Permissions.Reports.View)]
    public Task<IReadOnlyList<CommissionReportRow>> Commission([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        => _doctors.CommissionReportAsync(from, to, ct);

    // ---------- Availability (own) ----------

    [HttpGet("me/availability/rules")]
    [Authorize]
    public async Task<IReadOnlyList<AvailabilityRuleDto>> MyRules(CancellationToken ct)
    {
        var me = await _doctors.GetMyProfileAsync(ct);
        return me is null ? Array.Empty<AvailabilityRuleDto>() : await _availability.ListRulesAsync(me.Id, ct);
    }

    [HttpPost("me/availability/rules")]
    [Authorize]
    public async Task<IActionResult> AddRule([FromBody] AvailabilityRuleInput input, CancellationToken ct)
    {
        var id = await _availability.AddRuleAsync(input, ct);
        return Ok(new { id });
    }

    [HttpDelete("me/availability/rules/{ruleId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveRule(Guid ruleId, CancellationToken ct)
    {
        await _availability.RemoveRuleAsync(ruleId, ct);
        return NoContent();
    }

    [HttpGet("me/availability/holidays")]
    [Authorize]
    public async Task<IReadOnlyList<HolidayDto>> MyHolidays(CancellationToken ct)
    {
        var me = await _doctors.GetMyProfileAsync(ct);
        return me is null ? Array.Empty<HolidayDto>() : await _availability.ListHolidaysAsync(me.Id, ct);
    }

    [HttpPost("me/availability/holidays")]
    [Authorize]
    public async Task<IActionResult> AddHoliday([FromBody] HolidayInput input, CancellationToken ct)
    {
        var id = await _availability.AddHolidayAsync(input, ct);
        return Ok(new { id });
    }

    [HttpDelete("me/availability/holidays/{holidayId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveHoliday(Guid holidayId, CancellationToken ct)
    {
        await _availability.RemoveHolidayAsync(holidayId, ct);
        return NoContent();
    }

    [HttpPost("me/availability/generate-slots")]
    [Authorize]
    public async Task<IActionResult> Generate([FromBody] GenerateSlotsInput input, CancellationToken ct)
    {
        var count = await _availability.GenerateSlotsAsync(input, ct);
        return Ok(new { generated = count });
    }

    [HttpGet("me/slots")]
    [Authorize]
    public async Task<IReadOnlyList<TimeSlotDto>> MySlots(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] ConsultationType? type = null, [FromQuery] bool availableOnly = false, CancellationToken ct = default)
    {
        var me = await _doctors.GetMyProfileAsync(ct);
        return me is null ? Array.Empty<TimeSlotDto>() : await _availability.ListSlotsAsync(me.Id, from, to, type, availableOnly, ct);
    }

    [HttpPost("me/slots/{slotId:guid}/block")]
    [Authorize]
    public async Task<IActionResult> BlockSlot(Guid slotId, CancellationToken ct)
    {
        await _availability.BlockSlotAsync(slotId, ct);
        return NoContent();
    }

    [HttpPost("me/slots/{slotId:guid}/unblock")]
    [Authorize]
    public async Task<IActionResult> UnblockSlot(Guid slotId, CancellationToken ct)
    {
        await _availability.UnblockSlotAsync(slotId, ct);
        return NoContent();
    }
}
