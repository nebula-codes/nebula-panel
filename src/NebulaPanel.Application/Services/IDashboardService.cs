using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for aggregating dashboard data.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Gets the server count summary for dashboard cards.
    /// </summary>
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the combined activity feed (server + user activities).
    /// </summary>
    /// <param name="count">Maximum number of activities to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ActivityFeedItemDto>> GetRecentActivityAsync(int count = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resource usage history for charts.
    /// </summary>
    /// <param name="period">Time period to retrieve (e.g., 24 hours).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ServerResourceChartDto>> GetResourceHistoryAsync(TimeSpan period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current system health metrics.
    /// </summary>
    Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts all stopped/crashed servers that the user has permission to start.
    /// </summary>
    Task<Result> StartAllServersAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all running servers that the user has permission to stop.
    /// </summary>
    Task<Result> StopAllServersAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all dashboard data in a single call.
    /// </summary>
    Task<DashboardDataDto> GetDashboardDataAsync(CancellationToken cancellationToken = default);
}
