namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Tracks version and update information for Hytale servers.
/// </summary>
public class HytaleServerInfo
{
    /// <summary>
    /// Currently installed server version (e.g., "2025.01.31-abc1234").
    /// </summary>
    public string InstalledVersion { get; set; } = string.Empty;

    /// <summary>
    /// The patchline this server is following (e.g., "release", "pre-release").
    /// </summary>
    public string Patchline { get; set; } = "release";

    /// <summary>
    /// When the current version was installed.
    /// </summary>
    public DateTime InstalledAt { get; set; }

    /// <summary>
    /// When the last update check was performed.
    /// </summary>
    public DateTime? LastUpdateCheckAt { get; set; }

    /// <summary>
    /// Latest available version from the last check.
    /// </summary>
    public string? AvailableVersion { get; set; }

    /// <summary>
    /// Whether an update is available.
    /// </summary>
    public bool UpdateAvailable { get; set; }

    /// <summary>
    /// Version that the user dismissed (won't show notification for this version).
    /// </summary>
    public string? DismissedVersion { get; set; }
}
