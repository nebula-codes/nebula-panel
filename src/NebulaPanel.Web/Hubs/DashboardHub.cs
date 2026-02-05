using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Web.Hubs;

/// <summary>
/// SignalR hub client interface for dashboard real-time updates.
/// </summary>
public interface IDashboardHubClient
{
    /// <summary>
    /// Called when a server's status changes.
    /// </summary>
    Task ServerStatusChanged(Guid serverId, string serverName, string newStatus);

    /// <summary>
    /// Called when the dashboard summary needs to be updated.
    /// </summary>
    Task SummaryUpdated(DashboardSummaryDto summary);

    /// <summary>
    /// Called when a new activity is logged.
    /// </summary>
    Task NewActivity(ActivityFeedItemDto activity);

    /// <summary>
    /// Called when system health metrics are updated.
    /// </summary>
    Task SystemHealthUpdated(SystemHealthDto health);

    /// <summary>
    /// Called when a new announcement is added.
    /// </summary>
    Task AnnouncementAdded(AnnouncementDto announcement);

    /// <summary>
    /// Called when an announcement is removed.
    /// </summary>
    Task AnnouncementRemoved(Guid announcementId);

    /// <summary>
    /// Called when an announcement is updated.
    /// </summary>
    Task AnnouncementUpdated(AnnouncementDto announcement);

    /// <summary>
    /// Called when the health check status changes.
    /// </summary>
    Task HealthStatusChanged(string status, IReadOnlyList<string>? degradedChecks);
}

/// <summary>
/// SignalR hub for real-time dashboard updates.
/// </summary>
public class DashboardHub(
    IDashboardService dashboardService,
    HealthCheckService healthCheckService,
    ILogger<DashboardHub> logger) : Hub<IDashboardHubClient>
{
    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        logger.LogDebug("Client {ConnectionId} connected to DashboardHub", Context.ConnectionId);

        // Send current dashboard data to newly connected client
        try
        {
            var data = await dashboardService.GetDashboardDataAsync().ConfigureAwait(false);
            await Clients.Caller.SummaryUpdated(data.Summary).ConfigureAwait(false);
            await Clients.Caller.SystemHealthUpdated(data.SystemHealth).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending initial dashboard data to client {ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            logger.LogWarning(exception, "Client {ConnectionId} disconnected from DashboardHub with error",
                Context.ConnectionId);
        }
        else
        {
            logger.LogDebug("Client {ConnectionId} disconnected from DashboardHub", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current dashboard summary.
    /// </summary>
    public async Task<DashboardSummaryDto> GetSummary()
    {
        return await dashboardService.GetSummaryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current system health.
    /// </summary>
    public async Task<SystemHealthDto> GetSystemHealth()
    {
        return await dashboardService.GetSystemHealthAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Gets recent activity.
    /// </summary>
    public async Task<IReadOnlyList<ActivityFeedItemDto>> GetRecentActivity(int count = 10)
    {
        return await dashboardService.GetRecentActivityAsync(count).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current health check status.
    /// </summary>
    public async Task<HealthStatusDto> GetHealthStatus()
    {
        var report = await healthCheckService.CheckHealthAsync().ConfigureAwait(false);
        return new HealthStatusDto(
            Status: report.Status.ToString(),
            Timestamp: DateTime.UtcNow
        );
    }
}
