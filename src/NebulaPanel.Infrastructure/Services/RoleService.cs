namespace NebulaPanel.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Constants;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class RoleService(
    NebulaPanelDbContext context,
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IServerPermissionRepository serverPermissionRepository,
    ILogger<RoleService> logger) : IRoleService
{
    private const string AdminRoleName = "Admin";

    public async Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken ct = default)
    {
        var roles = await context.Roles
            .Include(r => r.Permissions)
            .Include(r => r.Users)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return roles.Select(r => new RoleDto(
            r.Id,
            r.Name,
            r.Description,
            r.IsSystemRole,
            r.Priority
        )).ToList();
    }

    public async Task<Result<RoleDetailDto>> GetRoleDetailAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await context.Roles
            .Include(r => r.Permissions)
                .ThenInclude(rp => rp.Permission)
            .Include(r => r.Users)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct)
            .ConfigureAwait(false);

        if (role == null)
        {
            return Result.Failure<RoleDetailDto>("Role not found");
        }

        var assignedPermissionIds = role.Permissions.Select(rp => rp.PermissionId).ToHashSet();

        var allPermissions = await permissionRepository.GetAllAsync(ct).ConfigureAwait(false);

        var permissionDtos = allPermissions.Select(p => new PermissionDto(
            p.Id,
            p.Code,
            p.Name,
            p.Category,
            p.Description,
            assignedPermissionIds.Contains(p.Id)
        )).ToList();

        return new RoleDetailDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.Priority,
            permissionDtos,
            role.Users.Count
        );
    }

    public async Task<Result<RoleDetailDto>> CreateRoleAsync(CreateRoleRequest request, Guid adminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<RoleDetailDto>("Role name is required");
        }

        var existingRole = await roleRepository.GetByNameAsync(request.Name, ct).ConfigureAwait(false);
        if (existingRole != null)
        {
            return Result.Failure<RoleDetailDto>("A role with this name already exists");
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsSystemRole = false,
            Priority = request.Priority
        };

        await roleRepository.AddAsync(role, ct).ConfigureAwait(false);

        // Add permissions
        if (request.PermissionIds is { Count: > 0 })
        {
            foreach (var permissionId in request.PermissionIds)
            {
                var permission = await permissionRepository.GetByIdAsync(permissionId, ct).ConfigureAwait(false);
                if (permission != null)
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permissionId
                    });
                }
            }
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        logger.LogInformation("Admin {AdminId} created role {RoleId} ({RoleName})", adminUserId, role.Id, role.Name);

        return await GetRoleDetailAsync(role.Id, ct).ConfigureAwait(false);
    }

    public async Task<Result<RoleDetailDto>> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, Guid adminUserId, CancellationToken ct = default)
    {
        var role = await context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct)
            .ConfigureAwait(false);

        if (role == null)
        {
            return Result.Failure<RoleDetailDto>("Role not found");
        }

        if (role.IsSystemRole)
        {
            return Result.Failure<RoleDetailDto>("System roles cannot be modified");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<RoleDetailDto>("Role name is required");
        }

        // Check for duplicate name (excluding current role)
        var existingRole = await roleRepository.GetByNameAsync(request.Name, ct).ConfigureAwait(false);
        if (existingRole != null && existingRole.Id != roleId)
        {
            return Result.Failure<RoleDetailDto>("A role with this name already exists");
        }

        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim();
        role.Priority = request.Priority;

        // Update permissions
        var currentPermissionIds = role.Permissions.Select(rp => rp.PermissionId).ToHashSet();
        var newPermissionIds = request.PermissionIds.ToHashSet();

        // Remove permissions that are no longer assigned
        var permissionsToRemove = role.Permissions
            .Where(rp => !newPermissionIds.Contains(rp.PermissionId))
            .ToList();

        foreach (var permissionToRemove in permissionsToRemove)
        {
            context.RolePermissions.Remove(permissionToRemove);
        }

        // Add new permissions
        var permissionIdsToAdd = newPermissionIds.Except(currentPermissionIds);
        foreach (var permissionId in permissionIdsToAdd)
        {
            var permission = await permissionRepository.GetByIdAsync(permissionId, ct).ConfigureAwait(false);
            if (permission != null)
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permissionId
                });
            }
        }

        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} updated role {RoleId} ({RoleName})", adminUserId, role.Id, role.Name);

        return await GetRoleDetailAsync(role.Id, ct).ConfigureAwait(false);
    }

    public async Task<Result> DeleteRoleAsync(Guid roleId, Guid adminUserId, CancellationToken ct = default)
    {
        var role = await roleRepository.GetByIdAsync(roleId, ct).ConfigureAwait(false);
        if (role == null)
        {
            return Result.Failure("Role not found");
        }

        if (role.IsSystemRole)
        {
            return Result.Failure("System roles cannot be deleted");
        }

        // Check if any users are assigned to this role
        var usersWithRole = await context.UserRoles
            .CountAsync(ur => ur.RoleId == roleId, ct)
            .ConfigureAwait(false);

        if (usersWithRole > 0)
        {
            return Result.Failure($"Cannot delete role: {usersWithRole} user(s) are assigned to this role");
        }

        await roleRepository.DeleteAsync(role, ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} deleted role {RoleId} ({RoleName})", adminUserId, roleId, role.Name);

        return Result.Success();
    }

    public async Task<IReadOnlyList<PermissionCategoryDto>> GetAllPermissionsGroupedAsync(Guid? roleId = null, CancellationToken ct = default)
    {
        var allPermissions = await permissionRepository.GetAllAsync(ct).ConfigureAwait(false);

        HashSet<Guid>? assignedPermissionIds = null;
        if (roleId.HasValue)
        {
            var role = await context.Roles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.Id == roleId.Value, ct)
                .ConfigureAwait(false);

            assignedPermissionIds = role?.Permissions.Select(rp => rp.PermissionId).ToHashSet() ?? [];
        }

        var grouped = allPermissions
            .GroupBy(p => p.Category)
            .OrderBy(g => GetCategoryOrder(g.Key))
            .Select(g => new PermissionCategoryDto(
                g.Key,
                GetCategoryDisplayName(g.Key),
                GetCategoryDescription(g.Key),
                g.OrderBy(p => p.Name)
                    .Select(p => new PermissionDto(
                        p.Id,
                        p.Code,
                        p.Name,
                        p.Category,
                        p.Description,
                        assignedPermissionIds?.Contains(p.Id) ?? false
                    )).ToList()
            ))
            .ToList();

        return grouped;
    }

    public async Task<IReadOnlyList<RoleUserDto>> GetRoleUsersAsync(Guid roleId, CancellationToken ct = default)
    {
        var userRoles = await context.UserRoles
            .Include(ur => ur.User)
            .Where(ur => ur.RoleId == roleId)
            .OrderBy(ur => ur.User.Username)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var result = new List<RoleUserDto>();
        foreach (var ur in userRoles)
        {
            string? assignedByUsername = null;
            if (ur.AssignedBy.HasValue)
            {
                var assignedByUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Id == ur.AssignedBy.Value, ct)
                    .ConfigureAwait(false);
                assignedByUsername = assignedByUser?.Username;
            }

            result.Add(new RoleUserDto(
                ur.User.Id,
                ur.User.Username,
                ur.User.Email,
                ur.AssignedAt,
                assignedByUsername
            ));
        }

        return result;
    }

    public async Task<IReadOnlyList<ServerPermissionSummaryDto>> GetUserServerPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var permissions = await serverPermissionRepository.GetByUserAsync(userId, ct).ConfigureAwait(false);

        return permissions.Select(sp => new ServerPermissionSummaryDto(
            sp.ServerId,
            sp.Server?.Name ?? "Unknown",
            sp.PermissionCode,
            sp.IsGranted
        )).ToList();
    }

    public async Task<Result> SetServerPermissionAsync(Guid userId, Guid serverId, string permissionCode, bool isGranted, Guid adminUserId, CancellationToken ct = default)
    {
        var user = await context.Users.FindAsync([userId], ct).ConfigureAwait(false);
        if (user == null)
        {
            return Result.Failure("User not found");
        }

        var server = await context.GameServers.FindAsync([serverId], ct).ConfigureAwait(false);
        if (server == null)
        {
            return Result.Failure("Server not found");
        }

        var existingPermission = await serverPermissionRepository
            .GetAsync(userId, serverId, permissionCode, ct)
            .ConfigureAwait(false);

        if (existingPermission != null)
        {
            existingPermission.IsGranted = isGranted;
            await serverPermissionRepository.UpdateAsync(existingPermission, ct).ConfigureAwait(false);
        }
        else
        {
            var newPermission = new ServerPermission
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ServerId = serverId,
                PermissionCode = permissionCode,
                IsGranted = isGranted
            };
            await serverPermissionRepository.AddAsync(newPermission, ct).ConfigureAwait(false);
        }

        logger.LogInformation(
            "Admin {AdminId} set server permission {PermissionCode} to {IsGranted} for user {UserId} on server {ServerId}",
            adminUserId, permissionCode, isGranted, userId, serverId);

        return Result.Success();
    }

    public async Task<Result> RemoveServerPermissionAsync(Guid userId, Guid serverId, string permissionCode, Guid adminUserId, CancellationToken ct = default)
    {
        var existingPermission = await serverPermissionRepository
            .GetAsync(userId, serverId, permissionCode, ct)
            .ConfigureAwait(false);

        if (existingPermission == null)
        {
            return Result.Failure("Permission override not found");
        }

        await serverPermissionRepository.DeleteAsync(existingPermission, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Admin {AdminId} removed server permission override {PermissionCode} for user {UserId} on server {ServerId}",
            adminUserId, permissionCode, userId, serverId);

        return Result.Success();
    }

    public async Task<IReadOnlyList<EffectivePermissionDto>> GetEffectivePermissionsAsync(Guid userId, Guid serverId, CancellationToken ct = default)
    {
        var result = new List<EffectivePermissionDto>();

        // Check if user is admin
        var isAdmin = await context.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.Role.Name == AdminRoleName, ct)
            .ConfigureAwait(false);

        // Check if user owns the server
        var isOwner = await context.GameServers
            .AnyAsync(gs => gs.Id == serverId && gs.OwnerId == userId, ct)
            .ConfigureAwait(false);

        // Get user's role-based permissions
        var rolePermissions = await context.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.Permissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Get per-server permission overrides
        var serverOverrides = await serverPermissionRepository
            .GetByUserAndServerAsync(userId, serverId, ct)
            .ConfigureAwait(false);
        var overrideDict = serverOverrides.ToDictionary(sp => sp.PermissionCode, sp => sp.IsGranted);

        // Per-server permission codes
        var perServerPermissions = new[]
        {
            ("start", "Start Server"),
            ("stop", "Stop Server"),
            ("restart", "Restart Server"),
            ("console", "Access Console"),
            ("files", "Manage Files"),
            ("config", "Edit Configuration"),
            ("mods", "Manage Mods"),
            ("backup", "Manage Backups"),
            ("schedule", "Manage Schedules")
        };

        foreach (var (code, name) in perServerPermissions)
        {
            bool isGranted;
            PermissionSource source;

            if (isAdmin)
            {
                isGranted = true;
                source = PermissionSource.Admin;
            }
            else if (isOwner)
            {
                isGranted = true;
                source = PermissionSource.Ownership;
            }
            else if (overrideDict.TryGetValue(code, out var overrideValue))
            {
                isGranted = overrideValue;
                source = PermissionSource.ServerOverride;
            }
            else
            {
                // Check role-based permission using the template pattern
                var genericCode = $"servers.{{id}}.{code}";
                isGranted = rolePermissions.Contains(genericCode);
                source = PermissionSource.Role;
            }

            result.Add(new EffectivePermissionDto(code, name, isGranted, source));
        }

        return result;
    }

    private static int GetCategoryOrder(string category) => category.ToLowerInvariant() switch
    {
        "system" => 0,
        "users" => 1,
        "games" => 2,
        "servers" => 3,
        _ => 99
    };

    private static string GetCategoryDisplayName(string category) => category.ToLowerInvariant() switch
    {
        "system" => "System",
        "users" => "User Management",
        "games" => "Game Management",
        "servers" => "Server Management",
        _ => category
    };

    private static string? GetCategoryDescription(string category) => category.ToLowerInvariant() switch
    {
        "system" => "Core system functionality and monitoring",
        "users" => "User accounts and role management",
        "games" => "Game definitions and configurations",
        "servers" => "Game server operations and management",
        _ => null
    };
}
