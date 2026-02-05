using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Interface for managing backup file operations.
/// </summary>
public interface IBackupFileManager
{
    /// <summary>
    /// Creates a backup archive of the server.
    /// </summary>
    /// <param name="server">The server to back up.</param>
    /// <param name="backupDirectory">The directory to store the backup.</param>
    /// <param name="backupName">The name of the backup file (without extension).</param>
    /// <param name="selectivePaths">Optional relative paths to include. If null, backs up entire server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full path to the created backup file.</returns>
    Task<string> CreateBackupArchiveAsync(
        GameServer server,
        string backupDirectory,
        string backupName,
        IEnumerable<string>? selectivePaths = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts a backup archive to the server's install path.
    /// </summary>
    /// <param name="server">The server to restore to.</param>
    /// <param name="backupFilePath">The path to the backup file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExtractBackupArchiveAsync(
        GameServer server,
        string backupFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a backup file from the file system.
    /// </summary>
    /// <param name="backupFilePath">The path to the backup file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteBackupFileAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a stream to read the backup file.
    /// </summary>
    /// <param name="backupFilePath">The path to the backup file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream to read the backup file.</returns>
    Task<Stream> OpenBackupStreamAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the size of a backup file in bytes.
    /// </summary>
    /// <param name="backupFilePath">The path to the backup file.</param>
    /// <returns>The file size in bytes.</returns>
    long GetBackupFileSize(string backupFilePath);

    /// <summary>
    /// Checks if a backup file exists.
    /// </summary>
    /// <param name="backupFilePath">The path to the backup file.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    bool BackupFileExists(string backupFilePath);

    /// <summary>
    /// Ensures the backup directory exists.
    /// </summary>
    /// <param name="backupDirectory">The backup directory path.</param>
    void EnsureBackupDirectoryExists(string backupDirectory);

    /// <summary>
    /// Gets the contents of a backup archive (list of files/folders).
    /// </summary>
    /// <param name="backupFilePath">The path to the backup file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of entry names in the archive.</returns>
    Task<IReadOnlyList<string>> GetBackupContentsAsync(string backupFilePath, CancellationToken cancellationToken = default);
}
