using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Modules.Messaging;

/// <summary>
/// Redis-backed presence:
///   presence:user:{id}   SET of active connectionIds (TTL: 60s, refreshed via Touch)
///   presence:last:{id}   string (UTC ticks) — last seen timestamp
///
/// A user is online when their connection-id set is non-empty.
/// </summary>
public class PresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<PresenceService> _logger;
    private static readonly TimeSpan ConnectionTtl = TimeSpan.FromMinutes(2);

    public PresenceService(IConnectionMultiplexer redis, ILogger<PresenceService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private IDatabase Db => _redis.GetDatabase();

    public async Task TrackConnectionAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        try
        {
            var key = ConnKey(userId);
            await Db.SetAddAsync(key, connectionId);
            await Db.KeyExpireAsync(key, ConnectionTtl);
            await TouchLastSeenAsync(userId, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "PresenceService.Track failed"); }
    }

    public async Task UntrackConnectionAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        try
        {
            await Db.SetRemoveAsync(ConnKey(userId), connectionId);
            await TouchLastSeenAsync(userId, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "PresenceService.Untrack failed"); }
    }

    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        try { return await Db.SetLengthAsync(ConnKey(userId)) > 0; }
        catch (Exception ex) { _logger.LogWarning(ex, "PresenceService.IsOnline failed"); return false; }
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> AreOnlineAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToArray();
        var result = new Dictionary<Guid, bool>(ids.Length);
        if (ids.Length == 0) return result;

        try
        {
            var batch = Db.CreateBatch();
            var tasks = ids.ToDictionary(id => id, id => batch.SetLengthAsync(ConnKey(id)));
            batch.Execute();
            foreach (var (id, t) in tasks) result[id] = (await t) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PresenceService.AreOnline failed");
            foreach (var id in ids) result[id] = false;
        }
        return result;
    }

    public async Task<DateTime?> GetLastSeenAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var v = await Db.StringGetAsync(LastKey(userId));
            return v.IsNullOrEmpty ? null : new DateTime(long.Parse(v!), DateTimeKind.Utc);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "PresenceService.GetLastSeen failed"); return null; }
    }

    public async Task TouchLastSeenAsync(Guid userId, CancellationToken ct = default)
    {
        try { await Db.StringSetAsync(LastKey(userId), DateTime.UtcNow.Ticks.ToString(), TimeSpan.FromDays(30)); }
        catch (Exception ex) { _logger.LogWarning(ex, "PresenceService.TouchLastSeen failed"); }
    }

    private static string ConnKey(Guid uid) => $"presence:user:{uid}";
    private static string LastKey(Guid uid) => $"presence:last:{uid}";
}
