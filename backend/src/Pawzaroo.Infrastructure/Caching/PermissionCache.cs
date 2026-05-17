using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// Permission set cache. The resolver pays one DB hit; subsequent requests
/// in the same window read from Redis. Invalidated on role-assign / role-edit.
/// </summary>
public class PermissionCache : IPermissionCache
{
    private readonly CacheHelper _cache;
    public PermissionCache(CacheHelper cache) => _cache = cache;

    public async Task<IReadOnlyCollection<string>?> GetAsync(Guid userId, CancellationToken ct = default)
        => await _cache.GetAsync<HashSet<string>>(RedisKeys.UserPermissions(userId));

    public Task SetAsync(Guid userId, IReadOnlyCollection<string> permissions, CancellationToken ct = default)
        => _cache.SetAsync(RedisKeys.UserPermissions(userId), permissions, RedisTtls.UserPermissions);

    public Task InvalidateAsync(Guid userId, CancellationToken ct = default)
        => _cache.RemoveAsync(RedisKeys.UserPermissions(userId));

    public Task InvalidateAllAsync(CancellationToken ct = default)
        => _cache.RemoveByPatternAsync($"{RedisKeys.Rbac}:*");
}
