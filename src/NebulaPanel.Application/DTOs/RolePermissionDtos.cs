namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Detailed role information including permissions and user count.
/// </summary>
public record RoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int Priority,
    IReadOnlyList<PermissionDto> Permissions,
    int UserCount
);

/// <summary>
/// Permission representation for role assignment UI.
/// </summary>
public record PermissionDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string? Description,
    bool IsAssigned
);

/// <summary>
/// Permissions grouped by category for the tree view.
/// </summary>
public record PermissionCategoryDto(
    string Category,
    string DisplayName,
    string? Description,
    IReadOnlyList<PermissionDto> Permissions
);

/// <summary>
/// Request to create a new role.
/// </summary>
public record CreateRoleRequest(
    string Name,
    string? Description,
    int Priority,
    IReadOnlyList<Guid> PermissionIds
);

/// <summary>
/// Request to update an existing role.
/// </summary>
public record UpdateRoleRequest(
    string Name,
    string? Description,
    int Priority,
    IReadOnlyList<Guid> PermissionIds
);

/// <summary>
/// Effective permission for a user on a specific server.
/// </summary>
public record EffectivePermissionDto(
    string PermissionCode,
    string PermissionName,
    bool IsGranted,
    PermissionSource Source
);

/// <summary>
/// Source of a permission grant/deny decision.
/// </summary>
public enum PermissionSource
{
    /// <summary>Permission granted via role membership.</summary>
    Role,
    /// <summary>Permission overridden at server level.</summary>
    ServerOverride,
    /// <summary>Permission granted due to server ownership.</summary>
    Ownership,
    /// <summary>Permission granted due to Admin role (bypasses all checks).</summary>
    Admin
}

/// <summary>
/// User summary for role membership display.
/// </summary>
public record RoleUserDto(
    Guid Id,
    string Username,
    string Email,
    DateTime AssignedAt,
    string? AssignedByUsername
);
