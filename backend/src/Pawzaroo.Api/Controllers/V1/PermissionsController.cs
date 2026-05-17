using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/permissions")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _perms;
    private readonly ICurrentUserService _current;

    public PermissionsController(IPermissionService perms, ICurrentUserService current)
    {
        _perms = perms;
        _current = current;
    }

    [HttpGet]
    [Permission(Permissions.PermissionsAdmin.View)]
    public Task<IReadOnlyCollection<PermissionDto>> List(CancellationToken ct)
        => _perms.ListPermissionsAsync(ct);

    /// <summary>Returns the effective set for the calling user (post-cache, post-SuperAdmin expansion).</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new UnauthorizedAccessException();
        var perms = await _perms.GetPermissionsForUserAsync(uid, ct);
        return Ok(new { userId = uid, permissions = perms });
    }
}
