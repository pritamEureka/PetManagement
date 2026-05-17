using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Moderation;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Security;

/// <summary>
/// Centralized moderator workbench. One entry point handles every action so
/// the audit trail (ModerationAction row + optional AdminActionLog) is
/// always written in one place. Side-effects per action:
///
///   Warn        -> create UserWarning, notify user
///   Suspend     -> create UserSuspension (timed), notify user
///   Ban         -> create UserSuspension (permanent), notify user
///   Hide        -> flip IsActive/IsDeleted on the target
///   Restore     -> flip back
///   Approve     -> push the target's ApprovalStatus to Approved
///   Reject      -> push to Rejected
///   MarkSusp.   -> tag the target (annotation only)
///   Escalate    -> raise notification to super-admins
/// </summary>
public class ModerationService : IModerationService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IUserDisciplineService _discipline;
    private readonly INotificationService _notify;
    private readonly IAdminActionLogger _adminLog;

    public ModerationService(ApplicationDbContext db, ICurrentUserService current,
        IUserDisciplineService discipline, INotificationService notify, IAdminActionLogger adminLog)
    {
        _db = db;
        _current = current;
        _discipline = discipline;
        _notify = notify;
        _adminLog = adminLog;
    }

    public async Task<ModerationActionDto> TakeActionAsync(ModerationActionInput input, CancellationToken ct = default)
    {
        EnsurePermission(input.Action);
        var moderatorId = _current.UserId ?? throw new ForbiddenException();

        Guid? warningId = null, suspensionId = null;

        switch (input.Action)
        {
            case ModerationActionType.Warn:
                if (input.TargetType != ModerationTargetType.User)
                    throw new ValidationException(new Dictionary<string, string[]>
                        { ["targetType"] = new[] { "Warn applies to users only." } });
                var w = await _discipline.WarnAsync(input.TargetId,
                    input.WarningSeverity ?? WarningSeverity.Minor,
                    input.Notes ?? "Community guideline violation",
                    null,
                    input.ReportId.HasValue ? "ContentReport" : null,
                    input.ReportId, ct);
                warningId = w.Id;
                break;

            case ModerationActionType.Suspend:
                var s = await _discipline.SuspendAsync(input.TargetId,
                    input.Notes ?? "Suspended by moderation", null,
                    input.SuspendUntil ?? DateTime.UtcNow.AddDays(7), isBan: false, ct);
                suspensionId = s.Id;
                break;

            case ModerationActionType.Ban:
                var b = await _discipline.SuspendAsync(input.TargetId,
                    input.Notes ?? "Banned by moderation", null,
                    expiresAt: null, isBan: true, ct);
                suspensionId = b.Id;
                break;

            case ModerationActionType.Hide:
                await SetContentVisibilityAsync(input.TargetType, input.TargetId, visible: false, ct);
                break;
            case ModerationActionType.Restore:
            case ModerationActionType.Unhide:
                await SetContentVisibilityAsync(input.TargetType, input.TargetId, visible: true, ct);
                break;

            case ModerationActionType.Approve:
                await SetApprovalAsync(input.TargetType, input.TargetId, ApprovalStatus.Approved, ct);
                break;
            case ModerationActionType.Reject:
                await SetApprovalAsync(input.TargetType, input.TargetId, ApprovalStatus.Rejected, ct);
                break;

            case ModerationActionType.MarkSuspicious:
                // No state change beyond the action record; future jobs may pick this up.
                break;

            case ModerationActionType.Escalate:
                await _notify.BroadcastAsync("Moderation escalation",
                    $"{input.TargetType}/{input.TargetId} escalated by {moderatorId}",
                    new { input.TargetType, input.TargetId, input.ReportId }, ct);
                break;
        }

        var record = new ModerationAction
        {
            Action = input.Action,
            TargetType = input.TargetType,
            TargetId = input.TargetId,
            ReportId = input.ReportId,
            ModeratorId = moderatorId,
            Notes = input.Notes,
            RelatedSuspensionId = suspensionId,
            RelatedWarningId = warningId
        };
        _db.ModerationActions.Add(record);
        await _db.SaveChangesAsync(ct);

        // Auto-close the originating report if one was attached.
        if (input.ReportId.HasValue)
        {
            var report = await _db.ContentReports.FirstOrDefaultAsync(r => r.Id == input.ReportId, ct);
            if (report is not null && report.Status == ReportStatus.Open)
            {
                report.Status = ReportStatus.Resolved;
                report.ResolvedById = moderatorId;
                report.ResolvedAt = DateTime.UtcNow;
                report.ResolutionNotes = $"{input.Action}: {input.Notes}";
                await _db.SaveChangesAsync(ct);
            }
        }

        await _adminLog.LogAsync($"moderation.{input.Action.ToString().ToLowerInvariant()}",
            input.TargetType.ToString(), input.TargetId.ToString(), input.Notes,
            new { reportId = input.ReportId }, ct);

        return Map(record);
    }

    public async Task<IReadOnlyList<ModerationActionDto>> HistoryAsync(
        ModerationTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        EnsureCanView();
        return await _db.ModerationActions.AsNoTracking()
            .Where(a => a.TargetType == targetType && a.TargetId == targetId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ModerationActionDto(a.Id, a.Action, a.TargetType, a.TargetId,
                a.ModeratorId, a.Moderator.DisplayName, a.Notes,
                a.RelatedSuspensionId, a.RelatedWarningId, a.CreatedAt))
            .ToListAsync(ct);
    }

    // ---------------------------------------------------------------------

    private async Task SetContentVisibilityAsync(ModerationTargetType type, Guid id, bool visible, CancellationToken ct)
    {
        // Cast pattern: soft-delete + flip IsActive when present. Each target's
        // domain model differs, so we touch them per branch.
        switch (type)
        {
            case ModerationTargetType.Post:
                var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id, ct)
                            ?? throw new NotFoundException("Post", id);
                post.IsDeleted = !visible;
                post.DeletedAt = visible ? null : DateTime.UtcNow;
                break;
            case ModerationTargetType.Comment:
                var c = await _db.Comments.FirstOrDefaultAsync(x => x.Id == id, ct)
                        ?? throw new NotFoundException("Comment", id);
                c.IsDeleted = !visible;
                c.DeletedAt = visible ? null : DateTime.UtcNow;
                break;
            case ModerationTargetType.Product:
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                              ?? throw new NotFoundException("Product", id);
                product.IsActive = visible;
                product.IsDeleted = !visible;
                break;
            case ModerationTargetType.AdoptionListing:
                var listing = await _db.AdoptionListings.FirstOrDefaultAsync(l => l.Id == id, ct)
                              ?? throw new NotFoundException("AdoptionListing", id);
                listing.IsDeleted = !visible;
                listing.DeletedAt = visible ? null : DateTime.UtcNow;
                break;
            case ModerationTargetType.Review:
                var review = await _db.ProductReviews.FirstOrDefaultAsync(r => r.Id == id, ct);
                if (review is not null) { review.IsDeleted = !visible; review.DeletedAt = visible ? null : DateTime.UtcNow; }
                break;
            case ModerationTargetType.Message:
                var msg = await _db.Messages.FirstOrDefaultAsync(m => m.Id == id, ct)
                          ?? throw new NotFoundException("Message", id);
                msg.IsDeleted = !visible;
                msg.DeletedAt = visible ? null : DateTime.UtcNow;
                break;
            // Profiles (User / Doctor / Store) are hidden by setting status, not deletion.
            case ModerationTargetType.User:
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
                            ?? throw new NotFoundException("User", id);
                user.IsActive = visible;
                break;
            case ModerationTargetType.Doctor:
                var doc = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == id, ct)
                          ?? throw new NotFoundException("Doctor", id);
                doc.ApprovalStatus = visible ? ApprovalStatus.Approved : ApprovalStatus.Suspended;
                break;
            case ModerationTargetType.Store:
                var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id, ct)
                            ?? throw new NotFoundException("Store", id);
                store.ApprovalStatus = visible ? ApprovalStatus.Approved : ApprovalStatus.Suspended;
                break;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SetApprovalAsync(ModerationTargetType type, Guid id, ApprovalStatus status, CancellationToken ct)
    {
        switch (type)
        {
            case ModerationTargetType.AdoptionListing:
                var listing = await _db.AdoptionListings.FirstOrDefaultAsync(l => l.Id == id, ct)
                              ?? throw new NotFoundException("AdoptionListing", id);
                listing.Status = status == ApprovalStatus.Approved
                    ? AdoptionListingStatus.Approved : AdoptionListingStatus.Rejected;
                break;
            case ModerationTargetType.Doctor:
                var doc = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == id, ct)
                          ?? throw new NotFoundException("Doctor", id);
                doc.ApprovalStatus = status;
                break;
            case ModerationTargetType.Store:
                var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id, ct)
                            ?? throw new NotFoundException("Store", id);
                store.ApprovalStatus = status;
                break;
            case ModerationTargetType.Product:
                // Products use IsActive instead of an ApprovalStatus column.
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                              ?? throw new NotFoundException("Product", id);
                product.IsActive = status == ApprovalStatus.Approved;
                break;
        }
        await _db.SaveChangesAsync(ct);
    }

    private void EnsurePermission(ModerationActionType action)
    {
        var needed = action switch
        {
            ModerationActionType.Warn        => Permissions.Users.Suspend,
            ModerationActionType.Suspend     => Permissions.Users.Suspend,
            ModerationActionType.Ban         => Permissions.Users.Suspend,
            ModerationActionType.Approve     => Permissions.Moderation.Approve,
            ModerationActionType.Reject      => Permissions.Moderation.Reject,
            ModerationActionType.Escalate    => Permissions.Moderation.Moderate,
            ModerationActionType.MarkSuspicious => Permissions.Moderation.Moderate,
            _                                => Permissions.Moderation.Moderate
        };
        if (!_current.Permissions.Contains(needed)) throw new ForbiddenException();
    }

    private void EnsureCanView()
    {
        if (!_current.Permissions.Contains(Permissions.Moderation.View) &&
            !_current.Permissions.Contains(Permissions.Moderation.Moderate))
            throw new ForbiddenException();
    }

    private static ModerationActionDto Map(ModerationAction a) =>
        new(a.Id, a.Action, a.TargetType, a.TargetId, a.ModeratorId,
            a.Moderator?.DisplayName ?? "", a.Notes, a.RelatedSuspensionId, a.RelatedWarningId, a.CreatedAt);
}
