using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lost-found")]
[Authorize]
public class LostFoundController : ControllerBase
{
    private readonly ILostFoundService _svc;

    public LostFoundController(ILostFoundService svc) => _svc = svc;

    [HttpGet]
    [Permission(Permissions.LostFound.View)]
    public Task<PagedLostFound> Search([FromQuery] LostFoundKind? kind, [FromQuery] AnimalType? animalType,
        [FromQuery] string? city, [FromQuery] LostFoundStatus? status, [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _svc.SearchAsync(new LostFoundQuery(kind, animalType, city, status, q, page, pageSize), ct);

    [HttpGet("mine")]
    [Permission(Permissions.LostFound.View)]
    public Task<IReadOnlyList<LostFoundSummaryDto>> Mine(CancellationToken ct) => _svc.MineAsync(ct);

    [HttpGet("{id:guid}")]
    [Permission(Permissions.LostFound.View)]
    public Task<LostFoundDetailDto> Get(Guid id, CancellationToken ct) => _svc.GetAsync(id, ct);

    [HttpGet("{id:guid}/matches")]
    [Permission(Permissions.LostFound.View)]
    public Task<IReadOnlyList<LostFoundMatchDto>> Matches(Guid id, CancellationToken ct) => _svc.MatchesAsync(id, ct);

    [HttpPost]
    [Permission(Permissions.LostFound.Create)]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> Create([FromBody] CreateLostFoundInput input, CancellationToken ct)
    {
        var report = await _svc.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = report.Report.Id, version = "1.0" }, report);
    }

    [HttpPost("{id:guid}/resolve")]
    [Permission(Permissions.LostFound.View)]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveLostFoundInput input, CancellationToken ct)
    {
        await _svc.ResolveAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Permission(Permissions.LostFound.View)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return NoContent();
    }
}
