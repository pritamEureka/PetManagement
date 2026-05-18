using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Admin-only user management: search, detail, role grant/revoke, force logout,
/// and registration approval. Suspend / restore / warn live in
/// <see cref="ModerationController"/>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
[Authorize]
public class UsersAdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAdminActionLogger _adminLog;
    private readonly IEmailService _email;
    private readonly INotificationService _notify;
    private readonly ICurrentUserService _current;

    public UsersAdminController(ApplicationDbContext db, IAdminActionLogger adminLog,
        IEmailService email, INotificationService notify, ICurrentUserService current)
    {
        _db = db;
        _adminLog = adminLog;
        _email = email;
        _notify = notify;
        _current = current;
    }

    public record UserSummary(
        Guid Id, string Email, string DisplayName, string? AvatarUrl,
        bool IsActive, bool IsSuspended, bool EmailConfirmed,
        ApprovalStatus ApprovalStatus,
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
        [FromQuery] ApprovalStatus? approvalStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u => u.Email.Contains(q) || u.DisplayName.Contains(q));
        if (suspended.HasValue) query = query.Where(u => u.IsSuspended == suspended);
        if (active.HasValue)    query = query.Where(u => u.IsActive == active);
        if (approvalStatus.HasValue)
            query = query.Where(u => u.ApprovalStatus == approvalStatus);
        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));

        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserSummary(
                u.Id, u.Email, u.DisplayName, u.AvatarUrl,
                u.IsActive, u.IsSuspended, u.EmailConfirmed,
                u.ApprovalStatus,
                u.CreatedAt, u.LastLoginAt,
                u.UserRoles.Select(r => r.Role.Name).ToList()))
            .ToListAsync(ct);
        return new UserListResponse(items, total, page, pageSize);
    }

    public record UserDetailDto(
        Guid Id, string Email, string DisplayName, string? AvatarUrl, string? PhoneNumber,
        bool IsActive, bool IsSuspended, bool EmailConfirmed,
        ApprovalStatus ApprovalStatus, string? RejectionReason,
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
                x.ApprovalStatus, x.RejectionReason,
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
        // Admin / SuperAdmin escalation must come from a SuperAdmin. Other roles
        // (Moderator, StoreOwner, etc.) ride on the standard roles.assign permission.
        if (body.RoleName is SystemRoles.Admin or SystemRoles.SuperAdmin && !User.IsInRole(SystemRoles.SuperAdmin))
            throw new ForbiddenException("Only a SuperAdmin can grant Admin or SuperAdmin roles.");

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

    // ---------- Registration approval ----------

    public record ApproveBody(string? Notes);
    public record RejectBody(string Reason);

    [HttpPost("{id:guid}/approve")]
    [Permission(Permissions.Users.Approve)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveBody? body, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException("User", id);

        if (user.ApprovalStatus == ApprovalStatus.Approved)
            return NoContent();

        user.ApprovalStatus = ApprovalStatus.Approved;
        user.ApprovedAt = DateTime.UtcNow;
        user.ApprovedById = _current.UserId;
        user.RejectionReason = null;
        await _db.SaveChangesAsync(ct);

        await _email.SendAsync(user.Email,
            "Your Pawzaroo account is approved",
            $"Hi {user.DisplayName},\n\nGood news — your account has been approved by an administrator. " +
            "You can now log in at the Pawzaroo sign-in page.\n\n— Pawzaroo",
            user.Id, ct);

        try { await _notify.NotifyUserAsync(user.Id, "Account approved", "You can sign in now.", null, ct); }
        catch { /* notification is non-critical */ }

        await _adminLog.LogAsync("user.approve", "User", id.ToString(), body?.Notes, null, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [Permission(Permissions.Users.Reject)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Reason))
            throw new Pawzaroo.Shared.Exceptions.ValidationException(
                new Dictionary<string, string[]> { ["reason"] = new[] { "Reason is required." } });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException("User", id);

        user.ApprovalStatus = ApprovalStatus.Rejected;
        user.RejectionReason = body.Reason.Trim();
        user.ApprovedAt = null;
        user.ApprovedById = _current.UserId;
        await _db.SaveChangesAsync(ct);

        await _email.SendAsync(user.Email,
            "Your Pawzaroo registration was not approved",
            $"Hi {user.DisplayName},\n\nWe're unable to approve your account at this time.\n\nReason: {user.RejectionReason}\n\n— Pawzaroo",
            user.Id, ct);

        await _adminLog.LogAsync("user.reject", "User", id.ToString(), body.Reason, null, ct);
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
