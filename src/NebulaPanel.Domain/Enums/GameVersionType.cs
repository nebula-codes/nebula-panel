namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Indicates the release type of a game version.
/// </summary>
public enum GameVersionType
{
    /// <summary>
    /// Stable release version.
    /// </summary>
    Release,

    /// <summary>
    /// Development snapshot (Minecraft terminology).
    /// </summary>
    Snapshot,

    /// <summary>
    /// Beta testing version.
    /// </summary>
    Beta,

    /// <summary>
    /// Alpha/early development version.
    /// </summary>
    Alpha
}
