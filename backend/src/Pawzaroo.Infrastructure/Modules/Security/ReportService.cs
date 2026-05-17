using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Moderation;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Security;

/// <summary>
/// Polymorphic abuse-report queue. Authentication required to file;
/// <c>moderation.view</c> required to read; <c>moderation.moderate</c> to change status.
/// </summary>
public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly INotificationService _notify;

    public ReportService(ApplicationDbContext db, ICurrentUserService current, INotificationService notify)
    {
        _db = db;
        _current = current;
        _notify = notify;
    }

    public async Task<Guid> CreateAsync(ReportContentInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();

        // De-dup: same reporter can't open multiple open reports for the same target.
        var existing = await _db.ContentReports
            .Where(r => r.ReporterId == uid && r.TargetType == input.TargetType
                        && r.TargetId == input.TargetId && r.Status == ReportStatus.Open)
            .Select(r => r.Id).FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty) return existing;

        var report = new ContentReport
        {
            ReporterId = uid,
            TargetType = input.TargetType,
            TargetId = input.TargetId,
            Reason = input.Reason,
            Details = input.Details,
            Status = ReportStatus.Open
        };
        _db.ContentReports.Add(report);
        await _db.SaveChangesAsync(ct);
        return report.Id;
    }

    public async Task<IReadOnlyList<ContentReportDto>> ListAsync(
        ReportStatus? status, ReportTargetType? targetType, int page, int pageSize, CancellationToken ct = default)
    {
        EnsureModerator();
        var q = _db.ContentReports.AsNoTracking().AsQueryable();
        if (status.HasValue)     q = q.Where(r => r.Status == status);
        if (targetType.HasValue) q = q.Where(r => r.TargetType == targetType);
        return await q.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ContentReportDto(r.Id, r.TargetType, r.TargetId,
                r.ReporterId, r.Reporter.DisplayName,
                r.Reason, r.Details, r.Status,
                r.ResolvedById, r.ResolutionNotes, r.ResolvedAt, r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ContentReportDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        EnsureModerator();
        return await _db.ContentReports.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ContentReportDto(r.Id, r.TargetType, r.TargetId,
                r.ReporterId, r.Reporter.DisplayName,
                r.Reason, r.Details, r.Status,
                r.ResolvedById, r.ResolutionNotes, r.ResolvedAt, r.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task SetStatusAsync(Guid id, ReportStatus status, string? notes, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Moderation.Moderate))
            throw new ForbiddenException();
        var uid = _current.UserId ?? throw new ForbiddenException();
        var r = await _db.ContentReports.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("ContentReport", id);

        r.Status = status;
        r.ResolvedById = uid;
        r.ResolvedAt = DateTime.UtcNow;
        r.ResolutionNotes = notes;
        await _db.SaveChangesAsync(ct);

        if (status is ReportStatus.Resolved or ReportStatus.Dismissed)
            await _notify.NotifyUserAsync(r.ReporterId, "Your report was reviewed",
                $"Status: {status}. {notes}", new { reportId = r.Id }, ct);
    }

    private void EnsureModerator()
    {
        if (!_current.Permissions.Contains(Permissions.Moderation.View) &&
            !_current.Permissions.Contains(Permissions.Moderation.Moderate))
            throw new ForbiddenException();
    }
}
