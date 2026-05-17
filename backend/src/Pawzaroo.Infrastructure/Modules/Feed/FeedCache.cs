using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Modules.Feed;

/// <summary>
/// Read-through cache for hot feed reads and aggregate counters.
/// Keyspace:
///   feed:first:{scope}                                    string  TTL 60s
///   feed:reaction_count:{postId}                          string  no TTL
///   feed:comment_count:{postId}                           string  no TTL
///   feed:cursor:{userId}:{scope}                          string  TTL 1h
/// Resilient: any Redis exception is logged and swallowed; callers must still
/// be able to serve from the DB.
/// </summary>
public class FeedCache : IFeedCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<FeedCache> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly TimeSpan FirstPageTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CursorTtl    = TimeSpan.FromHours(1);

    public FeedCache(IConnectionMultiplexer redis, ILogger<FeedCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private IDatabase Db => _redis.GetDatabase();

    public async Task<T?> GetFirstPageAsync<T>(string scope, CancellationToken ct = default)
    {
        try
        {
            var v = await Db.StringGetAsync($"feed:first:{scope}");
            return v.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(v!, JsonOpts);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "FeedCache.GetFirstPage failed"); return default; }
    }

    public async Task SetFirstPageAsync<T>(string scope, T page, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(page, JsonOpts);
            await Db.StringSetAsync($"feed:first:{scope}", json, ttl ?? FirstPageTtl);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "FeedCache.SetFirstPage failed"); }
    }

    public async Task<int?> GetReactionCountAsync(Guid postId, CancellationToken ct = default)
    {
        try
        {
            var v = await Db.StringGetAsync($"feed:reaction_count:{postId}");
            return v.IsNullOrEmpty ? null : (int)v;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "FeedCache.GetReactionCount failed"); return null; }
    }

    public Task SetReactionCountAsync(Guid postId, int value, CancellationToken ct = default)
        => Safe(() => Db.StringSetAsync($"feed:reaction_count:{postId}", value));

    public Task BumpReactionCountAsync(Guid postId, int delta, CancellationToken ct = default)
        => Safe(() => Db.StringIncrementAsync($"feed:reaction_count:{postId}", delta));

    public async Task<int?> GetCommentCountAsync(Guid postId, CancellationToken ct = default)
    {
        try
        {
            var v = await Db.StringGetAsync($"feed:comment_count:{postId}");
            return v.IsNullOrEmpty ? null : (int)v;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "FeedCache.GetCommentCount failed"); return null; }
    }

    public Task SetCommentCountAsync(Guid postId, int value, CancellationToken ct = default)
        => Safe(() => Db.StringSetAsync($"feed:comment_count:{postId}", value));

    public Task BumpCommentCountAsync(Guid postId, int delta, CancellationToken ct = default)
        => Safe(() => Db.StringIncrementAsync($"feed:comment_count:{postId}", delta));

    public async Task InvalidateFeedFirstPagesAsync(CancellationToken ct = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: "feed:first:*").ToArray();
            if (keys.Length > 0) await Db.KeyDeleteAsync(keys);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "FeedCache.InvalidateFirstPages failed"); }
    }

    public async Task<string?> GetUserCursorAsync(Guid userId, string scope, CancellationToken ct = default)
    {
        try
        {
            var v = await Db.StringGetAsync($"feed:cursor:{userId}:{scope}");
            return v.IsNullOrEmpty ? null : (string?)v;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "FeedCache.GetUserCursor failed"); return null; }
    }

    public Task SetUserCursorAsync(Guid userId, string scope, string cursor, CancellationToken ct = default)
        => Safe(() => Db.StringSetAsync($"feed:cursor:{userId}:{scope}", cursor, CursorTtl));

    private async Task Safe(Func<Task> work)
    {
        try { await work(); }
        catch (Exception ex) { _logger.LogWarning(ex, "FeedCache write failed"); }
    }
}
