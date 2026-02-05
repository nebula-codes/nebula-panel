namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Represents the current phase during an update operation.
/// </summary>
public enum UpdateProgressPhase
{
    /// <summary>
    /// Preparing for the update operation.
    /// </summary>
    Preparing,

    /// <summary>
    /// Downloading the update package.
    /// </summary>
    Downloading,

    /// <summary>
    /// Verifying the downloaded package integrity.
    /// </summary>
    Verifying,

    /// <summary>
    /// Creating a backup before applying the update.
    /// </summary>
    CreatingBackup,

    /// <summary>
    /// Stopping game servers before applying the update.
    /// </summary>
    StoppingServers,

    /// <summary>
    /// Extracting update files.
    /// </summary>
    ExtractingFiles,

    /// <summary>
    /// Running database migrations.
    /// </summary>
    UpdatingDatabase,

    /// <summary>
    /// Starting services after update.
    /// </summary>
    StartingServices,

    /// <summary>
    /// Completing the update process.
    /// </summary>
    Completing
}
