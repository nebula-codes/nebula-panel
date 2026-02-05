using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service interface for managing server backups.
/// </summary>
public interface IBackupService
{
    #region Query Operations

    /// <summary>
    /// Gets all backups across all servers.
    /// </summary>
    Task<IReadOnlyList<BackupListItemDto>> GetAllBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all backups for a specific server.
    /// </summary>
    Task<IReadOnlyList<BackupListItemDto>> GetBackupsByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a backup by its ID.
    /// </summary>
    Task<Result<BackupDto>> GetBackupByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets backup summary statistics for a server.
    /// </summary>
    Task<BackupSummaryDto> GetServerBackupSummaryAsync(Guid serverId, CancellationToken cancellationToken = default);

    #endregion

    #region Backup Creation

    /// <summary>
    /// Creates a full backup of the entire server directory.
    /// </summary>
    Task<Result<BackupDto>> CreateFullBackupAsync(
        Guid serverId,
        string? name = null,
        string? notes = null,
        BackupType type = BackupType.Manual,
        Guid? scheduledTaskId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a selective backup of specified paths only.
    /// </summary>
    Task<Result<BackupDto>> CreateSelectiveBackupAsync(
        Guid serverId,
        IEnumerable<string> relativePaths,
        string? name = null,
        string? notes = null,
        BackupType type = BackupType.Manual,
        Guid? scheduledTaskId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a backup using the provided request parameters.
    /// </summary>
    Task<Result<BackupDto>> CreateBackupAsync(
        CreateBackupRequest request,
        BackupType type = BackupType.Manual,
        Guid? scheduledTaskId = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Backup Operations

    /// <summary>
    /// Restores a backup to its server.
    /// </summary>
    /// <param name="backupId">The backup to restore.</param>
    /// <param name="stopServerFirst">Whether to stop the server before restoring.</param>
    /// <param name="createPreRestoreBackup">Whether to create a backup before restoring.</param>
    Task<Result> RestoreBackupAsync(
        Guid backupId,
        bool stopServerFirst = true,
        bool createPreRestoreBackup = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a backup and its associated file.
    /// </summary>
    Task<Result> DeleteBackupAsync(Guid backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a stream to download a backup file.
    /// </summary>
    Task<Result<Stream>> DownloadBackupAsync(Guid backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the contents of a backup archive.
    /// </summary>
    Task<Result<IReadOnlyList<string>>> GetBackupContentsAsync(Guid backupId, CancellationToken cancellationToken = default);

    #endregion

    #region Retention and Cleanup

    /// <summary>
    /// Applies the retention policy to a server's backups, deleting the oldest beyond the limit.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="keepCount">Number of backups to keep.</param>
    /// <returns>Number of backups deleted.</returns>
    Task<Result<int>> ApplyRetentionPolicyAsync(
        Guid serverId,
        int keepCount,
        CancellationToken cancellationToken = default);

    #endregion

    #region Scheduled Task Integration

    /// <summary>
    /// Executes a backup as part of a scheduled task.
    /// This method is called by ScheduledTaskService.
    /// </summary>
    Task<Result> ExecuteScheduledBackupAsync(
        Guid serverId,
        BackupTaskConfig config,
        Guid scheduledTaskId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Utility

    /// <summary>
    /// Gets the default selective paths for a game.
    /// </summary>
    string[] GetDefaultSelectivePathsForGame(string gameSlug);

    #endregion
}

/// <summary>
/// Configuration for backup scheduled tasks.
/// </summary>
public record BackupTaskConfig(
    string? BackupPath = null,
    bool CompressBackup = true,
    int? RetentionCount = null,
    BackupScope Scope = BackupScope.Full,
    string[]? SelectivePaths = null
);
