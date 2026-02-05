namespace NebulaPanel.Application.Services;

using NebulaPanel.Domain.Entities;

public interface IPermissionService
{
    /// <summary>
    /// Check if a user has a specific global permission via their roles.
    /// </summary>
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default);

    /// <summary>
    /// Check if a user has a specific permission for a server.
    /// This considers: role permissions, server ownership, and per-server permission overrides.
    /// </summary>
    Task<bool> HasServerPermissionAsync(Guid userId, Guid serverId, string permissionCode, CancellationToken ct = default);

    /// <summary>
    /// Check if a user is an admin (bypasses all permission checks).
    /// </summary>
    Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Check if a user owns the specified server.
    /// </summary>
    Task<bool> IsServerOwnerAsync(Guid userId, Guid serverId, CancellationToken ct = default);

    /// <summary>
    /// Get all permission codes for a user (from all their roles).
    /// </summary>
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get all role names for a user.
    /// </summary>
    Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default);
}
