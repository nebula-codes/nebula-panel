namespace NebulaPanel.Domain.Repositories;

using NebulaPanel.Domain.Entities;

public interface IUserActivityRepository
{
    Task<IReadOnlyList<UserActivity>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<UserActivity>> GetRecentAsync(int limit = 50, CancellationToken ct = default);
    Task AddAsync(UserActivity activity, CancellationToken ct = default);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
