namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class PermissionRepository(NebulaPanelDbContext context) : IPermissionRepository
{
    public async Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Permissions
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await context.Permissions
            .FirstOrDefaultAsync(p => p.Code == code, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Permissions
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Permission>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        return await context.Permissions
            .Where(p => p.Category == category)
            .OrderBy(p => p.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Permission permission, CancellationToken ct = default)
    {
        await context.Permissions.AddAsync(permission, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task AddRangeAsync(IEnumerable<Permission> permissions, CancellationToken ct = default)
    {
        await context.Permissions.AddRangeAsync(permissions, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
