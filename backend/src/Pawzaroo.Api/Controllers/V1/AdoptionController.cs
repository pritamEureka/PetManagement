using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Adoption.Dtos;
using Pawzaroo.Application.Modules.Adoption.Services;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/adoption")]
public class AdoptionController : ControllerBase
{
    private readonly IAdoptionListingService _listings;
    private readonly IAdoptionRequestService _requests;
    private readonly ISavedAdoptionService _saved;

    public AdoptionController(IAdoptionListingService listings, IAdoptionRequestService requests, ISavedAdoptionService saved)
    {
        _listings = listings;
        _requests = requests;
        _saved = saved;
    }

    // ---------- Listings: read ----------

    [HttpGet("listings")]
    [AllowAnonymous]
    public Task<CursorPage<AdoptionListingSummaryDto>> Public(
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20,
        [FromQuery] AnimalType? animalType = null, [FromQuery] string? breed = null,
        [FromQuery] AnimalSize? size = null, [FromQuery] Gender? gender = null,
        [FromQuery] string? location = null, [FromQuery] decimal? maxFee = null,
        [FromQuery] bool? vaccinatedOnly = null, [FromQuery] bool? neuteredOnly = null,
        [FromQuery] bool? goodWithChildren = null, [FromQuery] bool? goodWithOtherPets = null,
        [FromQuery] string? sort = null, CancellationToken ct = default)
        => _listings.SearchAsync(new AdoptionListingQuery(
            AdoptionListingScope.Public, cursor, pageSize, animalType, breed, size, gender,
            location, maxFee, vaccinatedOnly, neuteredOnly, goodWithChildren, goodWithOtherPets,
            Status: null, sort), ct);

    [HttpGet("listings/mine")]
    [Authorize]
    public Task<CursorPage<AdoptionListingSummaryDto>> Mine(
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20,
        [FromQuery] AdoptionListingStatus? status = null, CancellationToken ct = default)
        => _listings.SearchAsync(new AdoptionListingQuery(
            AdoptionListingScope.Mine, cursor, pageSize, Status: status), ct);

    [HttpGet("listings/{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<AdoptionListingDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var l = await _listings.GetByIdAsync(id, ct);
        return l is null ? NotFound() : Ok(l);
    }

    // ---------- Listings: mutations ----------

    [HttpPost("listings")]
    [Authorize]
    [Permission(Permissions.Adoption.Create)]
    [EnableRateLimiting("writes")]
    [Audit("Adoption", "create", entityName: "AdoptionListing", entityIdRouteKey: null)]
    public async Task<IActionResult> Create([FromBody] CreateAdoptionListingInput input, CancellationToken ct)
    {
        var id = await _listings.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id, version = "1.0" }, new { id });
    }

    [HttpPut("listings/{id:guid}")]
    [Authorize]
    [Permission(Permissions.Adoption.Edit)]
    [EnableRateLimiting("writes")]
    [Audit("Adoption", "update", entityName: "AdoptionListing")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAdoptionListingInput input, CancellationToken ct)
    {
        await _listings.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("listings/{id:guid}")]
    [Authorize]
    [Permission(Permissions.Adoption.Delete)]
    [Audit("Adoption", "delete", entityName: "AdoptionListing")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _listings.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("listings/{id:guid}/submit")]
    [Authorize]
    [Permission(Permissions.Adoption.Edit)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        await _listings.SubmitForApprovalAsync(id, ct);
        return NoContent();
    }

    [HttpPost("listings/{id:guid}/close")]
    [Authorize]
    [Permission(Permissions.Adoption.Edit)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await _listings.CloseAsync(id, ct);
        return NoContent();
    }

    [HttpPost("listings/{id:guid}/adopted")]
    [Authorize]
    [Permission(Permissions.Adoption.Edit)]
    [Audit("Adoption", "mark_adopted", entityName: "AdoptionListing")]
    public async Task<IActionResult> MarkAdopted(Guid id, [FromBody] MarkAdoptedInput input, CancellationToken ct)
    {
        await _listings.MarkAdoptedAsync(id, input.AdoptedByUserId, ct);
        return NoContent();
    }

    // ---------- Admin moderation ----------

    [HttpGet("admin/listings")]
    [Authorize]
    [Permission(Permissions.Adoption.View)]
    public Task<CursorPage<AdoptionListingSummaryDto>> AdminList(
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20,
        [FromQuery] AdoptionListingStatus? status = AdoptionListingStatus.PendingApproval,
        CancellationToken ct = default)
        => _listings.SearchAsync(new AdoptionListingQuery(
            Scope: status == AdoptionListingStatus.PendingApproval
                ? AdoptionListingScope.AdminPending
                : AdoptionListingScope.AdminAll,
            Cursor: cursor, PageSize: pageSize, Status: status), ct);

    [HttpPost("listings/{id:guid}/approve")]
    [Authorize]
    [Permission(Permissions.Adoption.Approve)]
    [Audit("Adoption", "approve", entityName: "AdoptionListing")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] AdminDecisionInput input, CancellationToken ct)
    {
        await _listings.ApproveAsync(id, input.AdminNotes, ct);
        return NoContent();
    }

    [HttpPost("listings/{id:guid}/reject")]
    [Authorize]
    [Permission(Permissions.Adoption.Reject)]
    [Audit("Adoption", "reject", entityName: "AdoptionListing")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] AdminDecisionInput input, CancellationToken ct)
    {
        await _listings.RejectAsync(id, input.AdminNotes, ct);
        return NoContent();
    }

    // ---------- Requests on a listing ----------

    [HttpGet("listings/{id:guid}/requests")]
    [Authorize]
    public Task<IReadOnlyList<AdoptionRequestDto>> ListingRequests(Guid id, CancellationToken ct)
        => _requests.ListForListingAsync(id, ct);

    [HttpPost("listings/{id:guid}/requests")]
    [Authorize]
    [Permission(Permissions.AdoptionRequests.Create)]
    [EnableRateLimiting("writes")]
    public async Task<ActionResult<AdoptionRequestDto>> ApplyForAdoption(Guid id, [FromBody] CreateAdoptionRequestInput input, CancellationToken ct)
        => Ok(await _requests.CreateAsync(id, input, ct));

    // ---------- Saved ----------

    [HttpPost("listings/{id:guid}/saved")]
    [Authorize]
    public async Task<IActionResult> ToggleSaved(Guid id, CancellationToken ct)
    {
        var saved = await _saved.ToggleAsync(id, ct);
        return Ok(new { saved });
    }

    [HttpGet("listings/saved")]
    [Authorize]
    public Task<CursorPage<AdoptionListingSummaryDto>> Saved(
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _saved.ListAsync(cursor, pageSize, ct);
}
