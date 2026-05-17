using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Api.Controllers.V1;

public record AssignRoleDto(Guid UserId, Guid RoleId);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/user-roles")]
[Authorize]
public class UserRolesController : ControllerBase
{
    private readonly IPermissionService _perms;
    private readonly ApplicationDbContext _db;

    public UserRolesController(IPermissionService perms, ApplicationDbContext db)
    {
        _perms = perms;
        _db = db;
    }

    [HttpGet("user/{userId:guid}")]
    [Permission(Permissions.Roles.View)]
    public async Task<IActionResult> RolesForUser(Guid userId, CancellationToken ct)
    {
        var roles = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => new { ur.RoleId, ur.Role.Name, ur.AssignedAt })
            .ToListAsync(ct);
        var perms = await _perms.GetPermissionsForUserAsync(userId, ct);
        return Ok(new { userId, roles, permissions = perms });
    }

    [HttpPost("assign")]
    [Permission(Permissions.Roles.Assign)]
    public async Task<IActionResult> Assign([FromBody] AssignRoleDto dto, CancellationToken ct)
    {
        await _perms.AssignRoleToUserAsync(dto.UserId, dto.RoleId, ct);
        return NoContent();
    }

    [HttpPost("revoke")]
    [Permission(Permissions.Roles.Assign)]
    public async Task<IActionResult> Revoke([FromBody] AssignRoleDto dto, CancellationToken ct)
    {
        await _perms.RevokeRoleFromUserAsync(dto.UserId, dto.RoleId, ct);
        return NoContent();
    }
}
