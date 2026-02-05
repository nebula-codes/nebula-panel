using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Repositories;

/// <summary>
/// Repository for historical resource usage data.
/// </summary>
public interface IResourceUsageHistoryRepository
{
    /// <summary>
    /// Adds a single resource usage record.
    /// </summary>
    Task AddAsync(ResourceUsageHistory usage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple resource usage records in batch for efficiency.
    /// </summary>
    Task AddBatchAsync(IEnumerable<ResourceUsageHistory> usages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resource usage history for a specific server within a time range.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="from">Start of the time range.</param>
    /// <param name="to">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ResourceUsageHistory>> GetByServerIdAsync(
        Guid serverId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resource usage history for all servers within a time range.
    /// </summary>
    /// <param name="from">Start of the time range.</param>
    /// <param name="to">End of the time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ResourceUsageHistory>> GetAllServersAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes records older than the specified threshold.
    /// </summary>
    /// <param name="threshold">Records before this date will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default);
}
