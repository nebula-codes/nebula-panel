using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Complete definition for an official game. Used by providers to specify
/// all game properties. Applied to Game entity during database seeding.
/// </summary>
public record GameDefinition
{
    /// <summary>
    /// Display name of the game (e.g., "Minecraft: Java Edition").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// URL-friendly slug (e.g., "minecraft").
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Steam App ID for Steam-based games.
    /// </summary>
    public string? SteamAppId { get; init; }

    /// <summary>
    /// Type of executable (Jar, Exe, Shell, SteamCmd).
    /// </summary>
    public required ExecutableType ExecutableType { get; init; }

    /// <summary>
    /// Relative path to the executable.
    /// </summary>
    public required string DefaultExecutablePath { get; init; }

    /// <summary>
    /// Command template for starting the server.
    /// </summary>
    public required string DefaultStartCommand { get; init; }

    /// <summary>
    /// Optional graceful stop command.
    /// </summary>
    public string? DefaultStopCommand { get; init; }

    /// <summary>
    /// Whether this game supports Docker deployment.
    /// </summary>
    public bool SupportsDocker { get; init; }

    /// <summary>
    /// Default Docker image for this game.
    /// </summary>
    public string? DefaultDockerImage { get; init; }

    /// <summary>
    /// Path inside container for data volume mount (Docker).
    /// </summary>
    public string? DockerDataPath { get; init; }

    /// <summary>
    /// Default game server port for this game.
    /// </summary>
    public int? DefaultPort { get; init; }

    /// <summary>
    /// Path to the game icon.
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// Whether this game supports mods.
    /// </summary>
    public bool SupportsMods { get; init; }

    /// <summary>
    /// Configured mod providers for this game.
    /// </summary>
    public List<ModProviderConfiguration> ModProviders { get; init; } = [];

    /// <summary>
    /// Default RCON settings for this game.
    /// </summary>
    public RconDefaults? RconDefaults { get; init; }

    /// <summary>
    /// Configuration file schemas for dynamic form generation.
    /// </summary>
    public Dictionary<string, ConfigurationSchema> ConfigurationSchemas { get; init; } = [];
}
