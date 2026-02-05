namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class ServerPermissionRepository(NebulaPanelDbContext context) : IServerPermissionRepository
{
    public async Task<ServerPermission?> GetAsync(Guid userId, Guid serverId, string permissionCode, CancellationToken ct = default)
    {
        return await context.ServerPermissions
            .FirstOrDefaultAsync(sp => sp.UserId == userId && sp.ServerId == serverId && sp.PermissionCode == permissionCode, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ServerPermission>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.ServerPermissions
            .Where(sp => sp.UserId == userId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ServerPermission>> GetByServerAsync(Guid serverId, CancellationToken ct = default)
    {
        return await context.ServerPermissions
            .Where(sp => sp.ServerId == serverId)
            .Include(sp => sp.User)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ServerPermission>> GetByUserAndServerAsync(Guid userId, Guid serverId, CancellationToken ct = default)
    {
        return await context.ServerPermissions
            .Where(sp => sp.UserId == userId && sp.ServerId == serverId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ServerPermission permission, CancellationToken ct = default)
    {
        await context.ServerPermissions.AddAsync(permission, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ServerPermission permission, CancellationToken ct = default)
    {
        context.ServerPermissions.Update(permission);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(ServerPermission permission, CancellationToken ct = default)
    {
        context.ServerPermissions.Remove(permission);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteByUserAndServerAsync(Guid userId, Guid serverId, CancellationToken ct = default)
    {
        var permissions = await context.ServerPermissions
            .Where(sp => sp.UserId == userId && sp.ServerId == serverId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        context.ServerPermissions.RemoveRange(permissions);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
