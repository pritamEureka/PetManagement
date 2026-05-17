using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Messaging;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Messaging;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Worker.Jobs;

/// <summary>
/// Consumes pawzaroo.notifications and:
///   1) persists an <see cref="InAppNotification"/> for the recipient,
///   2) bumps the per-user unread counter in Redis (fast bell-icon read),
///   3) (TODO) hands off to an email/push provider for out-of-band channels.
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

        var db = scope.GetRequiredService<ApplicationDbContext>();
        var counter = scope.GetRequiredService<INotificationCountCache>();

        // Idempotency: if a row with this NotificationId already exists, skip.
        var exists = await db.Notifications.AnyAsync(n => n.Id == ev.NotificationId, ct);
        if (!exists)
        {
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
        }

        await counter.BumpUnreadAsync(ev.UserId, 1, ct);
        Logger.LogInformation("[notify] persisted + bumped counter for {UserId} ({Title})", ev.UserId, ev.Title);
    }
}
