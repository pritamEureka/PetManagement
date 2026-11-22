using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.ShopPlus;

namespace Pawzaroo.Worker.Jobs;

/// <summary>Releases checkout stock reservations whose hold window has elapsed.</summary>
public class ReservationExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceProvider _sp;
    private readonly ILogger<ReservationExpiryJob> _logger;

    public ReservationExpiryJob(IServiceProvider sp, ILogger<ReservationExpiryJob> logger)
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
                var svc = scope.ServiceProvider.GetRequiredService<IStockReservationService>();
                var n = await svc.ExpireDueAsync(stoppingToken);
                if (n > 0) _logger.LogInformation("Expired {Count} stock reservations", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReservationExpiryJob iteration failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
