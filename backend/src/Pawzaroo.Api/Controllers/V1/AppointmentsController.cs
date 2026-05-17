using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Application.Modules.Vet.Dtos;
using Pawzaroo.Application.Modules.Vet.Services;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/appointments")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointments;
    private readonly IPrescriptionService _prescriptions;
    private readonly IDoctorReviewService _reviews;

    public AppointmentsController(IAppointmentService appointments, IPrescriptionService prescriptions, IDoctorReviewService reviews)
    {
        _appointments = appointments;
        _prescriptions = prescriptions;
        _reviews = reviews;
    }

    // ---------- Read ----------

    [HttpGet("mine")]
    public Task<CursorPage<AppointmentSummaryDto>> Mine(
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20,
        [FromQuery] AppointmentStatus? status = null, CancellationToken ct = default)
        => _appointments.ListAsync(AppointmentScope.Mine, cursor, pageSize, status, ct);

    [HttpGet("clinic")]
    public Task<CursorPage<AppointmentSummaryDto>> Clinic(
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20,
        [FromQuery] AppointmentStatus? status = null, CancellationToken ct = default)
        => _appointments.ListAsync(AppointmentScope.ForMyClinic, cursor, pageSize, status, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AppointmentSummaryDto>> Get(Guid id, CancellationToken ct)
    {
        var a = await _appointments.GetAsync(id, ct);
        return a is null ? NotFound() : Ok(a);
    }

    // ---------- Booking + status transitions ----------

    [HttpPost("book")]
    [EnableRateLimiting("writes")]
    public async Task<ActionResult<AppointmentSummaryDto>> Book([FromBody] BookAppointmentInput input, CancellationToken ct)
        => Ok(await _appointments.BookAsync(input, ct));

    [HttpPost("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleInput input, CancellationToken ct)
    {
        await _appointments.RescheduleAsync(id, input, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelByUser(Guid id, [FromBody] CancelInput input, CancellationToken ct)
    {
        await _appointments.CancelByUserAsync(id, input, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel-by-doctor")]
    public async Task<IActionResult> CancelByDoctor(Guid id, [FromBody] CancelInput input, CancellationToken ct)
    {
        await _appointments.CancelByDoctorAsync(id, input, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        await _appointments.ConfirmAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await _appointments.CompleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/no-show")]
    public async Task<IActionResult> NoShow(Guid id, CancellationToken ct)
    {
        await _appointments.MarkNoShowAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id, CancellationToken ct)
    {
        await _appointments.MarkPaidAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<IActionResult> Refund(Guid id, CancellationToken ct)
    {
        await _appointments.RefundAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/meeting-link")]
    public async Task<IActionResult> MeetingLink(Guid id, CancellationToken ct)
        => Ok(new { url = await _appointments.EnsureMeetingLinkAsync(id, ct) });

    // ---------- Prescription + follow-up ----------

    [HttpPost("{id:guid}/prescription")]
    public async Task<IActionResult> Prescription(Guid id, [FromBody] PrescriptionInput input, CancellationToken ct)
    {
        var rxId = await _prescriptions.UploadAsync(id, input, ct);
        return Ok(new { id = rxId });
    }

    [HttpPost("{id:guid}/follow-up")]
    public async Task<IActionResult> FollowUp(Guid id, [FromBody] FollowUpInput input, CancellationToken ct)
    {
        await _prescriptions.AddFollowUpAsync(id, input, ct);
        return NoContent();
    }

    [HttpGet("my-prescriptions")]
    public Task<IReadOnlyList<object>> MyPrescriptions(CancellationToken ct) => _prescriptions.ListForPatientAsync(ct);

    // ---------- Reviews ----------

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewInput input, CancellationToken ct)
    {
        var rid = await _reviews.SubmitAsync(id, input, ct);
        return Ok(new { id = rid });
    }
}
