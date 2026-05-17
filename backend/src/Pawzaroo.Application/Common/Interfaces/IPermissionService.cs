namespace Pawzaroo.Application.Common.Interfaces;

public record RoleDto(Guid Id, string Name, string? Description, bool IsSystem, IReadOnlyCollection<string> Permissions);
public record PermissionDto(Guid Id, string Module, string Action, string Code, string? Description);

public interface IPermissionService
{
    Task<IReadOnlyCollection<PermissionDto>> ListPermissionsAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<RoleDto>> ListRolesAsync(CancellationToken ct = default);
    Task<RoleDto?> GetRoleAsync(Guid roleId, CancellationToken ct = default);

    Task<Guid> CreateRoleAsync(string name, string? description, IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default);
    Task UpdateRoleAsync(Guid roleId, string? description, IReadOnlyCollection<string>? permissionCodes, CancellationToken ct = default);
    Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default);

    Task AssignRoleToUserAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task RevokeRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken ct = default);

    Task<IReadOnlyCollection<string>> GetPermissionsForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Enforces "users cannot grant permissions they don't hold themselves".
    /// SuperAdmin short-circuits. Throws <see cref="Pawzaroo.Shared.Exceptions.ForbiddenException"/> on violation.
    /// </summary>
    Task AssertCanGrantAsync(Guid actorId, IEnumerable<string> permissionCodes, CancellationToken ct = default);

    Task InvalidateUserCacheAsync(Guid userId, CancellationToken ct = default);
    Task InvalidateRoleCacheAsync(Guid roleId, CancellationToken ct = default);
}
