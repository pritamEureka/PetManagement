using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Modules.Messaging;

/// <summary>Short-TTL typing state that is visible to every API replica.</summary>
public interface ITypingStateService
{
    Task SetTypingAsync(Guid conversationId, Guid userId, bool isTyping, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetTypingAsync(Guid conversationId, CancellationToken ct = default);
}

/// <summary>
/// Presence + typing with an explicit degradation policy:
///   * Normal: Redis is the single source of truth, so every replica sees the same state.
///     presence:user:{id}  SET of connection ids, TTL 2 min, refreshed by the hub heartbeat.
///     typing:{conv}:{user} string key with a 6 s TTL (a lost "stopped typing" self-heals).
///   * Redis error: operations fall back to a process-local in-memory store and log a warning.
///     Presence is then only correct per replica (users on other replicas may appear offline)
///     — we prefer "possibly stale" over failing connect/disconnect. The local store is
///     authoritative only until Redis recovers; entries written locally are not replayed.
/// Interface IPresenceService is unchanged.
/// </summary>
public class ResilientPresenceService : IPresenceService, ITypingStateService
{
    private static readonly TimeSpan ConnectionTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TypingTtl = TimeSpan.FromSeconds(6);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ResilientPresenceService> _logger;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, DateTime>> _localConns = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _localLastSeen = new();
    private readonly ConcurrentDictionary<(Guid Conv, Guid User), DateTime> _localTyping = new();

    public ResilientPresenceService(IConnectionMultiplexer redis, ILogger<ResilientPresenceService> logger)
    { _redis = redis; _logger = logger; }

    private IDatabase Db => _redis.GetDatabase();
    private static string ConnKey(Guid u) => $"presence:user:{u}";
    private static string LastKey(Guid u) => $"presence:last:{u}";
    private static string TypingKey(Guid c, Guid u) => $"typing:{c}:{u}";

    private void Warn(Exception ex, string op) => _logger.LogWarning(ex, "Presence {Op}: Redis unavailable, using in-memory fallback", op);

    public async Task TrackConnectionAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        _localConns.GetOrAdd(userId, _ => new()).TryAdd(connectionId, DateTime.UtcNow);
        try
        {
            await Db.SetAddAsync(ConnKey(userId), connectionId);
            await Db.KeyExpireAsync(ConnKey(userId), ConnectionTtl);
        }
        catch (Exception ex) { Warn(ex, nameof(TrackConnectionAsync)); }
        await TouchLastSeenAsync(userId, ct);
    }

    public async Task UntrackConnectionAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        if (_localConns.TryGetValue(userId, out var set)) set.TryRemove(connectionId, out _);
        try { await Db.SetRemoveAsync(ConnKey(userId), connectionId); }
        catch (Exception ex) { Warn(ex, nameof(UntrackConnectionAsync)); }
        await TouchLastSeenAsync(userId, ct);
    }

    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        try { return await Db.SetLengthAsync(ConnKey(userId)) > 0; }
        catch (Exception ex) { Warn(ex, nameof(IsOnlineAsync)); return _localConns.TryGetValue(userId, out var s) && !s.IsEmpty; }
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
            Warn(ex, nameof(AreOnlineAsync));
            foreach (var id in ids) result[id] = _localConns.TryGetValue(id, out var s) && !s.IsEmpty;
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
        catch (Exception ex)
        {
            Warn(ex, nameof(GetLastSeenAsync));
            return _localLastSeen.TryGetValue(userId, out var t) ? t : null;
        }
    }

    /// <summary>Records last-seen and doubles as the heartbeat: it also refreshes the connection-set TTL.</summary>
    public async Task TouchLastSeenAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        _localLastSeen[userId] = now;
        try
        {
            await Db.StringSetAsync(LastKey(userId), now.Ticks.ToString(), TimeSpan.FromDays(30));
            await Db.KeyExpireAsync(ConnKey(userId), ConnectionTtl);
        }
        catch (Exception ex) { Warn(ex, nameof(TouchLastSeenAsync)); }
    }

    // ---------- typing ----------

    public async Task SetTypingAsync(Guid conversationId, Guid userId, bool isTyping, CancellationToken ct = default)
    {
        if (isTyping) _localTyping[(conversationId, userId)] = DateTime.UtcNow + TypingTtl;
        else _localTyping.TryRemove((conversationId, userId), out _);
        try
        {
            if (isTyping) await Db.StringSetAsync(TypingKey(conversationId, userId), "1", TypingTtl);
            else await Db.KeyDeleteAsync(TypingKey(conversationId, userId));
        }
        catch (Exception ex) { Warn(ex, nameof(SetTypingAsync)); }
    }

    public async Task<IReadOnlyList<Guid>> GetTypingAsync(Guid conversationId, CancellationToken ct = default)
    {
        try
        {
            var server = _redis.GetEndPoints().Select(e => _redis.GetServer(e)).FirstOrDefault(s => !s.IsReplica && s.IsConnected);
            if (server is null) throw new InvalidOperationException("no redis server");
            var prefix = $"typing:{conversationId}:";
            var ids = new List<Guid>();
            await foreach (var key in server.KeysAsync(pattern: prefix + "*", pageSize: 50))
                if (Guid.TryParse(key.ToString()[prefix.Length..], out var g)) ids.Add(g);
            return ids;
        }
        catch (Exception ex)
        {
            Warn(ex, nameof(GetTypingAsync));
            var now = DateTime.UtcNow;
            return _localTyping.Where(kv => kv.Key.Conv == conversationId && kv.Value > now).Select(kv => kv.Key.User).ToList();
        }
    }
}
