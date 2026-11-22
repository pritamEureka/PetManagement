using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pawzaroo.Infrastructure.Messaging;
using Pawzaroo.Infrastructure.Observability;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>Platform/ops features: shared raw Kafka producer, dead-letter admin, capacity probes.</summary>
public static class PlatformRegistration
{
    public static IServiceCollection AddPlatformFeatures(this IServiceCollection s)
    {
        // One producer for DLQ writes + re-drives (replaces the per-message producer).
        s.AddSingleton<SharedKafkaProducer>(sp =>
            SharedKafkaProducer.GetShared(sp.GetRequiredService<IOptions<KafkaOptions>>().Value));
        s.AddSingleton<IRawKafkaProducer>(sp => sp.GetRequiredService<SharedKafkaProducer>());

        s.AddScoped<DeadLetterService>();
        s.AddScoped<IDeadLetterService>(sp => sp.GetRequiredService<DeadLetterService>());
        s.AddScoped<IDeadLetterRecorder>(sp => sp.GetRequiredService<DeadLetterService>());

        s.AddScoped<IInfrastructureCapacityService, InfrastructureCapacityService>();
        return s;
    }
}
