using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for sending real-time update notifications to connected clients.
/// </summary>
public interface IUpdateNotifier
{
    /// <summary>
    /// Notifies all connected clients that the update status has changed.
    /// </summary>
    /// <param name="status">The new update status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyStatusChangedAsync(UpdateStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all connected clients of update progress.
    /// </summary>
    /// <param name="progress">The current progress information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyProgressChangedAsync(PanelUpdateProgress progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all connected clients that an update check has completed.
    /// </summary>
    /// <param name="result">The result of the update check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyCheckCompletedAsync(UpdateCheckResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all connected clients that an update is available.
    /// </summary>
    /// <param name="release">Information about the available release.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyUpdateAvailableAsync(ReleaseInfo release, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all connected clients that an update has been scheduled.
    /// </summary>
    /// <param name="scheduleId">The schedule ID.</param>
    /// <param name="targetVersion">The target version.</param>
    /// <param name="scheduledAt">When the update is scheduled.</param>
    /// <param name="createdBy">Who created the schedule.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyUpdateScheduledAsync(
        Guid scheduleId,
        string targetVersion,
        DateTime scheduledAt,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all connected clients that a scheduled update has been cancelled.
    /// </summary>
    /// <param name="scheduleId">The cancelled schedule ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyScheduleCancelledAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all connected clients about an update countdown.
    /// </summary>
    /// <param name="secondsRemaining">Seconds until the update.</param>
    /// <param name="version">The version being updated to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyUpdateCountdownAsync(
        int secondsRemaining,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all connected clients to show the "What's New" information after an update.
    /// </summary>
    /// <param name="version">The new version.</param>
    /// <param name="releaseNotes">The release notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyShowWhatsNewAsync(
        string version,
        string releaseNotes,
        CancellationToken cancellationToken = default);
}
