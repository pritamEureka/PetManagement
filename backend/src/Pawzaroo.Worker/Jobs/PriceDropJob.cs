using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.ShopPlus;

namespace Pawzaroo.Worker.Jobs;

/// <summary>Notifies users when a watched product price falls.</summary>
public class PriceDropJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly IServiceProvider _sp;
    private readonly ILogger<PriceDropJob> _logger;

    public PriceDropJob(IServiceProvider sp, ILogger<PriceDropJob> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var p = scope.ServiceProvider.GetRequiredService<IDiscoveryAlertProcessor>();
                var n = await p.RunPriceDropsAsync(stoppingToken);
                if (n > 0) _logger.LogInformation("Price-drop alerts sent: {Count}", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PriceDropJob iteration failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
