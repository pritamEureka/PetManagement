using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Admin-only user management: search, detail, role grant/revoke, force logout.
/// Suspend / restore / warn live in <see cref="ModerationController"/>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
[Authorize]
public class UsersAdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAdminActionLogger _adminLog;

    public UsersAdminController(ApplicationDbContext db, IAdminActionLogger adminLog)
    {
        _db = db;
        _adminLog = adminLog;
    }

    public record UserSummary(
        Guid Id, string Email, string DisplayName, string? AvatarUrl,
        bool IsActive, bool IsSuspended, bool EmailConfirmed,
        DateTime CreatedAt, DateTime? LastLoginAt,
        IReadOnlyList<string> Roles);

    public record UserListResponse(IReadOnlyList<UserSummary> Items, long Total, int Page, int PageSize);

    [HttpGet]
    [Permission(Permissions.Users.View)]
    public async Task<UserListResponse> Search(
        [FromQuery] string? q,
        [FromQuery] string? role,
        [FromQuery] bool? suspended,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u => u.Email.Contains(q) || u.DisplayName.Contains(q));
        if (suspended.HasValue) query = query.Where(u => u.IsSuspended == suspended);
        if (active.HasValue)    query = query.Where(u => u.IsActive == active);
        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));

        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserSummary(
                u.Id, u.Email, u.DisplayName, u.AvatarUrl,
                u.IsActive, u.IsSuspended, u.EmailConfirmed,
                u.CreatedAt, u.LastLoginAt,
                u.UserRoles.Select(r => r.Role.Name).ToList()))
            .ToListAsync(ct);
        return new UserListResponse(items, total, page, pageSize);
    }

    public record UserDetailDto(
        Guid Id, string Email, string DisplayName, string? AvatarUrl, string? PhoneNumber,
        bool IsActive, bool IsSuspended, bool EmailConfirmed,
        DateTime CreatedAt, DateTime? LastLoginAt,
        IReadOnlyList<string> Roles,
        long PostCount, long OrderCount, long AdoptionListingCount);

    [HttpGet("{id:guid}")]
    [Permission(Permissions.Users.View)]
    public async Task<ActionResult<UserDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new UserDetailDto(
                x.Id, x.Email, x.DisplayName, x.AvatarUrl, x.PhoneNumber,
                x.IsActive, x.IsSuspended, x.EmailConfirmed,
                x.CreatedAt, x.LastLoginAt,
                x.UserRoles.Select(r => r.Role.Name).ToList(),
                _db.Posts.Count(p => p.AuthorId == x.Id),
                _db.Orders.Count(o => o.UserId == x.Id),
                _db.AdoptionListings.Count(l => l.OwnerId == x.Id)))
            .FirstOrDefaultAsync(ct);
        return u is null ? NotFound() : Ok(u);
    }

    public record GrantRoleBody(string RoleName);

    [HttpPost("{id:guid}/roles")]
    [Permission(Permissions.Roles.Assign)]
    public async Task<IActionResult> GrantRole(Guid id, [FromBody] GrantRoleBody body, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User", id);
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == body.RoleName, ct)
                    ?? throw new NotFoundException("Role", body.RoleName);

        var exists = await _db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct);
        if (!exists)
        {
            _db.UserRoles.Add(new Domain.Identity.UserRole { UserId = user.Id, RoleId = role.Id });
            await _db.SaveChangesAsync(ct);
        }
        await _adminLog.LogAsync("user.role.grant", "User", id.ToString(), null, new { body.RoleName }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/roles/{roleName}")]
    [Permission(Permissions.Roles.Assign)]
    public async Task<IActionResult> RevokeRole(Guid id, string roleName, CancellationToken ct)
    {
        var link = await _db.UserRoles.Include(ur => ur.Role)
            .FirstOrDefaultAsync(ur => ur.UserId == id && ur.Role.Name == roleName, ct);
        if (link is not null) { _db.UserRoles.Remove(link); await _db.SaveChangesAsync(ct); }
        await _adminLog.LogAsync("user.role.revoke", "User", id.ToString(), null, new { roleName }, ct);
        return NoContent();
    }

    /// <summary>Revoke every refresh token for the user — forces re-login on the next request.</summary>
    [HttpPost("{id:guid}/force-logout")]
    [Permission(Permissions.Users.Edit)]
    public async Task<IActionResult> ForceLogout(Guid id, CancellationToken ct)
    {
        var tokens = _db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null);
        await tokens.ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        await _adminLog.LogAsync("user.force_logout", "User", id.ToString(), null, null, ct);
        return NoContent();
    }
}
