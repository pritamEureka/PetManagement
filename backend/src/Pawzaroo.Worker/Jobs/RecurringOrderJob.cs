using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.ShopPlus;

namespace Pawzaroo.Worker.Jobs;

/// <summary>Processes due ProductSubscriptions (recurring orders, retry/backoff, missed-payment handling) every minute.</summary>
public class RecurringOrderJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceProvider _sp;
    private readonly ILogger<RecurringOrderJob> _logger;

    public RecurringOrderJob(IServiceProvider sp, ILogger<RecurringOrderJob> logger)
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
                var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionRunner>();
                var r = await runner.RunDueAsync(stoppingToken);
                if (r.Processed > 0)
                    _logger.LogInformation("Subscriptions: {Processed} due, {Ok} ok, {Failed} failed, {Paused} paused", r.Processed, r.Succeeded, r.Failed, r.Paused);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RecurringOrderJob iteration failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
