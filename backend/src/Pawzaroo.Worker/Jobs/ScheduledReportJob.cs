using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.Intelligence;

namespace Pawzaroo.Worker.Jobs;

/// <summary>Runs due scheduled intelligence reports (roadmap 8.8) and emails the CSV to recipients.</summary>
public class ScheduledReportJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private readonly IServiceProvider _sp;
    private readonly ILogger<ScheduledReportJob> _logger;

    public ScheduledReportJob(IServiceProvider sp, ILogger<ScheduledReportJob> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IScheduledReportService>();
                var n = await svc.RunDueAsync(stoppingToken);
                if (n > 0) _logger.LogInformation("Scheduled reports run: {Count}", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduledReportJob iteration failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
