namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Types of announcements that can be displayed on the dashboard.
/// </summary>
public enum AnnouncementType
{
    /// <summary>General information announcement.</summary>
    Info,

    /// <summary>Success or positive announcement.</summary>
    Success,

    /// <summary>Warning announcement requiring attention.</summary>
    Warning,

    /// <summary>Error or critical announcement.</summary>
    Error,

    /// <summary>Scheduled maintenance announcement.</summary>
    Maintenance
}
