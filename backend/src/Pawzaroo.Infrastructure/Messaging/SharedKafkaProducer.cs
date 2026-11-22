using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Pawzaroo.Infrastructure.Messaging;

/// <summary>
/// Raw (non-enveloped) producer used for dead-letter writes and re-drives, where the original
/// payload and headers must be preserved byte for byte. One producer per process.
/// </summary>
public interface IRawKafkaProducer
{
    /// <param name="topic">LOGICAL topic; <c>Kafka:TopicPrefix</c> is applied here.</param>
    Task ProduceAsync(string topic, string? key, string value,
        IEnumerable<KeyValuePair<string, byte[]>>? headers, CancellationToken ct = default);
}

public sealed class SharedKafkaProducer : IRawKafkaProducer, IDisposable
{
    private static readonly object Gate = new();
    private static SharedKafkaProducer? _default;

    private readonly KafkaOptions _options;
    private readonly Lazy<IProducer<string, string>> _producer;

    public SharedKafkaProducer(IOptions<KafkaOptions> options) : this(options.Value) { }

    public SharedKafkaProducer(KafkaOptions options)
    {
        _options = options;
        _producer = new Lazy<IProducer<string, string>>(Build, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Process-wide instance for callers that cannot take it from DI (e.g. consumer base class fallback).</summary>
    public static SharedKafkaProducer GetShared(KafkaOptions options)
    {
        lock (Gate) return _default ??= new SharedKafkaProducer(options);
    }

    private IProducer<string, string> Build()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
            CompressionType = CompressionType.Lz4,
            ClientId = $"pawzaroo-raw-{Environment.MachineName}"
        };
        if (!string.IsNullOrWhiteSpace(_options.SecurityProtocol)
            && Enum.TryParse<SecurityProtocol>(_options.SecurityProtocol, true, out var sp))
        {
            config.SecurityProtocol = sp;
            if (!string.IsNullOrWhiteSpace(_options.SaslMechanism)
                && Enum.TryParse<SaslMechanism>(_options.SaslMechanism, true, out var sasl))
            {
                config.SaslMechanism = sasl;
                config.SaslUsername = _options.SaslUsername;
                config.SaslPassword = _options.SaslPassword;
            }
        }
        return new ProducerBuilder<string, string>(config).Build();
    }

    public async Task ProduceAsync(string topic, string? key, string value,
        IEnumerable<KeyValuePair<string, byte[]>>? headers, CancellationToken ct = default)
    {
        var h = new Headers();
        if (headers is not null)
            foreach (var kv in headers) h.Add(kv.Key, kv.Value);

        await _producer.Value.ProduceAsync(KafkaTopicNaming.Apply(_options.TopicPrefix, topic), new Message<string, string>
        {
            Key = key!,
            Value = value,
            Headers = h
        }, ct);
    }

    public void Dispose()
    {
        if (!_producer.IsValueCreated) return;
        try { _producer.Value.Flush(TimeSpan.FromSeconds(5)); } catch { /* shutting down */ }
        _producer.Value.Dispose();
    }
}
