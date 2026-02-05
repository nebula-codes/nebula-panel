namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Represents the current state of the update system.
/// </summary>
public enum UpdateStatus
{
    /// <summary>
    /// No update activity in progress.
    /// </summary>
    Idle,

    /// <summary>
    /// Checking for available updates.
    /// </summary>
    Checking,

    /// <summary>
    /// An update is available but not yet downloaded.
    /// </summary>
    UpdateAvailable,

    /// <summary>
    /// Downloading an update.
    /// </summary>
    Downloading,

    /// <summary>
    /// Update downloaded and ready to install.
    /// </summary>
    ReadyToInstall,

    /// <summary>
    /// Creating a backup before applying the update.
    /// </summary>
    CreatingBackup,

    /// <summary>
    /// Stopping game servers before applying the update.
    /// </summary>
    StoppingServers,

    /// <summary>
    /// Installing the update.
    /// </summary>
    Installing,

    /// <summary>
    /// Restarting the application after update.
    /// </summary>
    Restarting,

    /// <summary>
    /// The update operation failed.
    /// </summary>
    Failed
}
