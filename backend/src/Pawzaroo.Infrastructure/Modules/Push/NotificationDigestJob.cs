using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Push;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.Push;

/// <summary>
/// Worker job (register with AddHostedService in the Worker host; NOT in the API):
/// every 5 minutes, for each (user, category) whose digest frequency is Hourly/Daily and whose last digest
/// is old enough, folds the user's unread in-app notifications of that category into ONE digest entry and
/// soft-deletes the originals. Category is inferred from payload/url (see NotificationMutePolicy) because
/// in-app rows carry no category column. A single pending notification is left as is (nothing to batch).
/// Digest rows carry Payload {"type":"digest"} and are never re-digested.
/// </summary>
public class NotificationDigestJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificationDigestJob> _logger;

    public NotificationDigestJob(IServiceScopeFactory scopes, ILogger<NotificationDigestJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(DateTime.UtcNow, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "NotificationDigestJob iteration failed"); }
            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    public static TimeSpan PeriodFor(NotificationDigestFrequency f) => f switch
    {
        NotificationDigestFrequency.Hourly => TimeSpan.FromHours(1),
        NotificationDigestFrequency.Daily => TimeSpan.FromDays(1),
        _ => TimeSpan.Zero,
    };

    public async Task<int> RunOnceAsync(DateTime now, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var prefs = await db.Set<NotificationDigestPreference>()
            .Where(p => p.Frequency != NotificationDigestFrequency.Instant)
            .OrderBy(p => p.LastDigestAt).Take(500).ToListAsync(ct);

        var counter = scope.ServiceProvider.GetService<INotificationCountCache>();
        var created = 0;
        foreach (var pref in prefs)
        {
            var period = PeriodFor(pref.Frequency);
            if (pref.LastDigestAt is { } last && now - last < period) continue;

            var unread = await db.Notifications
                .Where(n => n.UserId == pref.UserId && !n.IsRead && n.CreatedAt <= now)
                .OrderByDescending(n => n.CreatedAt).Take(500).ToListAsync(ct);

            var batch = unread
                .Where(n => !(n.Payload ?? "").Contains("\"type\":\"digest\"")
                            && NotificationMutePolicy.InferCategory(n.Payload, n.Url) == pref.Category)
                .ToList();

            pref.LastDigestAt = now;
            if (batch.Count < 2) continue;

            var titles = batch.Take(3).Select(n => n.Title).ToList();
            var counts = new Dictionary<string, int> { [pref.Category] = batch.Count };
            db.Notifications.Add(new InAppNotification
            {
                UserId = pref.UserId,
                Title = NotificationDigestFormatter.Title(counts),
                Body = NotificationDigestFormatter.Body(counts) + " — " + string.Join(" · ", titles) + (batch.Count > titles.Count ? $" and {batch.Count - titles.Count} more" : ""),
                Payload = JsonSerializer.Serialize(new { type = "digest", category = pref.Category, count = batch.Count }),
                Url = "/notifications",
            });
            foreach (var n in batch) db.Notifications.Remove(n); // soft delete via SaveChanges interceptor
            created++;
            if (counter is not null) await counter.DecrementUnreadAsync(pref.UserId, batch.Count - 1, ct);
        }

        await db.SaveChangesAsync(ct);
        return created;
    }
}
