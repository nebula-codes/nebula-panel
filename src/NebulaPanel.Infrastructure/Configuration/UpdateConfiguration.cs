using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for the update system.
/// </summary>
public class UpdateConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Update";

    /// <summary>
    /// Whether the update system is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to automatically check for updates on a schedule.
    /// </summary>
    public bool AutoCheck { get; set; } = true;

    /// <summary>
    /// The interval in hours between automatic update checks.
    /// </summary>
    public int CheckIntervalHours { get; set; } = 6;

    /// <summary>
    /// The update channel to use (Stable, Preview, or Development).
    /// </summary>
    public UpdateChannel Channel { get; set; } = UpdateChannel.Stable;

    /// <summary>
    /// The URL for checking updates (GitHub releases API).
    /// </summary>
    public string UpdateServerUrl { get; set; } = "https://api.github.com/repos/nebula-codes/nebula-panel/releases";

    /// <summary>
    /// Whether to automatically create a backup before applying updates.
    /// </summary>
    public bool AutoBackup { get; set; } = true;

    /// <summary>
    /// The number of update backups to keep.
    /// </summary>
    public int KeepBackupCount { get; set; } = 5;

    /// <summary>
    /// Whether to stop game servers before applying updates.
    /// </summary>
    public bool StopServersBeforeUpdate { get; set; }

    /// <summary>
    /// Additional paths to include in pre-update backups.
    /// </summary>
    public List<string> AdditionalBackupPaths { get; set; } = [];

    /// <summary>
    /// Gets the resolved update directory path.
    /// </summary>
    public string GetUpdateDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "data", "updates");
    }

    /// <summary>
    /// Gets the resolved backup directory path for updates.
    /// </summary>
    public string GetBackupDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "data", "backups", "updates");
    }
}
