using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Messaging;

public record DeadLetterListItem(
    Guid Id, string Topic, int Partition, long Offset, string? Key, string ConsumerGroup, string Error,
    string FailureKind, int Attempts, DateTime FirstFailedAt, DateTime? LastFailedAt, string Status,
    int RedriveCount, DateTime? ResolvedAt);

public record DeadLetterDetail(
    Guid Id, string Topic, int Partition, long Offset, string? Key, string ConsumerGroup, string Error,
    string FailureKind, int Attempts, DateTime FirstFailedAt, DateTime? LastFailedAt, string Status,
    int RedriveCount, DateTime? ResolvedAt, string Payload, IReadOnlyDictionary<string, string> Headers);

public record DeadLetterPage(int Total, IReadOnlyList<DeadLetterListItem> Items);

/// <summary>What the consumer knows when it parks a message.</summary>
public record DeadLetterEntry(
    string Topic, int Partition, long Offset, string? Key, string Payload,
    IReadOnlyDictionary<string, string> Headers, string ConsumerGroup, string Error,
    FailureKind Kind, int Attempts, DateTime FirstFailedAt);

public interface IDeadLetterRecorder
{
    Task RecordAsync(DeadLetterEntry entry, CancellationToken ct = default);
}

public interface IDeadLetterService
{
    Task<DeadLetterPage> ListAsync(DeadLetterStatus? status, string? topic, int page, int pageSize, CancellationToken ct = default);
    Task<DeadLetterDetail?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Republishes to the original topic with header x-redrive-count. Returns false if not found or not Pending.</summary>
    Task<bool> RedriveAsync(Guid id, Guid? actorId, CancellationToken ct = default);

    /// <summary>Marks Discarded. Returns false if not found or not Pending.</summary>
    Task<bool> DiscardAsync(Guid id, Guid? actorId, CancellationToken ct = default);
}

public class DeadLetterService : IDeadLetterService, IDeadLetterRecorder
{
    public const string RedriveHeader = "x-redrive-count";
    private static readonly string[] StripOnRedrive = { "dlq-reason", "dlq-source-topic", RedriveHeader };

    private readonly ApplicationDbContext _db;
    private readonly IRawKafkaProducer _producer;

    private readonly string _topicPrefix;

    public DeadLetterService(ApplicationDbContext db, IRawKafkaProducer producer,
        Microsoft.Extensions.Options.IOptions<KafkaOptions>? options = null)
    {
        _db = db;
        _producer = producer;
        _topicPrefix = options?.Value.TopicPrefix ?? "";
    }

    public async Task RecordAsync(DeadLetterEntry e, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await _db.Set<DeadLetterRecord>().FirstOrDefaultAsync(r =>
            r.Topic == e.Topic && r.Partition == e.Partition && r.Offset == e.Offset
            && r.ConsumerGroup == e.ConsumerGroup && r.Status == DeadLetterStatus.Pending, ct);

        if (existing is null)
        {
            _db.Set<DeadLetterRecord>().Add(new DeadLetterRecord
            {
                Topic = e.Topic, Partition = e.Partition, Offset = e.Offset, Key = Trunc(e.Key, 512),
                Payload = e.Payload, HeadersJson = JsonSerializer.Serialize(e.Headers),
                ConsumerGroup = e.ConsumerGroup, Error = Trunc(e.Error, 4000)!, FailureKind = e.Kind.ToString(),
                Attempts = e.Attempts, FirstFailedAt = e.FirstFailedAt, LastFailedAt = now
            });
        }
        else
        {
            existing.Attempts += e.Attempts;
            existing.Error = Trunc(e.Error, 4000)!;
            existing.LastFailedAt = now;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<DeadLetterPage> ListAsync(DeadLetterStatus? status, string? topic, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = _db.Set<DeadLetterRecord>().AsNoTracking().AsQueryable();
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(topic)) q = q.Where(r => r.Topic == topic);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(r => r.FirstFailedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new DeadLetterPage(total, rows.Select(ToItem).ToList());
    }

    public async Task<DeadLetterDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _db.Set<DeadLetterRecord>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;
        return new DeadLetterDetail(r.Id, r.Topic, r.Partition, r.Offset, r.Key, r.ConsumerGroup, r.Error,
            r.FailureKind, r.Attempts, r.FirstFailedAt, r.LastFailedAt, r.Status.ToString(), r.RedriveCount,
            r.ResolvedAt, r.Payload, ParseHeaders(r.HeadersJson));
    }

    public async Task<bool> RedriveAsync(Guid id, Guid? actorId, CancellationToken ct = default)
    {
        var r = await _db.Set<DeadLetterRecord>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || r.Status != DeadLetterStatus.Pending) return false;

        var headers = ParseHeaders(r.HeadersJson)
            .Where(kv => !StripOnRedrive.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .Select(kv => new KeyValuePair<string, byte[]>(kv.Key, Encoding.UTF8.GetBytes(kv.Value)))
            .ToList();
        headers.Add(new(RedriveHeader, Encoding.UTF8.GetBytes((r.RedriveCount + 1).ToString())));

        // Records hold the LOGICAL topic; the raw producer applies the prefix. Strip defensively in case a physical name was stored.
        await _producer.ProduceAsync(KafkaTopicNaming.Strip(_topicPrefix, r.Topic), r.Key, r.Payload, headers, ct);

        r.RedriveCount++;
        r.Status = DeadLetterStatus.Redriven;
        r.ResolvedAt = DateTime.UtcNow;
        r.ResolvedBy = actorId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DiscardAsync(Guid id, Guid? actorId, CancellationToken ct = default)
    {
        var r = await _db.Set<DeadLetterRecord>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || r.Status != DeadLetterStatus.Pending) return false;
        r.Status = DeadLetterStatus.Discarded;
        r.ResolvedAt = DateTime.UtcNow;
        r.ResolvedBy = actorId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static DeadLetterListItem ToItem(DeadLetterRecord r)
        => new(r.Id, r.Topic, r.Partition, r.Offset, r.Key, r.ConsumerGroup, r.Error, r.FailureKind, r.Attempts,
            r.FirstFailedAt, r.LastFailedAt, r.Status.ToString(), r.RedriveCount, r.ResolvedAt);

    private static Dictionary<string, string> ParseHeaders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static string? Trunc(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
