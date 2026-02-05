using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Information about an available game version from an official game provider.
/// </summary>
public record GameVersionInfo
{
    /// <summary>
    /// Version identifier (e.g., "1.20.4", "paper-1.20.4-496").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Human-readable display name (e.g., "1.20.4", "Paper 1.20.4 Build 496").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Release date of this version, if known.
    /// </summary>
    public DateTime? ReleaseDate { get; init; }

    /// <summary>
    /// Type of release (Release, Snapshot, Beta, Alpha).
    /// </summary>
    public GameVersionType VersionType { get; init; } = GameVersionType.Release;

    /// <summary>
    /// For Minecraft: the loader/server software type.
    /// </summary>
    public MinecraftLoader? LoaderType { get; init; }

    /// <summary>
    /// For Minecraft loaders: the base Minecraft version they support.
    /// </summary>
    public string? MinecraftVersion { get; init; }

    /// <summary>
    /// Whether this version is recommended for new installations.
    /// </summary>
    public bool IsRecommended { get; init; }
}
