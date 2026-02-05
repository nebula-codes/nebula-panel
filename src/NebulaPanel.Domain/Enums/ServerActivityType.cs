namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Types of server-related activities that can be tracked.
/// </summary>
public enum ServerActivityType
{
    /// <summary>Server was started.</summary>
    Started,

    /// <summary>Server was stopped gracefully.</summary>
    Stopped,

    /// <summary>Server crashed unexpectedly.</summary>
    Crashed,

    /// <summary>Server was restarted.</summary>
    Restarted,

    /// <summary>Server was forcefully killed.</summary>
    Killed,

    /// <summary>Server installation started.</summary>
    InstallStarted,

    /// <summary>Server installation completed successfully.</summary>
    InstallCompleted,

    /// <summary>Server installation failed.</summary>
    InstallFailed,

    /// <summary>Server update started.</summary>
    UpdateStarted,

    /// <summary>Server update completed successfully.</summary>
    UpdateCompleted,

    /// <summary>Server update failed.</summary>
    UpdateFailed,

    /// <summary>Backup was created.</summary>
    BackupCreated,

    /// <summary>Backup was restored.</summary>
    BackupRestored,

    /// <summary>Configuration was changed.</summary>
    ConfigChanged,

    /// <summary>A mod was installed.</summary>
    ModInstalled,

    /// <summary>A mod was removed.</summary>
    ModRemoved,

    /// <summary>A command was executed on the server.</summary>
    CommandExecuted
}
