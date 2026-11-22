using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Messaging;
using Pawzaroo.Application.Modules.Push;
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

        var category = MapCategory(ev.Title, ev.Payload);

        // Honor the per-category in-app toggle (missing preference row = enabled).
        var pref = await db.Set<NotificationPreference>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == ev.UserId && p.Category == category, ct);
        var inAppAllowed = NotificationPolicy.ShouldDeliver(NotificationChannel.InApp,
            pref is null ? null : new ChannelPrefs(pref.InAppEnabled, pref.PushEnabled, pref.EmailEnabled), null, DateTime.UtcNow);

        if (inAppAllowed)
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

        // Web Push fan-out (preferences + quiet hours are enforced inside PushDispatcher). Never fail the consumer.
        try
        {
            var push = scope.GetService<IPushDispatcher>();
            if (push is not null)
                await push.DispatchAsync(ev.UserId, new PushMessage(ev.Title, ev.Body, PayloadUrl(ev.Payload), category), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "[notify] push dispatch failed for {NotificationId}", ev.NotificationId);
        }

        Logger.LogInformation("[notify] persisted {NotificationId} for {UserId} ({Title})",
            ev.NotificationId, ev.UserId, ev.Title);
    }

    private static string? PayloadString(object? payload, params string[] names)
    {
        if (payload is null) return null;
        try
        {
            var el = payload is JsonElement je ? je : JsonSerializer.SerializeToElement(payload);
            if (el.ValueKind != JsonValueKind.Object) return null;
            foreach (var prop in el.EnumerateObject())
                foreach (var n in names)
                    if (string.Equals(prop.Name, n, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                        return prop.Value.GetString();
        }
        catch (JsonException) { }
        return null;
    }

    private static string? PayloadUrl(object? payload)
    {
        var url = PayloadString(payload, "url", "link", "href", "path");
        return url is not null && url.StartsWith('/') && !url.StartsWith("//") ? url : null;
    }

    /// <summary>Best-effort mapping of an event (payload type/kind or title keywords) to a NotificationCategories key.</summary>
    internal static string MapCategory(string title, object? payload)
    {
        var hint = ((PayloadString(payload, "category", "type", "kind", "event") ?? "") + " " + (title ?? "")).ToLowerInvariant();
        foreach (var c in NotificationCategories.All)
            if (hint.Trim() == c) return c;
        bool Has(params string[] k) => k.Any(hint.Contains);
        if (Has("message", "chat")) return NotificationCategories.Messages;
        if (Has("appointment", "booking", "telemedicine", "vet visit")) return NotificationCategories.Appointments;
        if (Has("reminder", "vaccin", "medication")) return NotificationCategories.Reminders;
        if (Has("order", "delivery", "payout", "payment", "refund", "return", "price", "stock")) return NotificationCategories.Orders;
        if (Has("adopt")) return NotificationCategories.Adoption;
        if (Has("lost", "sighting")) return NotificationCategories.LostFound;
        if (Has("communit", "event")) return NotificationCategories.Community;
        if (Has("like", "comment", "react", "follow", "feed", "post")) return NotificationCategories.Social;
        return NotificationCategories.System;
    }
}
