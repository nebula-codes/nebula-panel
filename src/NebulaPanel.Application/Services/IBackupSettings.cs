namespace NebulaPanel.Application.Services;

/// <summary>
/// Interface for backup configuration settings.
/// </summary>
public interface IBackupSettings
{
    /// <summary>
    /// Gets the resolved backup directory path.
    /// </summary>
    string GetResolvedBackupDirectory();

    /// <summary>
    /// Gets the default number of backups to retain per server.
    /// </summary>
    int DefaultRetentionCount { get; }

    /// <summary>
    /// Gets whether to create a pre-restore backup before restoring.
    /// </summary>
    bool CreatePreRestoreBackup { get; }

    /// <summary>
    /// Gets the default selective paths for a game.
    /// </summary>
    string[] GetSelectivePathsForGame(string gameSlug);
}
