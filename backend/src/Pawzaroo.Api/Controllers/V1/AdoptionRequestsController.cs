using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Adoption.Dtos;
using Pawzaroo.Application.Modules.Adoption.Services;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Api.Controllers.V1;

public record SetRequestStatusInput(AdoptionRequestStatus Status);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/adoption-requests")]
[Authorize]
public class AdoptionRequestsController : ControllerBase
{
    private readonly IAdoptionRequestService _requests;
    private readonly IAdoptionWantedPostService _wanted;

    public AdoptionRequestsController(IAdoptionRequestService requests, IAdoptionWantedPostService wanted)
    {
        _requests = requests;
        _wanted = wanted;
    }

    [HttpGet("mine")]
    public Task<IReadOnlyList<AdoptionRequestDto>> Mine(CancellationToken ct)
        => _requests.ListMineAsync(ct);

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        await _requests.WithdrawAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetRequestStatusInput input, CancellationToken ct)
    {
        await _requests.SetStatusAsync(id, input.Status, ct);
        return NoContent();
    }

    // ---------- Wanted posts ("I'm looking for a pet") ----------

    [HttpPost("wanted")]
    public async Task<IActionResult> CreateWanted([FromBody] CreateWantedPostInput input, CancellationToken ct)
    {
        var id = await _wanted.CreateAsync(input, ct);
        return Ok(new { id });
    }

    [HttpGet("wanted")]
    [AllowAnonymous]
    public Task<CursorPage<WantedPostDto>> SearchWanted(
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20,
        [FromQuery] AnimalType? animalType = null, [FromQuery] string? location = null,
        CancellationToken ct = default)
        => _wanted.SearchAsync(cursor, pageSize, animalType, location, ct);

    [HttpGet("wanted/mine")]
    public Task<IReadOnlyList<WantedPostDto>> MyWanted(CancellationToken ct)
        => _wanted.ListMineAsync(ct);
}
