using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Repositories;

/// <summary>
/// Repository for server activity tracking.
/// </summary>
public interface IServerActivityRepository
{
    /// <summary>
    /// Gets a server activity by its ID.
    /// </summary>
    Task<ServerActivity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent server activities across all servers.
    /// </summary>
    /// <param name="count">Maximum number of activities to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ServerActivity>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent activities for a specific server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="count">Maximum number of activities to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ServerActivity>> GetByServerIdAsync(Guid serverId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new server activity.
    /// </summary>
    Task AddAsync(ServerActivity activity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes activities older than the specified threshold.
    /// </summary>
    /// <param name="threshold">Activities before this date will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default);
}
