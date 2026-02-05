namespace NebulaPanel.Infrastructure.Auth;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Application.Services;
using NebulaPanel.Infrastructure.Persistence;

public class PermissionService(NebulaPanelDbContext context) : IPermissionService
{
    private const string AdminRoleName = "Admin";

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default)
    {
        // Admin bypasses all permission checks
        if (await IsAdminAsync(userId, ct).ConfigureAwait(false))
        {
            return true;
        }

        // Check if user has the permission via any of their roles
        return await context.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.Permissions)
            .AnyAsync(rp => rp.Permission.Code == permissionCode, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasServerPermissionAsync(Guid userId, Guid serverId, string permissionCode, CancellationToken ct = default)
    {
        // Admin bypasses all permission checks
        if (await IsAdminAsync(userId, ct).ConfigureAwait(false))
        {
            return true;
        }

        // Server ownership grants full access
        if (await IsServerOwnerAsync(userId, serverId, ct).ConfigureAwait(false))
        {
            return true;
        }

        // Check for explicit per-server permission override
        var serverPermission = await context.ServerPermissions
            .FirstOrDefaultAsync(sp => sp.UserId == userId && sp.ServerId == serverId && sp.PermissionCode == permissionCode, ct)
            .ConfigureAwait(false);

        if (serverPermission != null)
        {
            // Explicit grant or deny
            return serverPermission.IsGranted;
        }

        // Fall back to global role-based permission
        // For server-specific permissions like "servers.{id}.start",
        // check if user has the generic version via roles
        var genericPermission = permissionCode.Replace(serverId.ToString(), "{id}");
        return await HasPermissionAsync(userId, genericPermission, ct).ConfigureAwait(false);
    }

    public async Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.UserRoles
            .Where(ur => ur.UserId == userId)
            .AnyAsync(ur => ur.Role.Name == AdminRoleName, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> IsServerOwnerAsync(Guid userId, Guid serverId, CancellationToken ct = default)
    {
        return await context.GameServers
            .AnyAsync(gs => gs.Id == serverId && gs.OwnerId == userId, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var permissions = await context.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.Permissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return permissions;
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var roles = await context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return roles;
    }
}
