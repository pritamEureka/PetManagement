using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Infrastructure.Modules.Pets;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Worker.Jobs;

/// <summary>
/// Hourly privacy sweep: nulls the exact coordinates of lost/found reports older than
/// LostFound:ExactLocationDays (default 14). City/area are kept. Idempotent.
/// Registration (Worker/Program.cs): builder.Services.AddHostedService&lt;LostFoundLocationExpiryJob&gt;();
/// </summary>
public class LostFoundLocationExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly IServiceProvider _sp;
    private readonly ILogger<LostFoundLocationExpiryJob> _logger;

    public LostFoundLocationExpiryJob(IServiceProvider sp, ILogger<LostFoundLocationExpiryJob> logger)
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
                var days = LostFoundLocationExpiry.ConfiguredDays(scope.ServiceProvider.GetRequiredService<IConfiguration>());
                var n = await LostFoundLocationExpiry.RunAsync(db, days, DateTime.UtcNow, stoppingToken);
                if (n > 0) _logger.LogInformation("Cleared exact location on {Count} lost/found reports", n);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LostFoundLocationExpiryJob failed");
            }
            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
