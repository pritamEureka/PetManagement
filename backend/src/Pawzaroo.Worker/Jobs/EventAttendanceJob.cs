using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.Communities;
using Pawzaroo.Domain.Communities;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Worker.Jobs;

/// <summary>
/// Every 30 minutes: for events that ended within the last 3 days (and were not cancelled), awards
/// <see cref="ReputationKind.EventAttended"/> to every Going RSVP. Idempotent: the ledger is unique on
/// (UserId, Kind, SourceId=eventId), so re-runs and overlapping windows never double-award.
/// Registration (Worker/Program.cs): builder.Services.AddHostedService&lt;EventAttendanceJob&gt;();
/// Requires IReputationService to be registered (SocialPlusRegistration does this).
/// </summary>
public class EventAttendanceJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Lookback = TimeSpan.FromDays(3);

    private readonly IServiceProvider _sp;
    private readonly ILogger<EventAttendanceJob> _logger;

    public EventAttendanceJob(IServiceProvider sp, ILogger<EventAttendanceJob> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var rep = scope.ServiceProvider.GetRequiredService<IReputationService>();
                await RunOnceAsync(db, rep, DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventAttendanceJob iteration failed");
            }
            try { await Task.Delay(Interval, stoppingToken); } catch (TaskCanceledException) { break; }
        }
    }

    internal static async Task<int> RunOnceAsync(ApplicationDbContext db, IReputationService rep, DateTime now, CancellationToken ct)
    {
        var since = now - Lookback;
        var events = await db.Set<CommunityEvent>().AsNoTracking()
            .Where(e => !e.IsCancelled && e.EndsAt <= now && e.EndsAt > since)
            .Select(e => new { e.Id, e.CommunityId }).Take(500).ToListAsync(ct);

        var awarded = 0;
        foreach (var ev in events)
        {
            var going = await db.Set<EventRsvp>().AsNoTracking()
                .Where(r => r.EventId == ev.Id && r.Status == RsvpStatus.Going)
                .Select(r => r.UserId).ToListAsync(ct);
            foreach (var uid in going)
            {
                if (await rep.AwardAsync(uid, ev.CommunityId, ReputationKind.EventAttended, ev.Id, ct)) awarded++;
            }
        }
        return awarded;
    }
}
