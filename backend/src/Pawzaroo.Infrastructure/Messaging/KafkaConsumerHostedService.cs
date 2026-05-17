using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Messaging;

namespace Pawzaroo.Infrastructure.Messaging;

/// <summary>
/// Catch-all logger consumer used by the API in dev. Subscribes to a handful
/// of topics and prints the envelope — handy when iterating on a producer
/// without standing up the worker. Production-grade consumers live in the
/// worker project (see <c>Pawzaroo.Worker.Consumers</c>) and use a dedicated
/// consumer group each.
/// </summary>
public class KafkaConsumerHostedService : KafkaConsumerBase
{
    public KafkaConsumerHostedService(IOptions<KafkaOptions> options, IServiceScopeFactory scopeFactory,
        ILogger<KafkaConsumerHostedService> logger)
        : base(options, scopeFactory, logger) { }

    protected override string GroupId => KafkaConsumerGroups.ApiInbox;

    protected override IReadOnlyList<string> Topics => new[]
    {
        Options.Topics.Notifications,
        Options.Topics.UserEvents,
        Options.Topics.PostEvents,
        Options.Topics.AdoptionEvents,
        Options.Topics.AppointmentEvents,
        Options.Topics.OrderEvents
    };

    protected override Task HandleAsync(string topic, JsonElement data, EventEnvelopeHeader header,
        IServiceProvider scope, CancellationToken ct)
    {
        Logger.LogInformation("[kafka {Topic}] {Type} v{Version} id={EventId} corr={Corr} body={Body}",
            topic, header.EventType, header.Version, header.EventId, header.CorrelationId, data.GetRawText());
        return Task.CompletedTask;
    }
}
