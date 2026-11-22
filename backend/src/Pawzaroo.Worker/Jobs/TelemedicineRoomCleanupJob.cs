using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Worker.Jobs;

/// <summary>Every 5 minutes closes telemedicine rooms whose time window has expired.</summary>
public class TelemedicineRoomCleanupJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private readonly IServiceProvider _sp;
    private readonly ILogger<TelemedicineRoomCleanupJob> _logger;

    public TelemedicineRoomCleanupJob(IServiceProvider sp, ILogger<TelemedicineRoomCleanupJob> logger)
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
                var now = DateTime.UtcNow;
                var expired = await db.Set<TelemedicineRoom>()
                    .Where(r => r.ClosedAt == null && r.ExpiresAt < now)
                    .Take(500)
                    .ToListAsync(stoppingToken);
                foreach (var room in expired)
                {
                    room.ClosedAt = now;
                    db.Set<TelemedicineEvent>().Add(new TelemedicineEvent
                    { RoomId = room.Id, Kind = TelemedicineEventKind.Closed, At = now });
                }
                if (expired.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Closed {Count} expired telemedicine rooms", expired.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TelemedicineRoomCleanupJob iteration failed");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (TaskCanceledException) { break; }
        }
    }
}
