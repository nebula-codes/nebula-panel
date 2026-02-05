using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Tracks metadata about an installed modpack on a game server.
/// </summary>
public class InstalledModpackInfo
{
    /// <summary>
    /// The provider the modpack was installed from.
    /// </summary>
    public ModpackProviderType Provider { get; set; }

    /// <summary>
    /// Provider-specific modpack identifier.
    /// </summary>
    public string ModpackId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the modpack.
    /// </summary>
    public string ModpackName { get; set; } = string.Empty;

    /// <summary>
    /// Provider-specific version identifier.
    /// </summary>
    public string VersionId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable version name (e.g., "1.0.5").
    /// </summary>
    public string? VersionName { get; set; }

    /// <summary>
    /// Minecraft version targeted by the modpack.
    /// </summary>
    public string? MinecraftVersion { get; set; }

    /// <summary>
    /// Mod loader type used by the modpack.
    /// </summary>
    public ModLoaderType LoaderType { get; set; }

    /// <summary>
    /// Specific mod loader version (e.g., "49.0.19" for Forge).
    /// </summary>
    public string? LoaderVersion { get; set; }

    /// <summary>
    /// Number of mods included in the modpack.
    /// </summary>
    public int? ModCount { get; set; }

    /// <summary>
    /// When the modpack was installed.
    /// </summary>
    public DateTime InstalledAt { get; set; }

    // Update cache properties (populated by update check)

    /// <summary>
    /// Latest available version ID from the provider.
    /// </summary>
    public string? LatestVersionId { get; set; }

    /// <summary>
    /// Latest available version name.
    /// </summary>
    public string? LatestVersionName { get; set; }

    /// <summary>
    /// When the latest version was released.
    /// </summary>
    public DateTime? LatestVersionReleasedAt { get; set; }

    /// <summary>
    /// Whether an update is available.
    /// </summary>
    public bool UpdateAvailable { get; set; }

    /// <summary>
    /// When the last update check was performed.
    /// </summary>
    public DateTime? LastUpdateCheckAt { get; set; }

    /// <summary>
    /// Version ID that the user dismissed (won't show notification for this version).
    /// </summary>
    public string? DismissedVersionId { get; set; }
}
