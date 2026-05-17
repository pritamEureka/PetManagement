using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Pawzaroo.Application.Common.Messaging;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Messaging;

/// <summary>
/// EF-backed outbox writer. Adds a row to the <c>outbox_messages</c> table
/// using the *current* DbContext / transaction so the event ships atomically
/// with the business write. Use <see cref="IOutbox"/> instead of
/// <see cref="Pawzaroo.Application.Common.Interfaces.IKafkaProducer"/> whenever
/// the event represents a state change that callers care about — orders,
/// payments, approvals.
/// </summary>
public class Outbox : IOutbox
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor? _http;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Outbox(ApplicationDbContext db, IHttpContextAccessor? http = null)
    {
        _db = db;
        _http = http;
    }

    public Task EnqueueAsync<T>(string topic, T payload, string? partitionKey = null, CancellationToken ct = default)
        where T : IIntegrationEvent
    {
        var envelope = new EventEnvelope<T>(
            EventId: payload.EventId,
            EventType: typeof(T).Name,
            Version: "1",
            OccurredAt: payload.OccurredAt,
            CorrelationId: _http?.HttpContext?.Items["CorrelationId"] as string
                          ?? _http?.HttpContext?.TraceIdentifier,
            UserId: _http?.HttpContext?.User?.Identity?.Name,
            Data: payload);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id            = payload.EventId,
            Topic         = topic,
            EventType     = typeof(T).Name,
            Version       = "1",
            Payload       = JsonSerializer.Serialize(envelope, Json),
            PartitionKey  = partitionKey ?? payload.EventId.ToString(),
            OccurredAt    = payload.OccurredAt,
            Status        = OutboxStatus.Pending,
            NextAttemptAt = DateTime.UtcNow,
            CorrelationId = envelope.CorrelationId,
            UserId        = envelope.UserId is null ? null : Guid.TryParse(envelope.UserId, out var u) ? u : null
        });
        // No SaveChangesAsync here: caller's transaction commits the row.
        return Task.CompletedTask;
    }
}
