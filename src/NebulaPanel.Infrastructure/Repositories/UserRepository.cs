namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class UserRepository(NebulaPanelDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.Username == username, ct)
            .ConfigureAwait(false);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.Email == email, ct)
            .ConfigureAwait(false);
    }

    public async Task<User?> GetWithRolesAndPermissionsAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Users
            .Include(u => u.Roles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
                        .ThenInclude(rp => rp.Permission)
            .Include(u => u.ServerPermissions)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Users
            .Include(u => u.Roles)
                .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Username)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await context.Users
            .AnyAsync(u => u.Username == username, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.Users
            .AnyAsync(u => u.Email == email, ct)
            .ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await context.Users.CountAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await context.Users.AddAsync(user, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(User user, CancellationToken ct = default)
    {
        context.Users.Remove(user);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
