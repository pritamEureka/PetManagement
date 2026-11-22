using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.Reliability;

namespace Pawzaroo.Worker.Jobs;

/// <summary>Purges expired (24h TTL) idempotency-key rows. Requires AddReliabilityFeatures() in the worker host.</summary>
public class IdempotencyCleanupJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private readonly IServiceProvider _sp;
    private readonly ILogger<IdempotencyCleanupJob> _logger;

    public IdempotencyCleanupJob(IServiceProvider sp, ILogger<IdempotencyCleanupJob> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var svc = _sp.GetRequiredService<IIdempotencyCleanupService>();
                var n = await svc.CleanupExpiredAsync(stoppingToken);
                if (n > 0) _logger.LogInformation("Removed {Count} expired idempotency records", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IdempotencyCleanupJob iteration failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
