namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class RoleRepository(NebulaPanelDbContext context) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Roles
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await context.Roles
            .FirstOrDefaultAsync(r => r.Name == name, ct)
            .ConfigureAwait(false);
    }

    public async Task<Role?> GetWithPermissionsAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Roles
            .Include(r => r.Permissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Roles
            .Include(r => r.Permissions)
                .ThenInclude(rp => rp.Permission)
            .OrderByDescending(r => r.Priority)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Role role, CancellationToken ct = default)
    {
        await context.Roles.AddAsync(role, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Role role, CancellationToken ct = default)
    {
        context.Roles.Update(role);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Role role, CancellationToken ct = default)
    {
        context.Roles.Remove(role);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
