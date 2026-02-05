using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Repositories;

/// <summary>
/// Repository interface for managing backup entities.
/// </summary>
public interface IBackupRepository
{
    /// <summary>
    /// Gets all backups with server information.
    /// </summary>
    Task<IReadOnlyList<Backup>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all backups for a specific server.
    /// </summary>
    Task<IReadOnlyList<Backup>> GetByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent backups for a specific server.
    /// </summary>
    Task<IReadOnlyList<Backup>> GetRecentByServerIdAsync(Guid serverId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a backup by ID.
    /// </summary>
    Task<Backup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a backup by ID with server information included.
    /// </summary>
    Task<Backup?> GetByIdWithServerAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of backups for a server.
    /// </summary>
    Task<int> GetCountByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of backups by status for a server.
    /// </summary>
    Task<int> GetCountByServerIdAndStatusAsync(Guid serverId, BackupStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of backups by type for a server.
    /// </summary>
    Task<int> GetCountByServerIdAndTypeAsync(Guid serverId, BackupType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total size of all backups for a server.
    /// </summary>
    Task<long> GetTotalSizeByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent completed backup for a server.
    /// </summary>
    Task<Backup?> GetLatestCompletedByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new backup.
    /// </summary>
    Task<Backup> AddAsync(Backup backup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing backup.
    /// </summary>
    Task UpdateAsync(Backup backup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a backup by ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets backups that should be deleted based on retention policy.
    /// Returns oldest backups beyond the keepCount.
    /// </summary>
    Task<IReadOnlyList<Backup>> GetBackupsToDeleteForRetentionAsync(
        Guid serverId,
        int keepCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the oldest backups for a server, keeping only the specified count.
    /// </summary>
    Task<int> DeleteOldestByServerIdAsync(Guid serverId, int keepCount, CancellationToken cancellationToken = default);
}
