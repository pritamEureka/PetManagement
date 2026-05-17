using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Messaging;

namespace Pawzaroo.Infrastructure.Messaging;

/// <summary>
/// Idempotent Kafka producer. Wraps every payload in <see cref="EventEnvelope{T}"/>
/// so downstream consumers can dedupe by <c>EventId</c> and version
/// contracts. The <see cref="IIntegrationEvent.EventId"/> on the payload (if
/// present) is preferred over a fresh GUID — that lets producers be naturally
/// idempotent without forcing every event through the outbox.
///
/// Adds correlation-id and user-id headers from the ambient HttpContext when
/// available, so traces survive the hop into the worker.
/// </summary>
public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private readonly IHttpContextAccessor? _http;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public KafkaProducer(IOptions<KafkaOptions> options, ILogger<KafkaProducer> logger, IHttpContextAccessor? http = null)
    {
        var o = options.Value;
        var config = new ProducerConfig
        {
            BootstrapServers      = o.BootstrapServers,
            EnableIdempotence     = true,                  // exactly-once-ish: dedup within a producer session
            Acks                  = Acks.All,
            MessageSendMaxRetries = 5,
            LingerMs              = 5,
            CompressionType       = CompressionType.Lz4,
            ClientId              = $"pawzaroo-{Environment.MachineName}"
        };

        if (!string.IsNullOrWhiteSpace(o.SecurityProtocol)
            && Enum.TryParse<SecurityProtocol>(o.SecurityProtocol, true, out var sp))
        {
            config.SecurityProtocol = sp;
            if (!string.IsNullOrWhiteSpace(o.SaslMechanism)
                && Enum.TryParse<SaslMechanism>(o.SaslMechanism, true, out var sasl))
            {
                config.SaslMechanism = sasl;
                config.SaslUsername  = o.SaslUsername;
                config.SaslPassword  = o.SaslPassword;
            }
        }

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => logger.LogWarning("[kafka producer] {Reason}", e.Reason))
            .Build();
        _logger = logger;
        _http   = http;
    }

    public async Task PublishAsync<T>(string topic, T message, string? key = null, CancellationToken ct = default)
    {
        var (eventId, occurredAt) = ExtractIdentity(message);
        var envelope = new EventEnvelope<T>(
            EventId: eventId,
            EventType: typeof(T).Name,
            Version: "1",
            OccurredAt: occurredAt,
            CorrelationId: _http?.HttpContext?.Items["CorrelationId"] as string
                          ?? _http?.HttpContext?.TraceIdentifier,
            UserId: _http?.HttpContext?.User?.Identity?.Name,
            Data: message);

        var payload = JsonSerializer.Serialize(envelope, Json);
        var headers = new Headers
        {
            new Header("event-type", Encoding.UTF8.GetBytes(typeof(T).Name)),
            new Header("event-id",   Encoding.UTF8.GetBytes(envelope.EventId.ToString())),
            new Header("event-version", Encoding.UTF8.GetBytes(envelope.Version)),
        };
        if (envelope.CorrelationId is not null)
            headers.Add(new Header("correlation-id", Encoding.UTF8.GetBytes(envelope.CorrelationId)));

        try
        {
            var dr = await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key     = key ?? envelope.EventId.ToString(),
                Value   = payload,
                Headers = headers
            }, ct);
            _logger.LogDebug("[kafka] {Topic} partition={Partition} offset={Offset} type={Type}",
                dr.Topic, dr.Partition.Value, dr.Offset.Value, typeof(T).Name);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Kafka produce failed for {Topic}", topic);
            throw;
        }
    }

    private static (Guid, DateTime) ExtractIdentity<T>(T message)
    {
        if (message is IIntegrationEvent ie) return (ie.EventId, ie.OccurredAt);
        return (Guid.NewGuid(), DateTime.UtcNow);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
