using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Messaging;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Caching;
using Pawzaroo.Infrastructure.Messaging;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Worker.Jobs;

/// <summary>
/// Consumes pawzaroo.notifications and writes the durable
/// <see cref="InAppNotification"/> row for the recipient.
///
/// Idempotency: two-stage. A Redis SET NX claim on the NotificationId stops
/// re-deliveries cheaply within the dedup window; the DB AnyAsync guard catches
/// the cold-start case after Redis TTL. The unread counter is NOT touched here
/// — the producer already bumped it on the hot path so the bell icon updates
/// without waiting on this consumer (see SignalRNotificationService).
///
/// Future hook: email / push fan-out lives here. Add a per-channel preference
/// check (NotificationPreference entity) before invoking those adapters.
/// </summary>
public class NotificationDispatcherJob : KafkaConsumerBase
{
    public NotificationDispatcherJob(IOptions<KafkaOptions> options, IServiceScopeFactory scopeFactory,
        ILogger<NotificationDispatcherJob> logger)
        : base(options, scopeFactory, logger) { }

    protected override string GroupId => KafkaConsumerGroups.NotificationDispatch;
    protected override IReadOnlyList<string> Topics => new[] { Options.Topics.Notifications };

    protected override async Task HandleAsync(string topic, JsonElement data, EventEnvelopeHeader header,
        IServiceProvider scope, CancellationToken ct)
    {
        if (header.EventType != nameof(NotificationCreated)) return;

        var ev = data.Deserialize<NotificationCreated>(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        if (ev is null) return;

        var dedup = scope.GetRequiredService<IConsumerDeduplicator>();
        var claimed = await dedup.TryClaimAsync(GroupId, ev.NotificationId.ToString(), RedisTtls.NotifyConsumerSeen, ct);
        if (!claimed)
        {
            Logger.LogDebug("[notify] dedup skipped {NotificationId}", ev.NotificationId);
            return;
        }

        var db = scope.GetRequiredService<ApplicationDbContext>();

        // DB-level idempotency fallback in case the dedup TTL already expired
        // on a very late re-delivery.
        var exists = await db.Notifications.AnyAsync(n => n.Id == ev.NotificationId, ct);
        if (exists)
        {
            Logger.LogDebug("[notify] already persisted {NotificationId}", ev.NotificationId);
            return;
        }

        db.Notifications.Add(new InAppNotification
        {
            Id = ev.NotificationId,
            UserId = ev.UserId,
            Title = ev.Title,
            Body = ev.Body,
            Payload = ev.Payload is null ? null : JsonSerializer.Serialize(ev.Payload),
            IsRead = false,
            CreatedAt = ev.OccurredAt
        });
        await db.SaveChangesAsync(ct);

        Logger.LogInformation("[notify] persisted {NotificationId} for {UserId} ({Title})",
            ev.NotificationId, ev.UserId, ev.Title);
    }
}
