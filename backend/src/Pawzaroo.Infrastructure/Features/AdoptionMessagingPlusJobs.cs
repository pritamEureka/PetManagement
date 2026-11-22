using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Modules.Adoption;
using Pawzaroo.Infrastructure.Modules.Messaging;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// Worker job (register with AddHostedService in Worker/Program.cs): every 15 minutes
///   * reminds listing owners 24h before a Decision-stage SLA expires,
///   * flags overdue decisions (one notification, OverdueFlaggedAt stamped),
///   * reminds adopters when a follow-up check-in becomes due.
/// Each reminder is stamped on the row so it fires exactly once.
/// </summary>
public class AdoptionSlaJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AdoptionSlaJob> _logger;

    public AdoptionSlaJob(IServiceScopeFactory scopes, ILogger<AdoptionSlaJob> logger) { _scopes = scopes; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "AdoptionSlaJob iteration failed"); }
            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var stages = db.Set<AdoptionRequestStageEvent>();
        var open = await stages
            .Where(e => e.Stage == AdoptionApplicationStage.Decision && e.DueAt != null
                        && (e.ReminderSentAt == null || e.OverdueFlaggedAt == null)
                        && !stages.Any(n => n.RequestId == e.RequestId && n.EnteredAt > e.EnteredAt))
            .Take(500).ToListAsync(ct);

        foreach (var e in open)
        {
            var info = await db.AdoptionRequests.Where(r => r.Id == e.RequestId)
                .Select(r => new { r.AdoptionListing.OwnerId, Title = r.AdoptionListing.PetName ?? r.AdoptionListing.Title, r.AdoptionListingId })
                .SingleOrDefaultAsync(ct);
            if (info is null) continue;

            if (AdoptionSla.IsOverdue(e.DueAt, now) && e.OverdueFlaggedAt == null)
            {
                e.OverdueFlaggedAt = now;
                db.Notifications.Add(new InAppNotification
                {
                    UserId = info.OwnerId, Title = "Adoption decision overdue",
                    Body = $"An applicant for {info.Title} is waiting past the decision deadline.", Url = "/adoption/pipeline"
                });
            }
            else if (AdoptionSla.NeedsReminder(e.DueAt, now) && e.ReminderSentAt == null)
            {
                e.ReminderSentAt = now;
                db.Notifications.Add(new InAppNotification
                {
                    UserId = info.OwnerId, Title = "Adoption decision due soon",
                    Body = $"A decision for {info.Title} is due within 24 hours.", Url = "/adoption/pipeline"
                });
            }
        }

        var dueFollowUps = await db.Set<AdoptionFollowUp>()
            .Where(f => f.Status == AdoptionFollowUpStatus.Due && f.DueAt <= now && f.ReminderSentAt == null)
            .Take(500).Select(f => new { Row = f, Title = f.Listing.PetName ?? f.Listing.Title }).ToListAsync(ct);
        foreach (var f in dueFollowUps)
        {
            f.Row.ReminderSentAt = now;
            db.Notifications.Add(new InAppNotification
            {
                UserId = f.Row.AdopterId, Title = "Time for a check-in",
                Body = $"How is {f.Title} settling in? Share a quick day-{f.Row.DayOffset} update.", Url = "/adoption/follow-ups"
            });
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Worker job: every minute hard-expires (tombstones for everyone, same semantics as recall)
/// messages older than the conversation's disappearAfterSeconds and pushes a "removed" event.
/// </summary>
public class DisappearingMessageJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DisappearingMessageJob> _logger;

    public DisappearingMessageJob(IServiceScopeFactory scopes, ILogger<DisappearingMessageJob> logger) { _scopes = scopes; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "DisappearingMessageJob iteration failed"); }
            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notify = scope.ServiceProvider.GetService<INotificationService>();
        var now = DateTime.UtcNow;

        var settings = await db.Set<ConversationSettings>().AsNoTracking()
            .Where(s => s.DisappearAfterSeconds != null).Select(s => new { s.ConversationId, Ttl = s.DisappearAfterSeconds!.Value }).ToListAsync(ct);

        foreach (var s in settings)
        {
            var cutoff = now.AddSeconds(-s.Ttl);
            var expired = await db.Messages.Where(m => m.ConversationId == s.ConversationId && !m.IsDeletedForAll && m.CreatedAt <= cutoff)
                .Take(200).ToListAsync(ct);
            if (expired.Count == 0) continue;

            foreach (var m in expired) { m.IsDeletedForAll = true; m.Content = null; m.MediaUrl = null; }
            var ids = expired.Select(m => m.Id).ToList();
            var pins = await db.Set<ConversationPin>().Where(p => ids.Contains(p.MessageId)).ToListAsync(ct);
            db.Set<ConversationPin>().RemoveRange(pins);
            await db.SaveChangesAsync(ct);

            if (notify is null) continue;
            var users = await db.ConversationParticipants.Where(p => p.ConversationId == s.ConversationId && !p.HasLeft).Select(p => p.UserId).ToListAsync(ct);
            foreach (var id in ids)
                await ChatEventPublisher.PublishAsync(notify, users, "removed",
                    new { messageId = id, conversationId = s.ConversationId, reason = "expired" }, ct);
        }
    }
}
