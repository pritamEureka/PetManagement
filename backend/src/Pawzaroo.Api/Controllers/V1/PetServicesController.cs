using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.PetServices;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Grooming / training / boarding / walking providers and their bookings.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/pet-services")]
[Authorize]
public class PetServicesController : ControllerBase
{
    private readonly IPetServicesMarketplace _svc;

    public PetServicesController(IPetServicesMarketplace svc) => _svc = svc;

    // ---------- Discovery ----------

    [HttpGet("providers")]
    [Permission(Permissions.Services.View)]
    public Task<PagedProviders> Search([FromQuery] ServiceProviderType? type, [FromQuery] string? city,
        [FromQuery] AnimalType? animalType, [FromQuery] string? q, [FromQuery] string? sort,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _svc.SearchAsync(new ProviderQuery(type, city, animalType, q, sort, page, pageSize), ct);

    [HttpGet("providers/{id:guid}")]
    [Permission(Permissions.Services.View)]
    public async Task<ActionResult<ProviderDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var p = await _svc.GetAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    // ---------- Provider self-service ----------

    [HttpGet("providers/me")]
    [Permission(Permissions.Services.Edit)]
    public async Task<ActionResult<ProviderDetailDto>> Mine(CancellationToken ct)
    {
        var p = await _svc.GetMineAsync(ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost("providers")]
    [Permission(Permissions.Services.Edit)]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> Register([FromBody] RegisterProviderInput input, CancellationToken ct)
    {
        var id = await _svc.RegisterAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id, version = "1.0" }, new { id });
    }

    [HttpPut("providers/me")]
    [Permission(Permissions.Services.Edit)]
    public async Task<IActionResult> UpdateMine([FromBody] RegisterProviderInput input, CancellationToken ct)
    {
        await _svc.UpdateMineAsync(input, ct);
        return NoContent();
    }

    [HttpGet("bookings/incoming")]
    [Permission(Permissions.Services.Edit)]
    public Task<IReadOnlyList<BookingDto>> Incoming([FromQuery] AppointmentStatus? status, CancellationToken ct)
        => _svc.IncomingBookingsAsync(status, ct);

    [HttpPost("bookings/{id:guid}/confirm")]
    [Permission(Permissions.Services.Edit)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        await _svc.ConfirmAsync(id, ct);
        return NoContent();
    }

    [HttpPost("bookings/{id:guid}/complete")]
    [Permission(Permissions.Services.Edit)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await _svc.CompleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("bookings/{id:guid}/mark-paid")]
    [Permission(Permissions.Services.Edit)]
    public async Task<IActionResult> MarkPaid(Guid id, CancellationToken ct)
    {
        await _svc.MarkPaidAsync(id, ct);
        return NoContent();
    }

    // ---------- Customer bookings ----------

    [HttpPost("bookings")]
    [Permission(Permissions.Services.Book)]
    [EnableRateLimiting("writes")]
    public Task<BookingDto> Book([FromBody] CreateBookingInput input, CancellationToken ct) => _svc.BookAsync(input, ct);

    [HttpGet("bookings/mine")]
    [Permission(Permissions.Services.View)]
    public Task<IReadOnlyList<BookingDto>> MyBookings(CancellationToken ct) => _svc.MyBookingsAsync(ct);

    [HttpPost("bookings/{id:guid}/cancel")]
    [Permission(Permissions.Services.View)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelBookingInput input, CancellationToken ct)
    {
        await _svc.CancelAsync(id, input, ct);
        return NoContent();
    }

    [HttpPost("bookings/{id:guid}/review")]
    [Permission(Permissions.Services.Book)]
    public async Task<IActionResult> Review(Guid id, [FromBody] ServiceReviewInput input, CancellationToken ct)
    {
        await _svc.ReviewAsync(id, input, ct);
        return NoContent();
    }

    // ---------- Admin ----------

    [HttpGet("admin/providers")]
    [Permission(Permissions.Services.Approve)]
    public Task<PagedProviders> AdminList([FromQuery] ApprovalStatus status = ApprovalStatus.Pending,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _svc.AdminListAsync(status, page, pageSize, ct);

    [HttpPost("admin/providers/{id:guid}/approve")]
    [Permission(Permissions.Services.Approve)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ProviderDecisionInput input, CancellationToken ct)
    {
        await _svc.DecideAsync(id, ApprovalStatus.Approved, input.Notes, ct);
        return NoContent();
    }

    [HttpPost("admin/providers/{id:guid}/reject")]
    [Permission(Permissions.Services.Reject)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ProviderDecisionInput input, CancellationToken ct)
    {
        await _svc.DecideAsync(id, ApprovalStatus.Rejected, input.Notes, ct);
        return NoContent();
    }

    [HttpPost("admin/providers/{id:guid}/suspend")]
    [Permission(Permissions.Services.Suspend)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] ProviderDecisionInput input, CancellationToken ct)
    {
        await _svc.DecideAsync(id, ApprovalStatus.Suspended, input.Notes, ct);
        return NoContent();
    }
}
