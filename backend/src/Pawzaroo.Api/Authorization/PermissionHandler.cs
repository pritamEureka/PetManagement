using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;

namespace Pawzaroo.Api.Authorization;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionResolver _resolver;

    public PermissionHandler(IPermissionResolver resolver) => _resolver = resolver;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // Fast path: permissions baked into JWT.
        if (context.User.Claims.Any(c => c.Type == "perm" && c.Value == requirement.Permission))
        {
            context.Succeed(requirement);
            return;
        }

        // Super-admin role short-circuit.
        if (context.User.IsInRole(SystemRoles.SuperAdmin))
        {
            context.Succeed(requirement);
            return;
        }

        // Slow path: hit DB / cache to load the user's effective permissions.
        var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId)) return;

        var perms = await _resolver.GetPermissionsForUserAsync(userId);
        if (perms.Contains(requirement.Permission)) context.Succeed(requirement);
    }
}
