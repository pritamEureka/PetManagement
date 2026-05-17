using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// Implements <see cref="IRedisCacheService"/> for code that only needs the
/// generic GET/SET/DEL surface. All actual logic lives in <see cref="CacheHelper"/>.
/// </summary>
public class RedisCacheService : IRedisCacheService
{
    private readonly CacheHelper _cache;
    public RedisCacheService(CacheHelper cache) => _cache = cache;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => _cache.GetAsync<T>(key);
    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        => _cache.SetAsync(key, value, expiry);
    public Task RemoveAsync(string key, CancellationToken ct = default) => _cache.RemoveAsync(key);
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => _cache.ExistsAsync(key);
}
