namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class ResourceUsageHistoryRepository(NebulaPanelDbContext context) : IResourceUsageHistoryRepository
{
    public async Task AddAsync(ResourceUsageHistory usage, CancellationToken cancellationToken = default)
    {
        await context.ResourceUsageHistory.AddAsync(usage, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddBatchAsync(IEnumerable<ResourceUsageHistory> usages, CancellationToken cancellationToken = default)
    {
        await context.ResourceUsageHistory.AddRangeAsync(usages, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ResourceUsageHistory>> GetByServerIdAsync(
        Guid serverId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        return await context.ResourceUsageHistory
            .Where(r => r.ServerId == serverId)
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ResourceUsageHistory>> GetAllServersAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        return await context.ResourceUsageHistory
            .Include(r => r.Server)
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.ServerId)
            .ThenBy(r => r.Timestamp)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default)
    {
        await context.ResourceUsageHistory
            .Where(r => r.Timestamp < threshold)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
