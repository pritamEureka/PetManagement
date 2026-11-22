using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Pawzaroo.Infrastructure.Observability;

/// <summary>
/// Single home for the application ActivitySource and Meter (both named "Pawzaroo").
/// Instruments are static so any layer can record without DI. Names are snake_case; an OTLP
/// collector exposes counters with a "_total" suffix and ms histograms as "*_milliseconds".
/// </summary>
public static class PawzarooTelemetry
{
    public const string Name = "Pawzaroo";

    public static readonly ActivitySource Source = new(Name, BuildInfo.Version);
    public static readonly Meter Meter = new(Name, BuildInfo.Version);

    public static readonly Counter<long> BookingCreated =
        Meter.CreateCounter<long>("booking_created", "{booking}", "Appointments/bookings created");
    public static readonly Counter<long> CheckoutCompleted =
        Meter.CreateCounter<long>("checkout_completed", "{checkout}", "Checkouts completed (tag: outcome)");
    public static readonly Histogram<double> CheckoutDurationMs =
        Meter.CreateHistogram<double>("checkout_duration_ms", "ms", "Checkout handler duration");
    public static readonly Counter<long> NotificationDispatched =
        Meter.CreateCounter<long>("notification_dispatched", "{notification}", "Notifications persisted by the dispatcher");
    public static readonly Histogram<double> NotificationLatencyMs =
        Meter.CreateHistogram<double>("notification_latency_ms", "ms", "Event occurred to notification persisted");
    public static readonly Histogram<double> OutboxDispatchLatencyMs =
        Meter.CreateHistogram<double>("outbox_dispatch_latency_ms", "ms", "Outbox row created to published to Kafka");
    public static readonly Counter<long> OutboxDispatched =
        Meter.CreateCounter<long>("outbox_dispatched", "{message}", "Outbox rows processed (tag: result)");
    public static readonly Counter<long> WebhookReceived =
        Meter.CreateCounter<long>("webhook_received", "{request}", "Inbound webhooks received");
    public static readonly Counter<long> WebhookRejected =
        Meter.CreateCounter<long>("webhook_rejected", "{request}", "Inbound webhooks rejected (tag: reason)");
    public static readonly Counter<long> DlqMessages =
        Meter.CreateCounter<long>("dlq_messages", "{message}", "Messages parked on a dead-letter topic (tags: topic, kind)");

    // Observable gauges read these snapshots; the outbox dispatcher refreshes them each loop.
    private static long _outboxPending;
    private static double _outboxOldestPendingAgeSeconds;

    public static void SetOutboxSnapshot(long pending, double oldestPendingAgeSeconds)
    {
        Interlocked.Exchange(ref _outboxPending, pending);
        Volatile.Write(ref _outboxOldestPendingAgeSeconds, oldestPendingAgeSeconds);
    }

    public static readonly ObservableGauge<long> OutboxPending =
        Meter.CreateObservableGauge("outbox_pending", () => Interlocked.Read(ref _outboxPending),
            "{message}", "Outbox rows awaiting dispatch");
    public static readonly ObservableGauge<double> OutboxOldestPendingAge =
        Meter.CreateObservableGauge("outbox_oldest_pending_age_seconds", () => Volatile.Read(ref _outboxOldestPendingAgeSeconds),
            "s", "Age of the oldest pending outbox row");

    /// <summary>Starts an activity tagged with build info (null when nobody is listening).</summary>
    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        var a = Source.StartActivity(name, kind);
        a?.SetTag("build.version", BuildInfo.Version);
        a?.SetTag("build.git_sha", BuildInfo.GitSha);
        return a;
    }
}
