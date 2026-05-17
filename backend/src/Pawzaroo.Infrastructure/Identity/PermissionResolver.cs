using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Identity;

public class PermissionResolver : IPermissionResolver
{
    private readonly ApplicationDbContext _db;
    private readonly IRedisCacheService _cache;

    public PermissionResolver(ApplicationDbContext db, IRedisCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"perms:{userId}";
        var cached = await _cache.GetAsync<string[]>(cacheKey, ct);
        if (cached is { Length: > 0 }) return cached;

        var perms = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Module + "." + rp.Permission.Action)
            .Distinct()
            .ToArrayAsync(ct);

        await _cache.SetAsync(cacheKey, perms, TimeSpan.FromMinutes(10), ct);
        return perms;
    }
}
