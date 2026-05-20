using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Identity;

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _db;
    private readonly IRedisCacheService _cache;
    private readonly ICurrentUserService _current;
    private readonly IAuditLogger _audit;

    public PermissionService(ApplicationDbContext db, IRedisCacheService cache,
        ICurrentUserService current, IAuditLogger audit)
    {
        _db = db;
        _cache = cache;
        _current = current;
        _audit = audit;
    }

    public async Task<IReadOnlyCollection<PermissionDto>> ListPermissionsAsync(CancellationToken ct = default)
        => await _db.Permissions.AsNoTracking()
            .OrderBy(p => p.Module).ThenBy(p => p.Action)
            .Select(p => new PermissionDto(p.Id, p.Module, p.Action, p.Module + "." + p.Action, p.Description))
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<RoleDto>> ListRolesAsync(CancellationToken ct = default)
        => await _db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.IsSystem,
                r.RolePermissions.Select(rp => rp.Permission.Module + "." + rp.Permission.Action).ToArray()))
            .ToListAsync(ct);

    public async Task<RoleDto?> GetRoleAsync(Guid roleId, CancellationToken ct = default)
        => await _db.Roles.AsNoTracking().Where(r => r.Id == roleId)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.IsSystem,
                r.RolePermissions.Select(rp => rp.Permission.Module + "." + rp.Permission.Action).ToArray()))
            .SingleOrDefaultAsync(ct);

    public async Task<Guid> CreateRoleAsync(string name, string? description,
        IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default)
    {
        var actor = _current.UserId ?? throw new ForbiddenException();
        await AssertCanGrantAsync(actor, permissionCodes, ct);

        if (await _db.Roles.AnyAsync(r => r.Name == name, ct))
            throw new ConflictException($"Role '{name}' already exists.");

        var role = new Role { Name = name, Description = description, IsSystem = false };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);

        await ReplacePermissionsCoreAsync(role.Id, permissionCodes, ct);
        await _audit.LogAsync("create", "Role", role.Id.ToString(), "RBAC",
            newValues: new { role.Name, permissions = permissionCodes }, ct: ct);
        return role.Id;
    }

    public async Task UpdateRoleAsync(Guid roleId, string? description,
        IReadOnlyCollection<string>? permissionCodes, CancellationToken ct = default)
    {
        var actor = _current.UserId ?? throw new ForbiddenException();
        var role = await _db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(r => r.Id == roleId, ct)
            ?? throw new NotFoundException("Role", roleId);
        if (role.IsSystem && role.Name == SystemRoles.SuperAdmin)
            throw new ForbiddenException("SuperAdmin role permissions are immutable.");

        var oldPerms = role.RolePermissions.Select(rp => rp.Permission.Module + "." + rp.Permission.Action).ToArray();

        if (description is not null) role.Description = description;

        if (permissionCodes is not null)
        {
            await AssertCanGrantAsync(actor, permissionCodes, ct);
            await ReplacePermissionsCoreAsync(roleId, permissionCodes, ct);
        }

        await _db.SaveChangesAsync(ct);
        await InvalidateRoleCacheAsync(roleId, ct);
        await _audit.LogAsync("update", "Role", roleId.ToString(), "RBAC",
            oldValues: new { permissions = oldPerms },
            newValues: new { description, permissions = permissionCodes }, ct: ct);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await _db.Roles.SingleOrDefaultAsync(r => r.Id == roleId, ct)
            ?? throw new NotFoundException("Role", roleId);
        if (role.IsSystem)
            throw new ForbiddenException("System roles cannot be deleted.");
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);
        await InvalidateRoleCacheAsync(roleId, ct);
        await _audit.LogAsync("delete", "Role", roleId.ToString(), "RBAC",
            oldValues: new { role.Name }, ct: ct);
    }

    public async Task AssignRoleToUserAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var actor = _current.UserId ?? throw new ForbiddenException();
        var role = await _db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(r => r.Id == roleId, ct)
            ?? throw new NotFoundException("Role", roleId);

        var rolePerms = role.RolePermissions
            .Select(rp => rp.Permission.Module + "." + rp.Permission.Action).ToArray();
        await AssertCanGrantAsync(actor, rolePerms, ct);

        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct))
            throw new NotFoundException("User", userId);

        if (!await _db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct))
        {
            _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
            await _db.SaveChangesAsync(ct);
        }
        await InvalidateUserCacheAsync(userId, ct);
        // EntityId is varchar(64); a "userId:roleId" composite of two GUIDs is 73 chars
        // and would fail the insert. Store userId here; roleId is captured in newValues.
        await _audit.LogAsync("assign", "UserRole", userId.ToString(), "RBAC",
            newValues: new { userId, roleId, roleName = role.Name }, ct: ct);
    }

    public async Task RevokeRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var ur = await _db.UserRoles.SingleOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId, ct);
        if (ur is null) return;
        _db.UserRoles.Remove(ur);
        await _db.SaveChangesAsync(ct);
        await InvalidateUserCacheAsync(userId, ct);
        await _audit.LogAsync("revoke", "UserRole", userId.ToString(), "RBAC",
            oldValues: new { userId, roleId }, ct: ct);
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"perms:{userId}";
        var cached = await _cache.GetAsync<string[]>(cacheKey, ct);
        if (cached is { Length: > 0 }) return cached;

        // SuperAdmin -> implicitly every permission.
        var isSuper = await _db.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.Role.Name == SystemRoles.SuperAdmin, ct);
        string[] perms = isSuper
            ? await _db.Permissions.Select(p => p.Module + "." + p.Action).ToArrayAsync(ct)
            : await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Module + "." + rp.Permission.Action)
                .Distinct().ToArrayAsync(ct);

        await _cache.SetAsync(cacheKey, perms, TimeSpan.FromMinutes(10), ct);
        return perms;
    }

    public async Task AssertCanGrantAsync(Guid actorId, IEnumerable<string> permissionCodes, CancellationToken ct = default)
    {
        var isSuper = await _db.UserRoles
            .AnyAsync(ur => ur.UserId == actorId && ur.Role.Name == SystemRoles.SuperAdmin, ct);
        if (isSuper) return;

        var actorPerms = (await GetPermissionsForUserAsync(actorId, ct)).ToHashSet();
        var requested = permissionCodes.Distinct().ToArray();
        var missing = requested.Where(p => !actorPerms.Contains(p)).ToArray();
        if (missing.Length > 0)
            throw new ForbiddenException(
                $"Cannot grant permissions you do not hold: {string.Join(", ", missing)}");
    }

    public Task InvalidateUserCacheAsync(Guid userId, CancellationToken ct = default)
        => _cache.RemoveAsync($"perms:{userId}", ct);

    public async Task InvalidateRoleCacheAsync(Guid roleId, CancellationToken ct = default)
    {
        var affected = await _db.UserRoles.Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId).ToListAsync(ct);
        foreach (var uid in affected)
            await _cache.RemoveAsync($"perms:{uid}", ct);
    }

    private async Task ReplacePermissionsCoreAsync(Guid roleId, IEnumerable<string> codes, CancellationToken ct)
    {
        var existing = _db.RolePermissions.Where(rp => rp.RoleId == roleId);
        _db.RolePermissions.RemoveRange(existing);
        var ids = await _db.Permissions
            .Where(p => codes.Contains(p.Module + "." + p.Action))
            .Select(p => p.Id).ToListAsync(ct);
        foreach (var pid in ids)
            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = pid });
        await _db.SaveChangesAsync(ct);
    }
}
