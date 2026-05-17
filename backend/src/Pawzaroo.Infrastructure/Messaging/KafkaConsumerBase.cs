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
            GroupId            = GroupId,
            AutoOffsetReset    = AutoOffsetReset.Earliest,
            EnableAutoCommit   = false,                  // we commit after success only
            EnableAutoOffsetStore = false,
            SessionTimeoutMs   = 45_000,
            ClientId           = $"{GroupId}-{Environment.MachineName}"
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

        consumer.Subscribe(Topics);
        Logger.LogInformation("[kafka] consumer {Group} subscribed to {Topics}", GroupId, string.Join(", ", Topics));

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
                        await ParkInDlqAsync(cr, "malformed-envelope", stoppingToken);
                        consumer.StoreOffset(cr);
                        consumer.Commit(cr);
                        continue;
                    }

                    await using var scope = ScopeFactory.CreateAsyncScope();
                    var idempotency = scope.ServiceProvider.GetService<IConsumerIdempotency>();
                    if (idempotency is not null && !await idempotency.TryClaimAsync(GroupId, header.EventId, stoppingToken))
                    {
                        Logger.LogDebug("[kafka {Group}] duplicate event {EventId}, skipping", GroupId, header.EventId);
                    }
                    else
                    {
                        try
                        {
                            await WithRetryAsync(ct =>
                                HandleAsync(cr.Topic, data, header, scope.ServiceProvider, ct), stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "[kafka {Group}] handler exhausted retries on {Topic} key={Key}; parking", GroupId, cr.Topic, cr.Message.Key);
                            await ParkInDlqAsync(cr, ex.GetType().Name + ": " + ex.Message, stoppingToken);
                            // Release idempotency claim so a replay from DLQ can re-run.
                            if (idempotency is not null) await idempotency.ReleaseAsync(GroupId, header.EventId, stoppingToken);
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

    /// <summary>Exponential backoff: base * 2^attempt with jitter.</summary>
    private async Task WithRetryAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        var attempts = Options.RetryAttempts;
        var baseDelay = Options.RetryBaseDelayMs;

        for (var i = 0; ; i++)
        {
            try { await work(ct); return; }
            catch when (i >= attempts) { throw; }
            catch (Exception ex)
            {
                var delay = TimeSpan.FromMilliseconds(baseDelay * Math.Pow(2, i) + Random.Shared.Next(50, 250));
                Logger.LogWarning(ex, "[kafka {Group}] handler attempt {Attempt} failed, retrying in {Delay}", GroupId, i + 1, delay);
                await Task.Delay(delay, ct);
            }
        }
    }

    /// <summary>
    /// Routes the offending message to <c>{topic}.dlq</c> with two extra headers
    /// (<c>dlq-reason</c>, <c>dlq-source-topic</c>) so operators can replay.
    /// </summary>
    private async Task ParkInDlqAsync(ConsumeResult<string, string> source, string reason, CancellationToken ct)
    {
        try
        {
            var dlqTopic = source.Topic + Pawzaroo.Application.Common.Messaging.KafkaTopics.DeadLetterSuffix;
            using var dlqProducer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = Options.BootstrapServers,
                EnableIdempotence = true
            }).Build();

            var headers = source.Message.Headers ?? new Headers();
            headers.Add(new Header("dlq-reason", Encoding.UTF8.GetBytes(reason)));
            headers.Add(new Header("dlq-source-topic", Encoding.UTF8.GetBytes(source.Topic)));

            await dlqProducer.ProduceAsync(dlqTopic, new Message<string, string>
            {
                Key = source.Message.Key,
                Value = source.Message.Value,
                Headers = headers
            }, ct);
            dlqProducer.Flush(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[kafka {Group}] failed to write to DLQ — message dropped", GroupId);
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
