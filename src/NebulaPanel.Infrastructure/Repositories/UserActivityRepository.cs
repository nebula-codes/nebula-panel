namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class UserActivityRepository(NebulaPanelDbContext context) : IUserActivityRepository
{
    public async Task<IReadOnlyList<UserActivity>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken ct = default)
    {
        return await context.UserActivities
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserActivity>> GetRecentAsync(int limit = 50, CancellationToken ct = default)
    {
        return await context.UserActivities
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(UserActivity activity, CancellationToken ct = default)
    {
        await context.UserActivities.AddAsync(activity, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        return await context.UserActivities
            .Where(a => a.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
