using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Public-ish directory of users for signed-in callers. Returns only the minimal
/// identity needed for "Message / View profile" surfaces (id, displayName,
/// avatarUrl, primary role) — explicitly excludes email, phone, last-login,
/// suspension state, and admin metadata, which live on the admin endpoint
/// <see cref="UsersAdminController"/>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPasswordHasher _hasher;

    public UsersController(ApplicationDbContext db, ICurrentUserService current, IPasswordHasher hasher)
    {
        _db = db;
        _current = current;
        _hasher = hasher;
    }

    public record PublicUserDto(Guid Id, string DisplayName, string? AvatarUrl, string? PrimaryRole);
    public record PublicUserListResponse(int Total, IReadOnlyList<PublicUserDto> Items);

    /// <summary>
    /// Search the user directory. Excludes the caller, pending/rejected accounts,
    /// suspended accounts, and inactive accounts.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PublicUserListResponse>> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 25 : pageSize;

        var me = _current.UserId;
        var query = _db.Users.AsNoTracking()
            .Where(u => u.IsActive && !u.IsSuspended
                        && u.ApprovalStatus == ApprovalStatus.Approved
                        && u.Id != me);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            // EF.Functions.ILike → case-insensitive in Postgres; falls back to
            // ToLower comparison on providers that don't support ILike.
            query = query.Where(u => EF.Functions.ILike(u.DisplayName, like));
        }

        var total = await query.CountAsync(ct);

        // Pull a single "primary role" for context labels — picks whichever role
        // sorts first by name (stable + cheap; the UI just uses it as a chip).
        var items = await query
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new PublicUserDto(
                u.Id,
                u.DisplayName,
                u.AvatarUrl,
                _db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Select(ur => ur.Role.Name)
                    .OrderBy(n => n)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return Ok(new PublicUserListResponse(total, items));
    }

    // ---------- Self-service profile (/users/me) ----------

    public record MyProfileDto(
        Guid Id,
        string Email,
        string DisplayName,
        string? PhoneNumber,
        string? AvatarUrl,
        string? Bio,
        string? Location,
        IReadOnlyList<string> Roles);

    /// <summary>Read the signed-in user's own profile (includes email/phone — admin fields stay on the admin surface).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<MyProfileDto>> Me(CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException("Not authenticated.");
        var dto = await _db.Users.AsNoTracking()
            .Where(u => u.Id == uid)
            .Select(u => new MyProfileDto(
                u.Id, u.Email, u.DisplayName, u.PhoneNumber, u.AvatarUrl, u.Bio, u.Location,
                u.UserRoles.Select(r => r.Role.Name).ToList()))
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    public record UpdateMyProfileBody(
        string? DisplayName,
        string? Email,
        string? PhoneNumber,
        string? AvatarUrl,
        string? Bio,
        string? Location);

    /// <summary>
    /// Update the signed-in user's own profile. Email changes require a
    /// uniqueness check; an empty/blank field is treated as "leave alone"
    /// (use null to skip, "" to clear nullable fields like bio/location/phone).
    /// </summary>
    [HttpPut("me")]
    public async Task<ActionResult<MyProfileDto>> UpdateMe([FromBody] UpdateMyProfileBody body, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException("Not authenticated.");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid, ct)
                   ?? throw new NotFoundException("User", uid);

        var errors = new Dictionary<string, string[]>();

        if (body.DisplayName is not null)
        {
            var name = body.DisplayName.Trim();
            if (name.Length == 0) errors["displayName"] = new[] { "Display name is required." };
            else if (name.Length > 128) errors["displayName"] = new[] { "Display name must be 128 characters or fewer." };
            else user.DisplayName = name;
        }

        if (body.Email is not null)
        {
            var email = body.Email.Trim().ToLowerInvariant();
            if (email.Length == 0 || !email.Contains('@'))
                errors["email"] = new[] { "A valid email address is required." };
            else if (email.Length > 256)
                errors["email"] = new[] { "Email must be 256 characters or fewer." };
            else if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var taken = await _db.Users.IgnoreQueryFilters()
                    .AnyAsync(u => u.Id != uid && u.Email == email, ct);
                if (taken) errors["email"] = new[] { "That email address is already in use." };
                else { user.Email = email; user.EmailConfirmed = false; }
            }
        }

        if (body.PhoneNumber is not null)
            user.PhoneNumber = string.IsNullOrWhiteSpace(body.PhoneNumber) ? null : body.PhoneNumber.Trim();

        if (body.AvatarUrl is not null)
            user.AvatarUrl = string.IsNullOrWhiteSpace(body.AvatarUrl) ? null : body.AvatarUrl.Trim();

        if (body.Bio is not null)
            user.Bio = string.IsNullOrWhiteSpace(body.Bio) ? null : body.Bio.Trim();

        if (body.Location is not null)
            user.Location = string.IsNullOrWhiteSpace(body.Location) ? null : body.Location.Trim();

        if (errors.Count > 0) throw new ValidationException(errors);

        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = uid;
        await _db.SaveChangesAsync(ct);

        return Ok(new MyProfileDto(
            user.Id, user.Email, user.DisplayName, user.PhoneNumber, user.AvatarUrl, user.Bio, user.Location,
            await _db.UserRoles.AsNoTracking()
                .Where(ur => ur.UserId == uid)
                .Select(ur => ur.Role.Name)
                .ToListAsync(ct)));
    }

    public record ChangePasswordBody(string CurrentPassword, string NewPassword);

    /// <summary>
    /// Self-service password change. Requires the current password as a
    /// re-auth gate (mitigates session-hijack damage). The new password is
    /// held to the same strength rules as registration.
    /// </summary>
    [HttpPost("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordBody body, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException("Not authenticated.");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid, ct)
                   ?? throw new NotFoundException("User", uid);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(body.CurrentPassword))
            errors["currentPassword"] = new[] { "Current password is required." };
        if (string.IsNullOrEmpty(body.NewPassword) || body.NewPassword.Length < 8 || body.NewPassword.Length > 128)
            errors["newPassword"] = new[] { "Password must be between 8 and 128 characters." };
        else if (!body.NewPassword.Any(char.IsUpper) || !body.NewPassword.Any(char.IsLower) || !body.NewPassword.Any(char.IsDigit))
            errors["newPassword"] = new[] { "Password must contain upper- and lower-case letters and at least one digit." };

        if (errors.Count > 0) throw new ValidationException(errors);

        if (!_hasher.Verify(body.CurrentPassword, user.PasswordHash))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["currentPassword"] = new[] { "Current password is incorrect." }
            });

        user.PasswordHash = _hasher.Hash(body.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = uid;

        // Invalidate every other session so a stolen refresh token can't survive
        // a password change. The current request stays on its access token until
        // it expires (~15 min default) — the client should re-login on next refresh.
        var liveTokens = _db.RefreshTokens.Where(t => t.UserId == uid && t.RevokedAt == null);
        await liveTokens.ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
