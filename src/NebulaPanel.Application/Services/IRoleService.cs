namespace NebulaPanel.Application.Services;

using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

/// <summary>
/// Service for managing roles and permissions.
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// Gets all roles with basic information.
    /// </summary>
    Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets detailed role information including permissions and user count.
    /// </summary>
    Task<Result<RoleDetailDto>> GetRoleDetailAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new role with the specified permissions.
    /// </summary>
    Task<Result<RoleDetailDto>> CreateRoleAsync(CreateRoleRequest request, Guid adminUserId, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing role. System roles cannot be modified.
    /// </summary>
    Task<Result<RoleDetailDto>> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, Guid adminUserId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a role. System roles cannot be deleted.
    /// </summary>
    Task<Result> DeleteRoleAsync(Guid roleId, Guid adminUserId, CancellationToken ct = default);

    /// <summary>
    /// Gets all permissions grouped by category, with assignment status for the specified role.
    /// </summary>
    Task<IReadOnlyList<PermissionCategoryDto>> GetAllPermissionsGroupedAsync(Guid? roleId = null, CancellationToken ct = default);

    /// <summary>
    /// Gets users assigned to a specific role.
    /// </summary>
    Task<IReadOnlyList<RoleUserDto>> GetRoleUsersAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>
    /// Gets all per-server permission overrides for a user.
    /// </summary>
    Task<IReadOnlyList<ServerPermissionSummaryDto>> GetUserServerPermissionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Sets a per-server permission override for a user.
    /// </summary>
    Task<Result> SetServerPermissionAsync(Guid userId, Guid serverId, string permissionCode, bool isGranted, Guid adminUserId, CancellationToken ct = default);

    /// <summary>
    /// Removes a per-server permission override for a user.
    /// </summary>
    Task<Result> RemoveServerPermissionAsync(Guid userId, Guid serverId, string permissionCode, Guid adminUserId, CancellationToken ct = default);

    /// <summary>
    /// Gets effective permissions for a user on a specific server, with source information.
    /// </summary>
    Task<IReadOnlyList<EffectivePermissionDto>> GetEffectivePermissionsAsync(Guid userId, Guid serverId, CancellationToken ct = default);
}
