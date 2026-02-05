namespace NebulaPanel.Application.Services;

using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Enums;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserListItemDto>> GetAllUsersAsync(CancellationToken ct = default);
    Task<Result<UserDetailDto>> GetUserDetailAsync(Guid userId, CancellationToken ct = default);

    Task<Result<Guid>> CreateUserAsync(CreateUserRequest request, Guid adminUserId, CancellationToken ct = default);
    Task<Result> UpdateUserAsync(Guid userId, UpdateUserRequest request, Guid adminUserId, CancellationToken ct = default);
    Task<Result> DeleteUserAsync(Guid userId, Guid adminUserId, CancellationToken ct = default);

    Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive, Guid adminUserId, CancellationToken ct = default);
    Task<Result> AdminResetPasswordAsync(Guid userId, string newPassword, Guid adminUserId, CancellationToken ct = default);

    Task<Result> AssignRoleAsync(Guid userId, Guid roleId, Guid adminUserId, CancellationToken ct = default);
    Task<Result> RemoveRoleAsync(Guid userId, Guid roleId, Guid adminUserId, CancellationToken ct = default);

    Task<IReadOnlyList<UserActivityDto>> GetUserActivityAsync(Guid userId, int limit = 50, CancellationToken ct = default);
    Task LogActivityAsync(
        Guid userId,
        UserActivityType activityType,
        string description,
        string? ipAddress = null,
        string? userAgent = null,
        string? metadata = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken ct = default);
}
