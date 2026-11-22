using System.Text;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Messaging;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Infrastructure.Caching;
using Pawzaroo.Infrastructure.Observability;
using Pawzaroo.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Messaging;

/// <summary>
/// Polls the outbox table and ships pending rows to Kafka.
///
/// Concurrency control: when multiple worker replicas run, only the one
/// holding the Redis leader lock dispatches. The lock is short-TTL so a dead
/// leader is replaced within ~15s. We do NOT use Postgres advisory locks
/// here because the dispatcher runs in the worker, not the API.
///
/// Backoff: failed sends bump <c>Attempts</c> and push <c>NextAttemptAt</c>
/// forward by an exponential window; after <see cref="MaxAttempts"/> the row
/// is moved to <see cref="OutboxStatus.Failed"/> for ops review.
/// </summary>
public class OutboxDispatcherJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly KafkaOptions _kafka;
    private readonly ILogger<OutboxDispatcherJob> _logger;

    private const int BatchSize = 100;
    private const int MaxAttempts = 10;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    public OutboxDispatcherJob(IServiceScopeFactory scopeFactory, IConnectionMultiplexer redis,
        IOptions<KafkaOptions> kafka, ILogger<OutboxDispatcherJob> logger)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _kafka = kafka.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var leaderId = Guid.NewGuid().ToString("N");
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
            CompressionType = CompressionType.Lz4,
            ClientId = $"outbox-{Environment.MachineName}"
        }).Build();

        _logger.LogInformation("[outbox] dispatcher started, leaderId={Leader}", leaderId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await TryBecomeLeaderAsync(leaderId))
                {
                    await Task.Delay(PollInterval, stoppingToken);
                    continue;
                }

                await DispatchBatchAsync(producer, stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[outbox] dispatcher loop error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    /// <summary>
    /// SETNX-with-TTL leader election. The leader periodically re-acquires
    /// (every iteration) so a crashed leader's lease expires within ~15s.
    /// </summary>
    private async Task<bool> TryBecomeLeaderAsync(string leaderId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = RedisKeys.OutboxLeader();
            // Try to take the lease, or extend it if we already hold it.
            if (await db.StringSetAsync(key, leaderId, RedisTtls.OutboxLeader, When.NotExists)) return true;
            // Extend if we still own the lock.
            var current = (string?)await db.StringGetAsync(key);
            if (current == leaderId)
            {
                await db.KeyExpireAsync(key, RedisTtls.OutboxLeader);
                return true;
            }
            return false;
        }
        catch (RedisException)
        {
            // Redis down — fail open so the worker keeps dispatching even with
            // multiple replicas (idempotent producers keep things consistent).
            return true;
        }
    }

    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);
    private readonly string _instanceId = $"{Truncate(Environment.MachineName, 24)}-{Guid.NewGuid():N}";

    /// <summary>
    /// Atomically claims a batch (SELECT ... FOR UPDATE SKIP LOCKED inside a short transaction, stamping
    /// ClaimedBy/ClaimedUntil as a lease), then publishes outside the transaction. If this replica dies mid-batch the
    /// lease expires and another replica re-claims the rows (at-least-once; consumers dedupe by event id).
    /// </summary>
    private async Task<List<OutboxMessage>> ClaimBatchAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var leaseUntil = now + Lease;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var batch = await db.OutboxMessages
            .FromSqlInterpolated($@"
                SELECT * FROM outbox_messages
                WHERE status = 0
                  AND (next_attempt_at IS NULL OR next_attempt_at <= {now})
                  AND (claimed_until IS NULL OR claimed_until < {now})
                ORDER BY occurred_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED")
            .ToListAsync(ct);

        foreach (var m in batch)
        {
            m.ClaimedBy = _instanceId;
            m.ClaimedUntil = leaseUntil;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return batch;
    }

    private async Task RefreshSnapshotAsync(ApplicationDbContext db, CancellationToken ct)
    {
        try
        {
            var pending = await db.OutboxMessages.AsNoTracking().CountAsync(m => m.Status == OutboxStatus.Pending, ct);
            var oldest = pending == 0 ? null : await db.OutboxMessages.AsNoTracking()
                .Where(m => m.Status == OutboxStatus.Pending).MinAsync(m => (DateTime?)m.OccurredAt, ct);
            PawzarooTelemetry.SetOutboxSnapshot(pending,
                oldest is null ? 0 : Math.Max(0, (DateTime.UtcNow - oldest.Value).TotalSeconds));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "[outbox] snapshot refresh failed");
        }
    }

    private async Task DispatchBatchAsync(IProducer<string, string> producer, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var batch = await ClaimBatchAsync(db, ct);
        await RefreshSnapshotAsync(db, ct);
        if (batch.Count == 0) return;

        using var activity = PawzarooTelemetry.StartActivity("outbox.dispatch_batch");
        activity?.SetTag("outbox.batch_size", batch.Count);

        foreach (var msg in batch)
        {
            try
            {
                var headers = new Headers
                {
                    new Header("event-type",  Encoding.UTF8.GetBytes(msg.EventType)),
                    new Header("event-id",    Encoding.UTF8.GetBytes(msg.Id.ToString())),
                    new Header("event-version", Encoding.UTF8.GetBytes(msg.Version)),
                };
                if (!string.IsNullOrEmpty(msg.CorrelationId))
                    headers.Add(new Header("correlation-id", Encoding.UTF8.GetBytes(msg.CorrelationId)));

                await producer.ProduceAsync(KafkaTopicNaming.Apply(_kafka.TopicPrefix, msg.Topic), new Message<string, string>
                {
                    Key     = msg.PartitionKey,
                    Value   = msg.Payload,
                    Headers = headers
                }, ct);

                msg.Status       = OutboxStatus.Dispatched;
                msg.DispatchedAt = DateTime.UtcNow;
                msg.LastError    = null;
                msg.ClaimedBy    = null;
                msg.ClaimedUntil = null;
                PawzarooTelemetry.OutboxDispatchLatencyMs.Record(Math.Max(0, (msg.DispatchedAt.Value - msg.OccurredAt).TotalMilliseconds));
                PawzarooTelemetry.OutboxDispatched.Add(1, new KeyValuePair<string, object?>("result", "dispatched"));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shutting down: release the lease so another replica can pick the row up immediately.
                msg.ClaimedBy = null;
                msg.ClaimedUntil = null;
                break;
            }
            catch (Exception ex)
            {
                msg.Attempts++;
                msg.LastError = Truncate(ex.Message, 2000);
                msg.ClaimedBy = null;
                msg.ClaimedUntil = null;

                if (FailureClassifier.ClassifyProducerFailure(ex) == FailureKind.Permanent)
                {
                    msg.Status = OutboxStatus.Poisoned;
                    PawzarooTelemetry.OutboxDispatched.Add(1, new KeyValuePair<string, object?>("result", "poisoned"));
                    _logger.LogError(ex, "[outbox] poisoned {EventId} (permanent failure): {Error}", msg.Id, msg.LastError);
                }
                else if (msg.Attempts >= MaxAttempts)
                {
                    msg.Status = OutboxStatus.Failed;
                    PawzarooTelemetry.OutboxDispatched.Add(1, new KeyValuePair<string, object?>("result", "failed"));
                    _logger.LogError(ex, "[outbox] giving up on {EventId} after {N} attempts", msg.Id, msg.Attempts);
                }
                else
                {
                    var backoff = OutboxBackoff.Delay(msg.Attempts, Random.Shared.NextDouble());
                    msg.NextAttemptAt = DateTime.UtcNow + backoff;
                    PawzarooTelemetry.OutboxDispatched.Add(1, new KeyValuePair<string, object?>("result", "retry"));
                    _logger.LogWarning(ex, "[outbox] dispatch failed for {EventId}, retry in {Backoff}", msg.Id, backoff);
                }
            }
        }
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
