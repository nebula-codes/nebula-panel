using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for logging and retrieving server activities.
/// </summary>
public interface IServerActivityService
{
    /// <summary>
    /// Logs a server activity event.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="activityType">The type of activity.</param>
    /// <param name="description">Human-readable description.</param>
    /// <param name="userId">The user who initiated the activity (null for system events).</param>
    /// <param name="metadata">Optional JSON metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogActivityAsync(
        Guid serverId,
        ServerActivityType activityType,
        string description,
        Guid? userId = null,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent server activities.
    /// </summary>
    /// <param name="count">Maximum number of activities to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ActivityFeedItemDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent activities for a specific server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="count">Maximum number of activities to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ActivityFeedItemDto>> GetByServerAsync(Guid serverId, int count, CancellationToken cancellationToken = default);
}
