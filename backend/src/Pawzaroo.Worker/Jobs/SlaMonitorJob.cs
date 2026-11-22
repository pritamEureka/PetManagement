using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.LogisticsPlus;

namespace Pawzaroo.Worker.Jobs;

/// <summary>Flags late shipments/deliveries (SlaBreach rows, idempotent) and notifies store owners and admins.</summary>
public class SlaMonitorJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private readonly IServiceProvider _sp;
    private readonly ILogger<SlaMonitorJob> _logger;

    public SlaMonitorJob(IServiceProvider sp, ILogger<SlaMonitorJob> logger) { _sp = sp; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<ISlaMonitor>().RunAsync(stoppingToken);
                if (n > 0) _logger.LogInformation("Recorded {Count} new SLA breaches", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "SlaMonitorJob iteration failed"); }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
