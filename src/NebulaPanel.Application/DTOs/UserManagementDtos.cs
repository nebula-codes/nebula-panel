namespace NebulaPanel.Application.DTOs;

using NebulaPanel.Domain.Enums;

public record UserListItemDto(
    Guid Id,
    string Username,
    string Email,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles,
    int OwnedServerCount
);

public record UserDetailDto(
    Guid Id,
    string Username,
    string Email,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<RoleSummaryDto> Roles,
    IReadOnlyList<OwnedServerDto> OwnedServers,
    IReadOnlyList<ServerPermissionSummaryDto> ServerPermissions
);

public record RoleSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    DateTime AssignedAt,
    string? AssignedByUsername
);

public record OwnedServerDto(
    Guid Id,
    string Name,
    string GameName,
    string? GameIcon
);

public record ServerPermissionSummaryDto(
    Guid ServerId,
    string ServerName,
    string PermissionCode,
    bool IsGranted
);

public record UserActivityDto(
    Guid Id,
    DateTime Timestamp,
    UserActivityType ActivityType,
    string Description,
    string? IpAddress,
    string? Metadata
);

public record AdminResetPasswordRequest(string NewPassword);

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int Priority
);
