using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Security;

/// <summary>
/// Warnings (strikes) and suspensions / bans. Mutating a user's disciplinary
/// record requires <c>users.suspend</c> / <c>users.ban</c> / <c>users.warn</c>;
/// the actor identity is captured for audit.
/// </summary>
public class UserDisciplineService : IUserDisciplineService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly INotificationService _notify;
    private readonly IAdminActionLogger _adminLog;

    public UserDisciplineService(ApplicationDbContext db, ICurrentUserService current,
        INotificationService notify, IAdminActionLogger adminLog)
    {
        _db = db;
        _current = current;
        _notify = notify;
        _adminLog = adminLog;
    }

    public async Task<UserSuspension> SuspendAsync(Guid userId, string reason, string? details,
        DateTime? expiresAt, bool isBan, CancellationToken ct = default)
    {
        var permRequired = isBan ? Permissions.Users.Suspend /* extend with Ban perm if added */
                                 : Permissions.Users.Suspend;
        EnsurePermission(permRequired);

        var actor = _current.UserId ?? throw new ForbiddenException();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User", userId);

        // Lift any previous active suspension first — we treat at most one open hold per user.
        var existing = await _db.UserSuspensions
            .Where(s => s.UserId == userId && s.Status == SuspensionStatus.Active)
            .ToListAsync(ct);
        foreach (var s in existing) { s.Status = SuspensionStatus.Lifted; s.LiftedAt = DateTime.UtcNow; s.LiftedById = actor; }

        var hold = new UserSuspension
        {
            UserId = userId,
            Reason = reason,
            Details = details,
            IsBan = isBan,
            ExpiresAt = expiresAt,
            IssuedById = actor,
            Status = SuspensionStatus.Active
        };
        _db.UserSuspensions.Add(hold);
        user.IsSuspended = true;
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyUserAsync(userId, isBan ? "Account banned" : "Account suspended",
            reason, new { hold.Id }, ct);
        await _adminLog.LogAsync(isBan ? "user.ban" : "user.suspend", "User", userId.ToString(),
            reason, new { hold.Id, expiresAt, isBan }, ct);
        return hold;
    }

    public async Task LiftAsync(Guid suspensionId, string? notes, CancellationToken ct = default)
    {
        EnsurePermission(Permissions.Users.Restore);
        var actor = _current.UserId ?? throw new ForbiddenException();
        var hold = await _db.UserSuspensions.FirstOrDefaultAsync(s => s.Id == suspensionId, ct)
                   ?? throw new NotFoundException("UserSuspension", suspensionId);
        if (hold.Status != SuspensionStatus.Active) return;

        hold.Status = SuspensionStatus.Lifted;
        hold.LiftedAt = DateTime.UtcNow;
        hold.LiftedById = actor;

        // Mark the user not-suspended if no other active hold exists.
        var stillSuspended = await _db.UserSuspensions
            .AnyAsync(s => s.UserId == hold.UserId && s.Status == SuspensionStatus.Active && s.Id != hold.Id, ct);
        if (!stillSuspended)
        {
            var user = await _db.Users.FirstAsync(u => u.Id == hold.UserId, ct);
            user.IsSuspended = false;
        }

        await _db.SaveChangesAsync(ct);
        await _notify.NotifyUserAsync(hold.UserId, "Account restored", notes ?? string.Empty, new { hold.Id }, ct);
        await _adminLog.LogAsync("user.restore", "User", hold.UserId.ToString(), notes, new { suspensionId }, ct);
    }

    public async Task<UserSuspensionDto?> GetActiveAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.UserSuspensions.AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == SuspensionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new UserSuspensionDto(s.Id, s.UserId, s.Reason, s.Details, s.IsBan,
                s.ExpiresAt, s.Status, s.IssuedById, s.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UserWarning> WarnAsync(Guid userId, WarningSeverity severity, string reason,
        string? message, string? relatedContentType, Guid? relatedContentId, CancellationToken ct = default)
    {
        EnsurePermission(Permissions.Users.Suspend);   // re-use; add Warn perm if you want finer control
        var actor = _current.UserId ?? throw new ForbiddenException();
        if (await _db.Users.AnyAsync(u => u.Id == userId, ct) == false)
            throw new NotFoundException("User", userId);

        var w = new UserWarning
        {
            UserId = userId,
            Severity = severity,
            Reason = reason,
            Message = message,
            RelatedContentType = relatedContentType,
            RelatedContentId = relatedContentId,
            IssuedById = actor
        };
        _db.UserWarnings.Add(w);
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyUserAsync(userId, "Community guideline notice",
            $"{severity}: {reason}", new { warningId = w.Id }, ct);
        await _adminLog.LogAsync("user.warn", "User", userId.ToString(),
            reason, new { severity = severity.ToString(), warningId = w.Id }, ct);
        return w;
    }

    public async Task AcknowledgeWarningAsync(Guid warningId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var w = await _db.UserWarnings.FirstOrDefaultAsync(x => x.Id == warningId && x.UserId == uid, ct)
                ?? throw new NotFoundException("UserWarning", warningId);
        w.AcknowledgedByUser = true;
        w.AcknowledgedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserWarningDto>> ListWarningsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.UserWarnings.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new UserWarningDto(w.Id, w.UserId, w.Severity, w.Reason, w.Message,
                w.AcknowledgedByUser, w.CreatedAt))
            .ToListAsync(ct);
    }

    private void EnsurePermission(string perm)
    {
        if (!_current.Permissions.Contains(perm)) throw new ForbiddenException();
    }
}
