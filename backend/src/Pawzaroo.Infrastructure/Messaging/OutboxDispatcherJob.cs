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

    private async Task DispatchBatchAsync(IProducer<string, string> producer, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var batch = await db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0) return;

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

                await producer.ProduceAsync(msg.Topic, new Message<string, string>
                {
                    Key     = msg.PartitionKey,
                    Value   = msg.Payload,
                    Headers = headers
                }, ct);

                msg.Status       = OutboxStatus.Dispatched;
                msg.DispatchedAt = DateTime.UtcNow;
                msg.LastError    = null;
            }
            catch (Exception ex)
            {
                msg.Attempts++;
                msg.LastError = Truncate(ex.Message, 2000);
                if (msg.Attempts >= MaxAttempts)
                {
                    msg.Status = OutboxStatus.Failed;
                    _logger.LogError(ex, "[outbox] giving up on {EventId} after {N} attempts", msg.Id, msg.Attempts);
                }
                else
                {
                    var backoff = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, msg.Attempts)));
                    msg.NextAttemptAt = DateTime.UtcNow + backoff;
                    _logger.LogWarning(ex, "[outbox] dispatch failed for {EventId}, retry in {Backoff}", msg.Id, backoff);
                }
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
