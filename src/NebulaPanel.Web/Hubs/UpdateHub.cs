using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Web.Hubs;

/// <summary>
/// DTO for update schedule notifications.
/// </summary>
public record UpdateScheduleNotification(
    Guid Id,
    string TargetVersion,
    DateTime ScheduledAt,
    string CreatedBy,
    bool CanCancel);

/// <summary>
/// SignalR hub client interface for update notifications.
/// </summary>
public interface IUpdateHubClient
{
    /// <summary>
    /// Called when the update status changes.
    /// </summary>
    Task UpdateStatusChanged(UpdateStatus status);

    /// <summary>
    /// Called when update progress changes during download or installation.
    /// </summary>
    Task UpdateProgressChanged(PanelUpdateProgress progress);

    /// <summary>
    /// Called when an update check completes.
    /// </summary>
    Task UpdateCheckCompleted(UpdateCheckResult result);

    /// <summary>
    /// Called when a new update is available.
    /// </summary>
    Task UpdateAvailable(ReleaseInfo release);

    /// <summary>
    /// Called when an update has been scheduled.
    /// </summary>
    Task UpdateScheduled(UpdateScheduleNotification schedule);

    /// <summary>
    /// Called when a scheduled update has been cancelled.
    /// </summary>
    Task UpdateScheduleCancelled(Guid scheduleId);

    /// <summary>
    /// Called during update countdown.
    /// </summary>
    Task UpdateCountdown(int secondsRemaining, string version);

    /// <summary>
    /// Called when an update is imminent (within seconds).
    /// </summary>
    Task UpdateImminent(string version, int secondsUntilRestart);

    /// <summary>
    /// Called after successful update to show release notes.
    /// </summary>
    Task ShowWhatsNew(string version, string releaseNotes);
}

/// <summary>
/// SignalR hub for real-time update notifications.
/// </summary>
[Authorize]
public class UpdateHub(
    IUpdateService updateService,
    ILogger<UpdateHub> logger) : Hub<IUpdateHubClient>
{
    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        logger.LogDebug("Client {ConnectionId} connected to UpdateHub", Context.ConnectionId);

        // Send current status to newly connected client
        await Clients.Caller.UpdateStatusChanged(updateService.Status).ConfigureAwait(false);

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            logger.LogWarning(exception, "Client {ConnectionId} disconnected from UpdateHub with error",
                Context.ConnectionId);
        }
        else
        {
            logger.LogDebug("Client {ConnectionId} disconnected from UpdateHub", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// Triggers an update check and returns the result to the caller.
    /// </summary>
    public async Task CheckForUpdates()
    {
        logger.LogDebug("Update check requested by client {ConnectionId}", Context.ConnectionId);

        var result = await updateService.CheckForUpdatesAsync().ConfigureAwait(false);
        await Clients.Caller.UpdateCheckCompleted(result).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current version information.
    /// </summary>
    public Task<VersionInfo> GetCurrentVersion()
    {
        return Task.FromResult(updateService.CurrentVersion);
    }

    /// <summary>
    /// Gets the current update status.
    /// </summary>
    public Task<UpdateStatus> GetStatus()
    {
        return Task.FromResult(updateService.Status);
    }
}
