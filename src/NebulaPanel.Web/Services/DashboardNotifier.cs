using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Web.Hubs;

namespace NebulaPanel.Web.Services;

/// <summary>
/// Service for broadcasting dashboard updates to connected clients via SignalR.
/// </summary>
public class DashboardNotifier(IHubContext<DashboardHub, IDashboardHubClient> hubContext) : ISystemHealthNotifier, IDockerPullNotifier
{
    private readonly IHubContext<DashboardHub, IDashboardHubClient> _hubContext = hubContext;

    /// <summary>
    /// Notifies all clients that a server's status has changed.
    /// </summary>
    public async Task NotifyServerStatusChangedAsync(Guid serverId, string serverName, string newStatus)
    {
        await _hubContext.Clients.All.ServerStatusChanged(serverId, serverName, newStatus).ConfigureAwait(false);
    }

    /// <summary>
    /// Notifies all clients that the dashboard summary has been updated.
    /// </summary>
    public async Task NotifySummaryUpdatedAsync(DashboardSummaryDto summary)
    {
        await _hubContext.Clients.All.SummaryUpdated(summary).ConfigureAwait(false);
    }

    /// <summary>
    /// Notifies all clients of a new activity.
    /// </summary>
    public async Task NotifyNewActivityAsync(ActivityFeedItemDto activity)
    {
        await _hubContext.Clients.All.NewActivity(activity).ConfigureAwait(false);
    }

    /// <summary>
    /// Notifies all clients of updated system health metrics.
    /// </summary>
    public async Task NotifySystemHealthUpdatedAsync(SystemHealthDto health)
    {
        await _hubContext.Clients.All.SystemHealthUpdated(health).ConfigureAwait(false);
    }

    /// <summary>
    /// Notifies all clients that a new announcement was added.
    /// </summary>
    public async Task NotifyAnnouncementAddedAsync(AnnouncementDto announcement)
    {
        await _hubContext.Clients.All.AnnouncementAdded(announcement).ConfigureAwait(false);
    }

    /// <summary>
    /// Notifies all clients that an announcement was removed.
    /// </summary>
    public async Task NotifyAnnouncementRemovedAsync(Guid announcementId)
    {
        await _hubContext.Clients.All.AnnouncementRemoved(announcementId).ConfigureAwait(false);
    }

    /// <summary>
    /// Notifies all clients that an announcement was updated.
    /// </summary>
    public async Task NotifyAnnouncementUpdatedAsync(AnnouncementDto announcement)
    {
        await _hubContext.Clients.All.AnnouncementUpdated(announcement).ConfigureAwait(false);
    }

    /// <summary>
    /// Notifies all clients of Docker image pull progress for a server.
    /// </summary>
    public async Task NotifyPullProgressAsync(Guid serverId, DockerPullProgressInfo progress)
    {
        var dto = new DockerPullProgressDto(
            progress.Status,
            progress.Layer,
            progress.BytesDownloaded,
            progress.TotalBytes,
            progress.OverallPercent,
            progress.LayersComplete,
            progress.LayersTotal);

        await _hubContext.Clients.All.DockerPullProgress(serverId, dto).ConfigureAwait(false);
    }

    /// <summary>
    /// Notifies all clients that a Docker image pull has completed.
    /// </summary>
    public async Task NotifyPullCompleteAsync(Guid serverId, bool success, string? error = null)
    {
        await _hubContext.Clients.All.DockerPullComplete(serverId, success, error).ConfigureAwait(false);
    }
}
