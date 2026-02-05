using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Web.Hubs;

namespace NebulaPanel.Web.Services;

/// <summary>
/// Implementation of IUpdateNotifier that sends notifications via SignalR.
/// </summary>
public class UpdateNotifier(
    IHubContext<UpdateHub, IUpdateHubClient> hubContext,
    ILogger<UpdateNotifier> logger) : IUpdateNotifier
{
    /// <inheritdoc />
    public async Task NotifyStatusChangedAsync(UpdateStatus status, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting update status change: {Status}", status);
        await hubContext.Clients.All.UpdateStatusChanged(status).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task NotifyProgressChangedAsync(PanelUpdateProgress progress, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting update progress: {Phase} - {Percentage:F1}%", progress.Phase, progress.Percentage);
        await hubContext.Clients.All.UpdateProgressChanged(progress).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task NotifyCheckCompletedAsync(UpdateCheckResult result, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting update check completed. Update available: {UpdateAvailable}", result.UpdateAvailable);
        await hubContext.Clients.All.UpdateCheckCompleted(result).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task NotifyUpdateAvailableAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting update available: {Version}", release.Version);
        await hubContext.Clients.All.UpdateAvailable(release).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task NotifyUpdateScheduledAsync(
        Guid scheduleId,
        string targetVersion,
        DateTime scheduledAt,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Broadcasting update scheduled: {Version} at {ScheduledAt}",
            targetVersion, scheduledAt);

        var notification = new UpdateScheduleNotification(
            scheduleId,
            targetVersion,
            scheduledAt,
            createdBy,
            CanCancel: true);

        await hubContext.Clients.All.UpdateScheduled(notification).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task NotifyScheduleCancelledAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting schedule cancelled: {ScheduleId}", scheduleId);
        await hubContext.Clients.All.UpdateScheduleCancelled(scheduleId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task NotifyUpdateCountdownAsync(
        int secondsRemaining,
        string version,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting update countdown: {Seconds}s until {Version}", secondsRemaining, version);
        await hubContext.Clients.All.UpdateCountdown(secondsRemaining, version).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task NotifyShowWhatsNewAsync(
        string version,
        string releaseNotes,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting show what's new for version {Version}", version);
        await hubContext.Clients.All.ShowWhatsNew(version, releaseNotes).ConfigureAwait(false);
    }
}
