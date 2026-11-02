using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Services;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Herds / flocks / litters, production logs and breeding records.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/livestock")]
[Authorize]
public class LivestockController : ControllerBase
{
    private readonly ILivestockService _svc;

    public LivestockController(ILivestockService svc) => _svc = svc;

    // ---------- Groups ----------

    [HttpGet("groups")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<PetGroupDto>> Groups(CancellationToken ct) => _svc.ListGroupsAsync(ct);

    [HttpPost("groups")]
    [Permission(Permissions.Pets.Create)]
    public Task<PetGroupDto> CreateGroup([FromBody] PetGroupInput input, CancellationToken ct) => _svc.CreateGroupAsync(input, ct);

    [HttpPut("groups/{id:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] PetGroupInput input, CancellationToken ct)
    {
        await _svc.UpdateGroupAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("groups/{id:guid}")]
    [Permission(Permissions.Pets.Delete)]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken ct)
    {
        await _svc.DeleteGroupAsync(id, ct);
        return NoContent();
    }

    [HttpPut("groups/{id:guid}/members")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> SetMembers(Guid id, [FromBody] GroupMembersInput input, CancellationToken ct)
    {
        await _svc.SetMembersAsync(id, input, ct);
        return NoContent();
    }

    [HttpPost("groups/{id:guid}/vaccinations")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> BulkVaccinate(Guid id, [FromBody] VaccinationInput input, CancellationToken ct)
        => Ok(new { recorded = await _svc.BulkVaccinateAsync(id, input, ct) });

    // ---------- Production ----------

    [HttpGet("production")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<ProductionLogDto>> Production([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => _svc.ListProductionAsync(from, to, ct);

    [HttpPost("production")]
    [Permission(Permissions.Pets.Edit)]
    public Task<ProductionLogDto> AddProduction([FromBody] ProductionLogInput input, CancellationToken ct) => _svc.AddProductionAsync(input, ct);

    [HttpDelete("production/{id:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> DeleteProduction(Guid id, CancellationToken ct)
    {
        await _svc.DeleteProductionAsync(id, ct);
        return NoContent();
    }

    // ---------- Breeding ----------

    [HttpGet("breeding")]
    [Permission(Permissions.Pets.View)]
    public Task<IReadOnlyList<BreedingRecordDto>> Breeding(CancellationToken ct) => _svc.ListBreedingAsync(ct);

    [HttpPost("breeding")]
    [Permission(Permissions.Pets.Edit)]
    public Task<BreedingRecordDto> AddBreeding([FromBody] BreedingRecordInput input, CancellationToken ct) => _svc.AddBreedingAsync(input, ct);

    [HttpPut("breeding/{id:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> UpdateBreeding(Guid id, [FromBody] BreedingRecordInput input, CancellationToken ct)
    {
        await _svc.UpdateBreedingAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("breeding/{id:guid}")]
    [Permission(Permissions.Pets.Edit)]
    public async Task<IActionResult> DeleteBreeding(Guid id, CancellationToken ct)
    {
        await _svc.DeleteBreedingAsync(id, ct);
        return NoContent();
    }
}
