using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.Communities;
using Pawzaroo.Domain.Communities;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.Communities;

/// <summary>
/// Appends to the reputation ledger and maintains the materialised totals (global row + optional community row).
/// Idempotent on (user, kind, source), capped per day, and best-effort: failures are logged, never thrown.
/// </summary>
public class ReputationService : IReputationService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReputationService> _log;

    public ReputationService(ApplicationDbContext db, ILogger<ReputationService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<bool> AwardAsync(Guid userId, Guid? communityId, ReputationKind kind, Guid sourceId, CancellationToken ct = default)
    {
        var added = new List<object>();
        try
        {
            var points = ReputationRules.PointsFor(kind);
            if (points == 0 || userId == Guid.Empty) return false;

            if (await _db.Set<ReputationEvent>().AnyAsync(e => e.UserId == userId && e.Kind == kind && e.SourceId == sourceId, ct))
                return false;

            if (points > 0)
            {
                var since = DateTime.UtcNow.AddHours(-24);
                var recent = await _db.Set<ReputationEvent>().AsNoTracking()
                    .Where(e => e.UserId == userId && e.CreatedAt >= since && e.Points > 0)
                    .Select(e => new { e.Kind, e.Points }).ToListAsync(ct);
                if (!ReputationRules.WithinCaps(kind, points, recent.Sum(r => r.Points),
                        recent.Where(r => r.Kind == kind).Sum(r => r.Points)))
                    return false;
            }

            var ev = new ReputationEvent { UserId = userId, CommunityId = communityId, Kind = kind, Points = points, SourceId = sourceId };
            _db.Set<ReputationEvent>().Add(ev);
            added.Add(ev);
            var scopes = communityId is { } c ? new Guid?[] { null, c } : new Guid?[] { null };
            foreach (var scope in scopes)
            {
                var row = await _db.Set<UserReputation>().FirstOrDefaultAsync(r => r.UserId == userId && r.CommunityId == scope, ct);
                if (row is null)
                {
                    row = new UserReputation { UserId = userId, CommunityId = scope };
                    _db.Set<UserReputation>().Add(row);
                    added.Add(row);
                }
                row.Total = Math.Max(0, row.Total + points);
                row.Level = ReputationRules.LevelFor(row.Total);
                row.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Typically a unique-index race (duplicate award). Drop our pending rows so the caller's context stays clean.
            foreach (var o in added) _db.Entry(o).State = EntityState.Detached;
            _log.LogDebug(ex, "Reputation award skipped for {User} {Kind}", userId, kind);
            return false;
        }
    }
}
