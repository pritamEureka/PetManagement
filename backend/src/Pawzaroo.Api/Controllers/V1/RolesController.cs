using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;

namespace Pawzaroo.Api.Controllers.V1;

public record CreateRoleDto(string Name, string? Description, List<string> Permissions);
public record UpdateRoleDto(string? Description, List<string>? Permissions);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IPermissionService _perms;
    public RolesController(IPermissionService perms) => _perms = perms;

    [HttpGet]
    [Permission(Permissions.Roles.View)]
    public Task<IReadOnlyCollection<RoleDto>> List(CancellationToken ct)
        => _perms.ListRolesAsync(ct);

    [HttpGet("{id:guid}")]
    [Permission(Permissions.Roles.View)]
    public async Task<ActionResult<RoleDto>> Get(Guid id, CancellationToken ct)
    {
        var r = await _perms.GetRoleAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost]
    [Permission(Permissions.Roles.Create)]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto, CancellationToken ct)
    {
        var id = await _perms.CreateRoleAsync(dto.Name, dto.Description, dto.Permissions, ct);
        return CreatedAtAction(nameof(Get), new { id, version = "1.0" }, new { id });
    }

    [HttpPut("{id:guid}")]
    [Permission(Permissions.Roles.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleDto dto, CancellationToken ct)
    {
        await _perms.UpdateRoleAsync(id, dto.Description, dto.Permissions, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Permission(Permissions.Roles.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _perms.DeleteRoleAsync(id, ct);
        return NoContent();
    }
}
