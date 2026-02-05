using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

/// <summary>
/// Context containing all information needed for Minecraft server installation.
/// </summary>
public record MinecraftInstallContext
{
    /// <summary>
    /// The game server entity being installed.
    /// </summary>
    public required GameServer Server { get; init; }

    /// <summary>
    /// Full version string (e.g., "paper-1.20.4-496").
    /// </summary>
    public required string VersionString { get; init; }

    /// <summary>
    /// Parsed loader type.
    /// </summary>
    public required MinecraftLoader LoaderType { get; init; }

    /// <summary>
    /// Base Minecraft version (e.g., "1.20.4").
    /// </summary>
    public required string MinecraftVersion { get; init; }

    /// <summary>
    /// Loader-specific version (build number, loader version, forge version, etc.).
    /// </summary>
    public string? LoaderVersion { get; init; }

    /// <summary>
    /// Installation directory path.
    /// </summary>
    public string InstallPath => Server.InstallPath;

    /// <summary>
    /// Path to Java executable to use.
    /// </summary>
    public string? JavaPath { get; init; }

    /// <summary>
    /// Minimum required Java version for this Minecraft version.
    /// </summary>
    public int RequiredJavaVersion { get; init; }
}
