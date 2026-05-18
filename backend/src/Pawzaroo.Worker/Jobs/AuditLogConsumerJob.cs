using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Messaging;
using Pawzaroo.Domain.Audit;
using Pawzaroo.Infrastructure.Messaging;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Worker.Jobs;

/// <summary>
/// Cross-cutting audit sink. Subscribes to *every* domain event topic and
/// writes a normalized <see cref="AuditEntry"/> row. Keeps the audit table
/// authoritative for compliance even if the originating service crashes
/// mid-flow.
/// </summary>
public class AuditLogConsumerJob : KafkaConsumerBase
{
    public AuditLogConsumerJob(IOptions<KafkaOptions> options, IServiceScopeFactory scopeFactory,
        ILogger<AuditLogConsumerJob> logger)
        : base(options, scopeFactory, logger) { }

    protected override string GroupId => KafkaConsumerGroups.AuditWriter;

    // Subscribe to every domain topic via the canonical catalog. Fully qualified
    // to disambiguate from Infrastructure's KafkaTopics options class.
    protected override IReadOnlyList<string> Topics =>
        Pawzaroo.Application.Common.Messaging.KafkaTopics.All();

    protected override async Task HandleAsync(string topic, JsonElement data, EventEnvelopeHeader header,
        IServiceProvider scope, CancellationToken ct)
    {
        var db = scope.GetRequiredService<ApplicationDbContext>();
        db.AuditEntries.Add(new AuditEntry
        {
            At            = header.OccurredAt,
            Action        = header.EventType,
            EntityName    = topic,
            EntityId      = (TryReadGuid(data, "id") ?? TryReadGuid(data, "entityId"))?.ToString(),
            UserId        = Guid.TryParse(header.UserId, out var uid) ? uid : null,
            NewValuesJson = data.GetRawText(),
            Module        = topic.Split('.').Skip(1).FirstOrDefault()
        });
        await db.SaveChangesAsync(ct);
    }

    private static Guid? TryReadGuid(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out var g) ? g : null;
    }
}
