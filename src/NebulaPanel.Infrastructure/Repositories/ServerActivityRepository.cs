namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class ServerActivityRepository(NebulaPanelDbContext context) : IServerActivityRepository
{
    public async Task<ServerActivity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.ServerActivities
            .Include(a => a.Server)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ServerActivity>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        return await context.ServerActivities
            .Include(a => a.Server)
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ServerActivity>> GetByServerIdAsync(Guid serverId, int count, CancellationToken cancellationToken = default)
    {
        return await context.ServerActivities
            .Include(a => a.Server)
            .Include(a => a.User)
            .Where(a => a.ServerId == serverId)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ServerActivity activity, CancellationToken cancellationToken = default)
    {
        await context.ServerActivities.AddAsync(activity, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default)
    {
        await context.ServerActivities
            .Where(a => a.Timestamp < threshold)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
