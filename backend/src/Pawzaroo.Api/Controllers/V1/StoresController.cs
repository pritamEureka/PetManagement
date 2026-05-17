using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stores")]
public class StoresController : ControllerBase
{
    private readonly IStoreService _stores;
    private readonly IStoreOwnerProfileService _kyc;
    private readonly IStoreReviewService _reviews;
    private readonly ISalesReportService _reports;

    public StoresController(IStoreService stores, IStoreOwnerProfileService kyc,
        IStoreReviewService reviews, ISalesReportService reports)
    {
        _stores = stores;
        _kyc = kyc;
        _reviews = reviews;
        _reports = reports;
    }

    // ----- KYC (store owner onboarding) ---------------------------------------

    [HttpGet("owner/profile")]
    [Authorize]
    public Task<StoreOwnerProfileDto?> MyKyc(CancellationToken ct) => _kyc.GetMineAsync(ct);

    [HttpPost("owner/profile")]
    [Authorize]
    public Task<StoreOwnerProfileDto> SubmitKyc([FromBody] SubmitStoreOwnerProfileInput input, CancellationToken ct)
        => _kyc.SubmitAsync(input, ct);

    [HttpGet("admin/owner-profiles")]
    [Permission(Permissions.Sellers.View)]
    public Task<PageResult<StoreOwnerProfileDto>> AdminListKyc(
        [FromQuery] ApprovalStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) => _kyc.ListForAdminAsync(status, page, pageSize, ct);

    [HttpPost("admin/owner-profiles/{id:guid}/approve")]
    [Permission(Permissions.Sellers.Approve)]
    public async Task<IActionResult> AdminApproveKyc(Guid id, [FromBody] AdminNotes? body, CancellationToken ct)
    {
        await _kyc.ApproveAsync(id, body?.Notes, ct);
        return NoContent();
    }

    [HttpPost("admin/owner-profiles/{id:guid}/reject")]
    [Permission(Permissions.Sellers.Reject)]
    public async Task<IActionResult> AdminRejectKyc(Guid id, [FromBody] AdminNotes? body, CancellationToken ct)
    {
        await _kyc.RejectAsync(id, body?.Notes, ct);
        return NoContent();
    }

    // ----- Store CRUD ---------------------------------------------------------

    [HttpGet]
    [AllowAnonymous]
    public Task<PageResult<StoreDto>> Search(
        [FromQuery] string? search, [FromQuery] ApprovalStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _stores.SearchAsync(search, status, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<StoreDto>> Get(Guid id, CancellationToken ct)
    {
        var s = await _stores.GetByIdAsync(id, ct);
        return s is null ? NotFound() : Ok(s);
    }

    [HttpGet("mine")]
    [Authorize]
    public Task<StoreDto?> Mine(CancellationToken ct) => _stores.GetMineAsync(ct);

    [HttpPost("register")]
    [Authorize]
    public async Task<IActionResult> Register([FromBody] RegisterStoreInput input, CancellationToken ct)
    {
        var id = await _stores.RegisterAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id, version = "1.0" }, new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStoreInput input, CancellationToken ct)
    {
        await _stores.UpdateAsync(id, input, ct);
        return NoContent();
    }

    // ----- Admin moderation ---------------------------------------------------

    [HttpPost("{id:guid}/approve")]
    [Permission(Permissions.Stores.Approve)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] AdminNotes? body, CancellationToken ct)
    {
        await _stores.ApproveAsync(id, body?.Notes, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [Permission(Permissions.Stores.Reject)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] AdminNotes? body, CancellationToken ct)
    {
        await _stores.RejectAsync(id, body?.Notes, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/suspend")]
    [Permission(Permissions.Stores.Suspend)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] AdminNotes? body, CancellationToken ct)
    {
        await _stores.SuspendAsync(id, body?.Notes, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [Permission(Permissions.Stores.Restore)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        await _stores.RestoreAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/feature")]
    [Permission(Permissions.Stores.Feature)]
    public async Task<IActionResult> Feature(Guid id, [FromQuery] bool value = true, CancellationToken ct = default)
    {
        await _stores.SetFeaturedAsync(id, value, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/commission")]
    [Permission(Permissions.Stores.Approve)]
    public async Task<IActionResult> SetCommission(Guid id, [FromBody] CommissionPayload body, CancellationToken ct)
    {
        await _stores.SetCommissionPercentAsync(id, body.Percent, ct);
        return NoContent();
    }

    // ----- Reviews ------------------------------------------------------------

    [HttpGet("{id:guid}/reviews")]
    [AllowAnonymous]
    public Task<PageResult<StoreReviewDto>> ListReviews(Guid id,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _reviews.ListAsync(id, page, pageSize, ct);

    [HttpPost("{id:guid}/reviews")]
    [Authorize]
    public Task<StoreReviewDto> CreateReview(Guid id, [FromBody] CreateStoreReviewInput input, CancellationToken ct)
        => _reviews.CreateAsync(id, input, ct);

    // ----- Sales report -------------------------------------------------------

    [HttpGet("{id:guid}/report")]
    [Authorize]
    public Task<StoreSalesReportDto> Report(Guid id,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
        => _reports.ForStoreAsync(id,
            from ?? DateTime.UtcNow.AddDays(-30),
            to ?? DateTime.UtcNow.AddDays(1), ct);

    public record AdminNotes(string? Notes);
    public record CommissionPayload(decimal Percent);
}
