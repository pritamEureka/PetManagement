namespace Pawzaroo.Application.Common.Interfaces;

/// <summary>
/// Read-through cache for hot feed reads and aggregate counters. Implementations
/// are free to short-circuit Redis when unavailable — feeds must still serve.
/// </summary>
public interface IFeedCache
{
    Task<T?> GetFirstPageAsync<T>(string scope, CancellationToken ct = default);
    Task SetFirstPageAsync<T>(string scope, T page, TimeSpan? ttl = null, CancellationToken ct = default);

    Task<int?> GetReactionCountAsync(Guid postId, CancellationToken ct = default);
    Task SetReactionCountAsync(Guid postId, int value, CancellationToken ct = default);
    Task BumpReactionCountAsync(Guid postId, int delta, CancellationToken ct = default);

    Task<int?> GetCommentCountAsync(Guid postId, CancellationToken ct = default);
    Task SetCommentCountAsync(Guid postId, int value, CancellationToken ct = default);
    Task BumpCommentCountAsync(Guid postId, int delta, CancellationToken ct = default);

    /// <summary>Drop all scope-keyed first-page caches (used after writes).</summary>
    Task InvalidateFeedFirstPagesAsync(CancellationToken ct = default);

    Task<string?> GetUserCursorAsync(Guid userId, string scope, CancellationToken ct = default);
    Task SetUserCursorAsync(Guid userId, string scope, string cursor, CancellationToken ct = default);
}
