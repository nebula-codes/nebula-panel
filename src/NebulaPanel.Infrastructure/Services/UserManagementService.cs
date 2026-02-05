namespace NebulaPanel.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class UserManagementService(
    NebulaPanelDbContext context,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUserActivityRepository activityRepository,
    IAuthService authService,
    ILogger<UserManagementService> logger) : IUserManagementService
{
    public async Task<IReadOnlyList<UserListItemDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var users = await context.Users
            .Include(u => u.Roles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.OwnedServers)
            .OrderBy(u => u.Username)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return users.Select(u => new UserListItemDto(
            u.Id,
            u.Username,
            u.Email,
            u.IsActive,
            u.CreatedAt,
            u.LastLoginAt,
            u.Roles.Select(r => r.Role.Name).ToList(),
            u.OwnedServers.Count
        )).ToList();
    }

    public async Task<Result<UserDetailDto>> GetUserDetailAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await context.Users
            .Include(u => u.Roles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.OwnedServers)
                .ThenInclude(s => s.Game)
            .Include(u => u.ServerPermissions)
                .ThenInclude(sp => sp.Server)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);

        if (user == null)
        {
            return Result.Failure<UserDetailDto>("User not found");
        }

        var roleSummaries = new List<RoleSummaryDto>();
        foreach (var ur in user.Roles)
        {
            string? assignedByUsername = null;
            if (ur.AssignedBy.HasValue)
            {
                var assignedByUser = await userRepository.GetByIdAsync(ur.AssignedBy.Value, ct).ConfigureAwait(false);
                assignedByUsername = assignedByUser?.Username;
            }

            roleSummaries.Add(new RoleSummaryDto(
                ur.Role.Id,
                ur.Role.Name,
                ur.Role.Description,
                ur.Role.IsSystemRole,
                ur.AssignedAt,
                assignedByUsername
            ));
        }

        var ownedServers = user.OwnedServers.Select(s => new OwnedServerDto(
            s.Id,
            s.Name,
            s.Game?.Name ?? "Unknown",
            s.Game?.IconPath
        )).ToList();

        var serverPermissions = user.ServerPermissions.Select(sp => new ServerPermissionSummaryDto(
            sp.ServerId,
            sp.Server?.Name ?? "Unknown",
            sp.PermissionCode,
            sp.IsGranted
        )).ToList();

        return new UserDetailDto(
            user.Id,
            user.Username,
            user.Email,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt,
            roleSummaries,
            ownedServers,
            serverPermissions
        );
    }

    public async Task<Result<Guid>> CreateUserAsync(CreateUserRequest request, Guid adminUserId, CancellationToken ct = default)
    {
        if (await userRepository.ExistsByUsernameAsync(request.Username, ct).ConfigureAwait(false))
        {
            return Result.Failure<Guid>("Username is already taken");
        }

        if (await userRepository.ExistsByEmailAsync(request.Email, ct).ConfigureAwait(false))
        {
            return Result.Failure<Guid>("Email is already registered");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await userRepository.AddAsync(user, ct).ConfigureAwait(false);

        if (request.RoleIds is { Count: > 0 })
        {
            foreach (var roleId in request.RoleIds)
            {
                var role = await roleRepository.GetByIdAsync(roleId, ct).ConfigureAwait(false);
                if (role != null)
                {
                    context.UserRoles.Add(new UserRole
                    {
                        UserId = user.Id,
                        RoleId = roleId,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = adminUserId
                    });
                }
            }
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        await LogActivityAsync(user.Id, UserActivityType.AccountCreated,
            $"Account created by administrator",
            metadata: $"{{\"AdminId\":\"{adminUserId}\"}}",
            ct: ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} created user {UserId} ({Username})", adminUserId, user.Id, user.Username);

        return user.Id;
    }

    public async Task<Result> UpdateUserAsync(Guid userId, UpdateUserRequest request, Guid adminUserId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user == null)
        {
            return Result.Failure("User not found");
        }

        var changes = new List<string>();

        if (!string.IsNullOrEmpty(request.Username) && request.Username != user.Username)
        {
            if (await userRepository.ExistsByUsernameAsync(request.Username, ct).ConfigureAwait(false))
            {
                return Result.Failure("Username is already taken");
            }
            user.Username = request.Username;
            changes.Add("Username");
        }

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            if (await userRepository.ExistsByEmailAsync(request.Email, ct).ConfigureAwait(false))
            {
                return Result.Failure("Email is already registered");
            }
            user.Email = request.Email;
            changes.Add("Email");
        }

        if (request.IsActive.HasValue && request.IsActive.Value != user.IsActive)
        {
            if (userId == adminUserId)
            {
                return Result.Failure("You cannot change your own active status");
            }
            user.IsActive = request.IsActive.Value;
            changes.Add("IsActive");

            var statusType = user.IsActive ? UserActivityType.AccountEnabled : UserActivityType.AccountDisabled;
            await LogActivityAsync(userId, statusType,
                $"Account {(user.IsActive ? "enabled" : "disabled")} by administrator",
                metadata: $"{{\"AdminId\":\"{adminUserId}\"}}",
                ct: ct).ConfigureAwait(false);
        }

        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);

        if (request.RoleIds != null)
        {
            var currentRoles = await context.UserRoles
                .Where(ur => ur.UserId == userId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var currentRoleIds = currentRoles.Select(r => r.RoleId).ToHashSet();
            var newRoleIds = request.RoleIds.ToHashSet();

            var rolesToRemove = currentRoles.Where(r => !newRoleIds.Contains(r.RoleId)).ToList();
            var roleIdsToAdd = newRoleIds.Except(currentRoleIds).ToList();

            foreach (var roleToRemove in rolesToRemove)
            {
                context.UserRoles.Remove(roleToRemove);
                var role = await roleRepository.GetByIdAsync(roleToRemove.RoleId, ct).ConfigureAwait(false);
                await LogActivityAsync(userId, UserActivityType.RoleRemoved,
                    $"Role '{role?.Name ?? "Unknown"}' removed by administrator",
                    metadata: $"{{\"RoleId\":\"{roleToRemove.RoleId}\",\"AdminId\":\"{adminUserId}\"}}",
                    ct: ct).ConfigureAwait(false);
            }

            foreach (var roleId in roleIdsToAdd)
            {
                var role = await roleRepository.GetByIdAsync(roleId, ct).ConfigureAwait(false);
                if (role != null)
                {
                    context.UserRoles.Add(new UserRole
                    {
                        UserId = userId,
                        RoleId = roleId,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = adminUserId
                    });
                    await LogActivityAsync(userId, UserActivityType.RoleAssigned,
                        $"Role '{role.Name}' assigned by administrator",
                        metadata: $"{{\"RoleId\":\"{roleId}\",\"AdminId\":\"{adminUserId}\"}}",
                        ct: ct).ConfigureAwait(false);
                }
            }

            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        if (changes.Count > 0)
        {
            await LogActivityAsync(userId, UserActivityType.ProfileUpdated,
                $"Profile updated by administrator ({string.Join(", ", changes)})",
                metadata: $"{{\"ChangedFields\":\"{string.Join(",", changes)}\",\"AdminId\":\"{adminUserId}\"}}",
                ct: ct).ConfigureAwait(false);
        }

        logger.LogInformation("Admin {AdminId} updated user {UserId}", adminUserId, userId);

        return Result.Success();
    }

    public async Task<Result> DeleteUserAsync(Guid userId, Guid adminUserId, CancellationToken ct = default)
    {
        if (userId == adminUserId)
        {
            return Result.Failure("You cannot delete your own account");
        }

        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user == null)
        {
            return Result.Failure("User not found");
        }

        await userRepository.DeleteAsync(user, ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} deleted user {UserId} ({Username})", adminUserId, userId, user.Username);

        return Result.Success();
    }

    public async Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive, Guid adminUserId, CancellationToken ct = default)
    {
        if (userId == adminUserId)
        {
            return Result.Failure("You cannot change your own active status");
        }

        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user == null)
        {
            return Result.Failure("User not found");
        }

        if (user.IsActive == isActive)
        {
            return Result.Success();
        }

        user.IsActive = isActive;
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);

        if (!isActive)
        {
            await authService.RevokeRefreshTokenAsync(userId, ct: ct).ConfigureAwait(false);
        }

        var activityType = isActive ? UserActivityType.AccountEnabled : UserActivityType.AccountDisabled;
        await LogActivityAsync(userId, activityType,
            $"Account {(isActive ? "enabled" : "disabled")} by administrator",
            metadata: $"{{\"AdminId\":\"{adminUserId}\"}}",
            ct: ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} {Action} user {UserId}", adminUserId, isActive ? "enabled" : "disabled", userId);

        return Result.Success();
    }

    public async Task<Result> AdminResetPasswordAsync(Guid userId, string newPassword, Guid adminUserId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user == null)
        {
            return Result.Failure("User not found");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);

        await authService.RevokeRefreshTokenAsync(userId, ct: ct).ConfigureAwait(false);

        await LogActivityAsync(userId, UserActivityType.PasswordResetByAdmin,
            "Password reset by administrator",
            metadata: $"{{\"AdminId\":\"{adminUserId}\"}}",
            ct: ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} reset password for user {UserId}", adminUserId, userId);

        return Result.Success();
    }

    public async Task<Result> AssignRoleAsync(Guid userId, Guid roleId, Guid adminUserId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user == null)
        {
            return Result.Failure("User not found");
        }

        var role = await roleRepository.GetByIdAsync(roleId, ct).ConfigureAwait(false);
        if (role == null)
        {
            return Result.Failure("Role not found");
        }

        var existingAssignment = await context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct)
            .ConfigureAwait(false);

        if (existingAssignment != null)
        {
            return Result.Failure("User already has this role");
        }

        context.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = adminUserId
        });
        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        await LogActivityAsync(userId, UserActivityType.RoleAssigned,
            $"Role '{role.Name}' assigned by administrator",
            metadata: $"{{\"RoleId\":\"{roleId}\",\"RoleName\":\"{role.Name}\",\"AdminId\":\"{adminUserId}\"}}",
            ct: ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} assigned role {RoleName} to user {UserId}", adminUserId, role.Name, userId);

        return Result.Success();
    }

    public async Task<Result> RemoveRoleAsync(Guid userId, Guid roleId, Guid adminUserId, CancellationToken ct = default)
    {
        var assignment = await context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct)
            .ConfigureAwait(false);

        if (assignment == null)
        {
            return Result.Failure("User does not have this role");
        }

        var role = await roleRepository.GetByIdAsync(roleId, ct).ConfigureAwait(false);

        context.UserRoles.Remove(assignment);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        await LogActivityAsync(userId, UserActivityType.RoleRemoved,
            $"Role '{role?.Name ?? "Unknown"}' removed by administrator",
            metadata: $"{{\"RoleId\":\"{roleId}\",\"RoleName\":\"{role?.Name ?? "Unknown"}\",\"AdminId\":\"{adminUserId}\"}}",
            ct: ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} removed role {RoleName} from user {UserId}", adminUserId, role?.Name ?? "Unknown", userId);

        return Result.Success();
    }

    public async Task<IReadOnlyList<UserActivityDto>> GetUserActivityAsync(Guid userId, int limit = 50, CancellationToken ct = default)
    {
        var activities = await activityRepository.GetByUserIdAsync(userId, limit, ct).ConfigureAwait(false);

        return activities.Select(a => new UserActivityDto(
            a.Id,
            a.Timestamp,
            a.ActivityType,
            a.Description,
            a.IpAddress,
            a.Metadata
        )).ToList();
    }

    public async Task LogActivityAsync(
        Guid userId,
        UserActivityType activityType,
        string description,
        string? ipAddress = null,
        string? userAgent = null,
        string? metadata = null,
        CancellationToken ct = default)
    {
        var activity = new UserActivity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            ActivityType = activityType,
            Description = description,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Metadata = metadata
        };

        await activityRepository.AddAsync(activity, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken ct = default)
    {
        var roles = await roleRepository.GetAllAsync(ct).ConfigureAwait(false);

        return roles.Select(r => new RoleDto(
            r.Id,
            r.Name,
            r.Description,
            r.IsSystemRole,
            r.Priority
        )).ToList();
    }
}
