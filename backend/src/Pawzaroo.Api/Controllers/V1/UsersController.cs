using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;

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

    public UsersController(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
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
}
