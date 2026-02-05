using NebulaPanel.Application.Common;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Event arguments for update status changes.
/// </summary>
public class UpdateStatusChangedEventArgs(UpdateStatus status) : EventArgs
{
    /// <summary>
    /// The new update status.
    /// </summary>
    public UpdateStatus Status { get; } = status;
}

/// <summary>
/// Service for checking, downloading, and applying panel updates.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Gets the current application version.
    /// </summary>
    VersionInfo CurrentVersion { get; }

    /// <summary>
    /// Gets the current update status.
    /// </summary>
    UpdateStatus Status { get; }

    /// <summary>
    /// Checks for available updates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the update check.</returns>
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific release.
    /// </summary>
    /// <param name="version">The version to get information for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The release information, or null if not found.</returns>
    Task<ReleaseInfo?> GetReleaseInfoAsync(string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an update package.
    /// </summary>
    /// <param name="version">The version to download.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the download operation.</returns>
    Task<UpdateDownloadResult> DownloadUpdateAsync(
        string version,
        IProgress<PanelUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a downloaded update. This will trigger application shutdown and restart.
    /// </summary>
    /// <param name="version">The version to apply.</param>
    /// <param name="options">Options for applying the update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the apply operation.</returns>
    Task<Result> ApplyUpdateAsync(
        string version,
        UpdateOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of available update backups.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available backups.</returns>
    Task<IReadOnlyList<UpdateBackup>> GetBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores from a backup.
    /// </summary>
    /// <param name="backupId">The ID of the backup to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the restore operation.</returns>
    Task<Result> RestoreBackupAsync(string backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a backup by ID.
    /// </summary>
    /// <param name="backupId">The ID of the backup to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> DeleteBackupAsync(string backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific backup by ID.
    /// </summary>
    /// <param name="backupId">The ID of the backup to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The backup if found, or null.</returns>
    Task<UpdateBackup?> GetBackupByIdAsync(string backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when the update status changes.
    /// </summary>
    event EventHandler<UpdateStatusChangedEventArgs>? StatusChanged;
}
