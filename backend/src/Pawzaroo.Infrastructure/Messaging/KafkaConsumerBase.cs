using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Messaging;

namespace Pawzaroo.Infrastructure.Messaging;

/// <summary>
/// Base class for every domain-specific Kafka consumer.
///
/// Responsibilities:
///   - subscribe to topic(s) under a named consumer group
///   - decode <see cref="EventEnvelope{T}"/> generically
///   - dedupe via <see cref="IConsumerIdempotency"/>
///   - retry with exponential backoff up to <c>RetryAttempts</c>
///   - park poisoned messages on <c>{topic}.dlq</c> with the failure reason
///   - manual offset commits — we only commit after a successful HandleAsync
///
/// Subclasses implement <see cref="HandleAsync"/> and override <see cref="Topics"/>.
/// </summary>
public abstract class KafkaConsumerBase : BackgroundService
{
    protected readonly KafkaOptions Options;
    protected readonly IServiceScopeFactory ScopeFactory;
    protected readonly ILogger Logger;

    /// <summary>Unique consumer group id (one per concern).</summary>
    protected abstract string GroupId { get; }

    /// <summary>Group id as seen by the broker (<c>Kafka:TopicPrefix</c> applied).</summary>
    protected string PhysicalGroupId => KafkaTopicNaming.ApplyGroup(Options.TopicPrefix, GroupId);

    /// <summary>Topics to subscribe to.</summary>
    protected abstract IReadOnlyList<string> Topics { get; }

    /// <summary>Handle a deserialized envelope. Throw to trigger retry/DLQ.</summary>
    protected abstract Task HandleAsync(string topic, JsonElement data, EventEnvelopeHeader header, IServiceProvider scope, CancellationToken ct);

    protected KafkaConsumerBase(IOptions<KafkaOptions> options, IServiceScopeFactory scopeFactory, ILogger logger)
    {
        Options = options.Value;
        ScopeFactory = scopeFactory;
        Logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => RunLoop(stoppingToken), stoppingToken);

    private async Task RunLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers   = Options.BootstrapServers,
            GroupId            = PhysicalGroupId,
            AutoOffsetReset    = AutoOffsetReset.Earliest,
            EnableAutoCommit   = false,                  // we commit after success only
            EnableAutoOffsetStore = false,
            SessionTimeoutMs   = 45_000,
            ClientId           = $"{PhysicalGroupId}-{Environment.MachineName}"
        };

        if (!string.IsNullOrWhiteSpace(Options.SecurityProtocol)
            && Enum.TryParse<SecurityProtocol>(Options.SecurityProtocol, true, out var sp))
        {
            config.SecurityProtocol = sp;
            if (!string.IsNullOrWhiteSpace(Options.SaslMechanism)
                && Enum.TryParse<SaslMechanism>(Options.SaslMechanism, true, out var sasl))
            {
                config.SaslMechanism = sasl;
                config.SaslUsername  = Options.SaslUsername;
                config.SaslPassword  = Options.SaslPassword;
            }
        }

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => Logger.LogWarning("[kafka consumer {Group}] {Reason}", GroupId, e.Reason))
            .Build();

        var wireTopics = Topics.Select(t => KafkaTopicNaming.Apply(Options.TopicPrefix, t)).ToList();
        consumer.Subscribe(wireTopics);
        Logger.LogInformation("[kafka] consumer {Group} subscribed to {Topics}", PhysicalGroupId, string.Join(", ", wireTopics));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? cr = null;
                try
                {
                    cr = consumer.Consume(stoppingToken);
                    if (cr?.Message is null) continue;

                    if (!TryParseEnvelope(cr.Message, out var header, out var data))
                    {
                        Logger.LogWarning("[kafka {Group}] malformed envelope on {Topic}; routing to DLQ", GroupId, cr.Topic);
                        await ParkInDlqAsync(cr, "malformed-envelope", FailureKind.Permanent, 1, DateTime.UtcNow, stoppingToken);
                        consumer.StoreOffset(cr);
                        consumer.Commit(cr);
                        continue;
                    }

                    await using var scope = ScopeFactory.CreateAsyncScope();
                    var idempotency = scope.ServiceProvider.GetService<IConsumerIdempotency>();
                    if (idempotency is not null && !await idempotency.TryClaimAsync(PhysicalGroupId, header.EventId, stoppingToken))
                    {
                        Logger.LogDebug("[kafka {Group}] duplicate event {EventId}, skipping", GroupId, header.EventId);
                    }
                    else
                    {
                        var firstFailedAt = DateTime.UtcNow;
                        var outcome = await RunHandlerWithRetryAsync(ct =>
                            HandleAsync(LogicalTopic(cr.Topic), data, header, scope.ServiceProvider, ct), stoppingToken);
                        if (outcome.Error is not null)
                        {
                            var ex = outcome.Error;
                            Logger.LogError(ex, "[kafka {Group}] {Kind} failure after {Attempts} attempt(s) on {Topic} key={Key}; parking",
                                GroupId, outcome.Kind, outcome.Attempts, cr.Topic, cr.Message.Key);
                            await ParkInDlqAsync(cr, ex.GetType().Name + ": " + ex.Message, outcome.Kind, outcome.Attempts, firstFailedAt, stoppingToken);
                            // Release idempotency claim so a replay from DLQ can re-run.
                            if (idempotency is not null) await idempotency.ReleaseAsync(PhysicalGroupId, header.EventId, stoppingToken);
                        }
                    }

                    consumer.StoreOffset(cr);
                    consumer.Commit(cr);
                }
                catch (ConsumeException ex) { Logger.LogError(ex, "[kafka {Group}] consume error", GroupId); }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        finally { consumer.Close(); }
    }

    private string LogicalTopic(string wireTopic) => KafkaTopicNaming.Strip(Options.TopicPrefix, wireTopic);

    private readonly record struct HandlerOutcome(Exception? Error, FailureKind Kind, int Attempts);

    /// <summary>
    /// Runs the handler. Transient failures are retried with exponential backoff up to
    /// <c>RetryAttempts</c> extra attempts; permanent failures (see <see cref="FailureClassifier"/>) stop at once.
    /// Returns the final error (null on success) instead of throwing so the caller can route to the DLQ.
    /// </summary>
    private async Task<HandlerOutcome> RunHandlerWithRetryAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        var attempts = Options.RetryAttempts;
        var baseDelay = Options.RetryBaseDelayMs;

        for (var i = 0; ; i++)
        {
            try { await work(ct); return new HandlerOutcome(null, FailureKind.Transient, i + 1); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                var kind = FailureClassifier.ClassifyConsumerFailure(ex);
                if (kind == FailureKind.Permanent || i >= attempts)
                    return new HandlerOutcome(ex, kind, i + 1);

                var delay = ConsumerBackoff.Delay(baseDelay, i, Random.Shared.Next(50, 250));
                Logger.LogWarning(ex, "[kafka {Group}] transient handler failure (attempt {Attempt}), retrying in {Delay}", GroupId, i + 1, delay);
                await Task.Delay(delay, ct);
            }
        }
    }

    /// <summary>
    /// Routes the offending message to <c>{topic}.dlq</c> (headers <c>dlq-reason</c>, <c>dlq-source-topic</c>)
    /// using the shared producer, and persists a <c>DeadLetterRecord</c> for the admin re-drive UI.
    /// Both writes are independent and best-effort so one failing never blocks the other.
    /// </summary>
    private async Task ParkInDlqAsync(ConsumeResult<string, string> source, string reason, FailureKind kind,
        int attempts, DateTime firstFailedAt, CancellationToken ct)
    {
        var logicalTopic = LogicalTopic(source.Topic);
        Pawzaroo.Infrastructure.Observability.PawzarooTelemetry.DlqMessages.Add(1,
            new KeyValuePair<string, object?>("topic", logicalTopic),
            new KeyValuePair<string, object?>("kind", kind.ToString()));

        var headerMap = new Dictionary<string, string>();
        foreach (var h in source.Message.Headers ?? new Headers())
            headerMap[h.Key] = Encoding.UTF8.GetString(h.GetValueBytes());

        // 1) durable record (fresh scope: the handler scope's DbContext may hold failed changes)
        try
        {
            await using var scope = ScopeFactory.CreateAsyncScope();
            var recorder = scope.ServiceProvider.GetService<IDeadLetterRecorder>();
            if (recorder is not null)
                await recorder.RecordAsync(new DeadLetterEntry(logicalTopic, source.Partition.Value, source.Offset.Value,
                    source.Message.Key, source.Message.Value ?? "", headerMap, GroupId, reason, kind, attempts, firstFailedAt), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError(ex, "[kafka {Group}] failed to persist DeadLetterRecord for {Topic}[{Partition}]@{Offset}",
                GroupId, source.Topic, source.Partition.Value, source.Offset.Value);
        }

        // 2) DLQ topic via the single shared producer
        try
        {
            var dlqTopic = logicalTopic + Pawzaroo.Application.Common.Messaging.KafkaTopics.DeadLetterSuffix;
            await using var scope = ScopeFactory.CreateAsyncScope();
            var producer = scope.ServiceProvider.GetService<IRawKafkaProducer>() ?? SharedKafkaProducer.GetShared(Options);

            var headers = headerMap
                .Where(kv => kv.Key is not ("dlq-reason" or "dlq-source-topic"))
                .Select(kv => new KeyValuePair<string, byte[]>(kv.Key, Encoding.UTF8.GetBytes(kv.Value)))
                .ToList();
            headers.Add(new("dlq-reason", Encoding.UTF8.GetBytes(reason)));
            headers.Add(new("dlq-source-topic", Encoding.UTF8.GetBytes(logicalTopic)));
            headers.Add(new("dlq-failure-kind", Encoding.UTF8.GetBytes(kind.ToString())));

            await producer.ProduceAsync(dlqTopic, source.Message.Key, source.Message.Value ?? "", headers, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError(ex, "[kafka {Group}] failed to write to DLQ topic (record persisted if DB was reachable)", GroupId);
        }
    }

    private static bool TryParseEnvelope(Message<string, string> msg, out EventEnvelopeHeader header, out JsonElement data)
    {
        header = default!;
        data = default;
        if (string.IsNullOrEmpty(msg.Value)) return false;

        try
        {
            using var doc = JsonDocument.Parse(msg.Value);
            var root = doc.RootElement;

            // The envelope was produced by KafkaProducer — required fields are present.
            var eventId    = Guid.Parse(root.GetProperty("eventId").GetString()!);
            var eventType  = root.GetProperty("eventType").GetString() ?? "";
            var version    = root.TryGetProperty("version", out var v) ? v.GetString() ?? "1" : "1";
            var occurredAt = root.GetProperty("occurredAt").GetDateTime();
            var corr       = root.TryGetProperty("correlationId", out var c) ? c.GetString() : null;
            var userId     = root.TryGetProperty("userId", out var u) ? u.GetString() : null;
            var dataEl     = root.GetProperty("data");

            header = new EventEnvelopeHeader(eventId, eventType, version, occurredAt, corr, userId);
            // Clone — the JsonDocument is disposed on return.
            data = dataEl.Clone();
            return true;
        }
        catch { return false; }
    }
}

public record EventEnvelopeHeader(
    Guid EventId, string EventType, string Version, DateTime OccurredAt,
    string? CorrelationId, string? UserId);
