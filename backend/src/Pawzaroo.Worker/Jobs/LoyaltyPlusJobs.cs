using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.Marketplace;

namespace Pawzaroo.Worker.Jobs;

/// <summary>Shared loop: run an action in a fresh scope every interval, never crash the host.</summary>
public abstract class ScopedIntervalJob : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _startDelay;

    protected ScopedIntervalJob(IServiceProvider sp, ILogger logger, TimeSpan interval, TimeSpan startDelay)
    { _sp = sp; _logger = logger; _interval = interval; _startDelay = startDelay; }

    protected abstract Task<int> RunOnceAsync(IServiceProvider scoped, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(_startDelay, stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var n = await RunOnceAsync(scope.ServiceProvider, stoppingToken);
                if (n > 0) _logger.LogInformation("{Job} processed {Count}", GetType().Name, n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "{Job} iteration failed", GetType().Name); }
            try { await Task.Delay(_interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>Notifies store owners when product/variant stock falls to the threshold (de-duplicated).</summary>
public class LowStockAlertJob : ScopedIntervalJob
{
    public LowStockAlertJob(IServiceProvider sp, ILogger<LowStockAlertJob> l) : base(sp, l, TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(40)) { }
    protected override Task<int> RunOnceAsync(IServiceProvider s, CancellationToken ct) =>
        s.GetRequiredService<IStockAlertService>().RunLowStockAlertsAsync(ct);
}

/// <summary>Computes run-out estimates from delivered orders and sends one reminder per cycle.</summary>
public class ReplenishmentReminderJob : ScopedIntervalJob
{
    public ReplenishmentReminderJob(IServiceProvider sp, ILogger<ReplenishmentReminderJob> l) : base(sp, l, TimeSpan.FromHours(6), TimeSpan.FromSeconds(60)) { }
    protected override Task<int> RunOnceAsync(IServiceProvider s, CancellationToken ct) =>
        s.GetRequiredService<IReplenishmentService>().RunAsync(ct);
}

/// <summary>Expires lapsed loyalty points and back-fills earn rows for delivered orders that missed the hook.</summary>
public class LoyaltyPointsExpiryJob : ScopedIntervalJob
{
    public LoyaltyPointsExpiryJob(IServiceProvider sp, ILogger<LoyaltyPointsExpiryJob> l) : base(sp, l, TimeSpan.FromHours(1), TimeSpan.FromSeconds(80)) { }
    protected override Task<int> RunOnceAsync(IServiceProvider s, CancellationToken ct) =>
        s.GetRequiredService<ILoyaltyService>().ExpirePointsAsync(ct);
}
