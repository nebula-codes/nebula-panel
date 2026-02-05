namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Types of notifications that can be sent to users.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// General informational notification.
    /// </summary>
    Info,

    /// <summary>
    /// Success notification for completed operations.
    /// </summary>
    Success,

    /// <summary>
    /// Warning notification for potential issues.
    /// </summary>
    Warning,

    /// <summary>
    /// Error notification for failed operations.
    /// </summary>
    Error,

    /// <summary>
    /// Server status change notification (started, stopped, etc.).
    /// </summary>
    ServerStatus,

    /// <summary>
    /// Backup completed successfully.
    /// </summary>
    BackupCompleted,

    /// <summary>
    /// Backup failed.
    /// </summary>
    BackupFailed,

    /// <summary>
    /// An update is available for a server or the panel.
    /// </summary>
    UpdateAvailable,

    /// <summary>
    /// Server or mod installation completed successfully.
    /// </summary>
    InstallCompleted,

    /// <summary>
    /// Server or mod installation failed.
    /// </summary>
    InstallFailed
}
