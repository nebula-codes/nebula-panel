using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Entities;

public class Game
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;            // "Minecraft", "Rust", "Valheim"
    public string Slug { get; set; } = string.Empty;            // "minecraft", "rust", "valheim"
    public GameSourceType SourceType { get; set; } = GameSourceType.Custom;  // Official or Custom
    public string? SteamAppId { get; set; }                     // For Steam-based games
    public ExecutableType ExecutableType { get; set; }          // Jar, Exe, Shell
    public string DefaultExecutablePath { get; set; } = string.Empty;  // Relative path to executable
    public string DefaultStartCommand { get; set; } = string.Empty;    // Start command template
    public string? DefaultStopCommand { get; set; }             // Graceful stop command (RCON, etc.)
    public bool SupportsDocker { get; set; }
    public string? DefaultDockerImage { get; set; }
    public string? DockerDataPath { get; set; }  // Path inside container for data volume mount
    public int? DefaultPort { get; set; }  // Default game server port
    public string? IconPath { get; set; }

    // Mod support - multiple providers per game
    public bool SupportsMods { get; set; }
    public List<ModProviderConfiguration> ModProviders { get; set; } = [];

    // RCON defaults for this game
    public RconDefaults? RconDefaults { get; set; }

    // Configuration file schemas (multiple files supported) - stored as JSON
    public Dictionary<string, ConfigurationSchema> ConfigurationSchemas { get; set; } = [];

    // Official game management properties
    /// <summary>
    /// Whether this game is enabled and available for server creation.
    /// Official games can be disabled but not deleted.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// When available versions were last fetched from upstream sources.
    /// </summary>
    public DateTime? LastVersionCheck { get; set; }

    /// <summary>
    /// Number of cached versions available for this game.
    /// </summary>
    public int? CachedVersionCount { get; set; }

    /// <summary>
    /// Version string of the configuration schema definition.
    /// Used to track schema updates.
    /// </summary>
    public string? SchemaVersion { get; set; }

    // Navigation property
    public ICollection<GameServer> Servers { get; set; } = [];
}
