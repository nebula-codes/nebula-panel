using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for providing preset configurations for popular Steam games.
/// </summary>
public interface ISteamGamePresetService
{
    /// <summary>
    /// Gets all available game presets.
    /// </summary>
    IReadOnlyList<SteamGamePreset> GetAllPresets();

    /// <summary>
    /// Gets a preset by Steam App ID.
    /// </summary>
    /// <param name="appId">The dedicated server App ID.</param>
    /// <returns>The preset if found, null otherwise.</returns>
    SteamGamePreset? GetPresetByAppId(string appId);

    /// <summary>
    /// Gets a preset by game slug.
    /// </summary>
    /// <param name="slug">The game slug.</param>
    /// <returns>The preset if found, null otherwise.</returns>
    SteamGamePreset? GetPresetBySlug(string slug);

    /// <summary>
    /// Searches presets by name.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <returns>Matching presets.</returns>
    IReadOnlyList<SteamGamePreset> SearchPresets(string query);
}

/// <summary>
/// A preset configuration for a popular Steam game server.
/// </summary>
public record SteamGamePreset
{
    /// <summary>
    /// Display name of the game.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// URL-friendly slug for the game.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Steam App ID for the dedicated server.
    /// </summary>
    public required string ServerAppId { get; init; }

    /// <summary>
    /// Steam App ID for the client game (for Workshop support).
    /// </summary>
    public string? ClientAppId { get; init; }

    /// <summary>
    /// Executable type.
    /// </summary>
    public ExecutableType ExecutableType { get; init; } = ExecutableType.Exe;

    /// <summary>
    /// Default path to the server executable (relative to install directory).
    /// </summary>
    public required string DefaultExecutablePath { get; init; }

    /// <summary>
    /// Default start command/arguments.
    /// </summary>
    public required string DefaultStartCommand { get; init; }

    /// <summary>
    /// Default stop command (sent via RCON/console).
    /// </summary>
    public string? DefaultStopCommand { get; init; }

    /// <summary>
    /// Default game port.
    /// </summary>
    public int DefaultPort { get; init; }

    /// <summary>
    /// RCON configuration.
    /// </summary>
    public SteamGameRconPreset? RconPreset { get; init; }

    /// <summary>
    /// Whether Docker is supported.
    /// </summary>
    public bool SupportsDocker { get; init; }

    /// <summary>
    /// Default Docker image if Docker is supported.
    /// </summary>
    public string? DefaultDockerImage { get; init; }

    /// <summary>
    /// Path inside the Docker container where server data is stored.
    /// Used to automatically mount InstallPath to this location.
    /// </summary>
    public string? DockerDataPath { get; init; }

    /// <summary>
    /// Whether Workshop mods are supported.
    /// </summary>
    public bool SupportsWorkshop { get; init; }

    /// <summary>
    /// Default mod installation path.
    /// </summary>
    public string? ModInstallPath { get; init; }

    /// <summary>
    /// Icon URL for the game.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Brief description of the game.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Category/genre of the game.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Additional notes about server setup.
    /// </summary>
    public string? SetupNotes { get; init; }
}

/// <summary>
/// RCON preset configuration for a game.
/// </summary>
public record SteamGameRconPreset
{
    /// <summary>
    /// Whether RCON is supported.
    /// </summary>
    public bool Supported { get; init; }

    /// <summary>
    /// RCON protocol type.
    /// </summary>
    public RconProtocolType Protocol { get; init; }

    /// <summary>
    /// Default RCON port.
    /// </summary>
    public int DefaultPort { get; init; }

    /// <summary>
    /// WebRcon path if applicable.
    /// </summary>
    public string? WebRconPath { get; init; }
}
