using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Complete JSON-based game definition for simple games that don't require custom code.
/// This is the schema for game.json definition files.
/// </summary>
public record JsonGameDefinition
{
    /// <summary>
    /// Schema version for this definition format.
    /// </summary>
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>
    /// URL-friendly slug (e.g., "valheim", "satisfactory").
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Display name of the game.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Steam App ID for Steam-based games.
    /// </summary>
    public string? SteamAppId { get; init; }

    /// <summary>
    /// Type of executable (Exe, Jar, Shell, SteamCmd).
    /// </summary>
    public required ExecutableType ExecutableType { get; init; }

    /// <summary>
    /// Relative path to the server executable.
    /// </summary>
    public required string DefaultExecutablePath { get; init; }

    /// <summary>
    /// Command template for starting the server.
    /// Supports placeholders: {port}, {maxPlayers}, {serverName}, etc.
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
    /// Path to the game icon (relative to wwwroot).
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
    /// Default RCON settings.
    /// </summary>
    public RconDefaults? RconDefaults { get; init; }

    /// <summary>
    /// Mapping of config file names to their schema file names.
    /// E.g., { "server.cfg": "server.cfg.schema.json" }
    /// </summary>
    public Dictionary<string, string> ConfigSchemaFiles { get; init; } = [];

    /// <summary>
    /// Version source configurations for fetching available versions.
    /// </summary>
    public List<VersionSourceConfig> VersionSources { get; init; } = [];

    /// <summary>
    /// Installation configuration.
    /// </summary>
    public JsonInstallConfig? InstallConfig { get; init; }

    /// <summary>
    /// Converts this JSON definition to a GameDefinition value object.
    /// </summary>
    public GameDefinition ToGameDefinition(Dictionary<string, ConfigurationSchema>? schemas = null)
    {
        return new GameDefinition
        {
            Name = Name,
            Slug = Slug,
            SteamAppId = SteamAppId,
            ExecutableType = ExecutableType,
            DefaultExecutablePath = DefaultExecutablePath,
            DefaultStartCommand = DefaultStartCommand,
            DefaultStopCommand = DefaultStopCommand,
            SupportsDocker = SupportsDocker,
            DefaultDockerImage = DefaultDockerImage,
            IconPath = IconPath,
            SupportsMods = SupportsMods,
            ModProviders = ModProviders,
            RconDefaults = RconDefaults,
            ConfigurationSchemas = schemas ?? []
        };
    }
}

/// <summary>
/// Configuration for a version source.
/// </summary>
public record VersionSourceConfig
{
    /// <summary>
    /// Type of version source: "SteamCmd", "HttpJson", "Static".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// URL to fetch version information from.
    /// For SteamCmd: Steam API URL
    /// For HttpJson: JSON endpoint URL
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// JSONPath expression to extract version array from response.
    /// </summary>
    public string? JsonPath { get; init; }

    /// <summary>
    /// Field name containing the version string in each version object.
    /// </summary>
    public string? VersionField { get; init; }

    /// <summary>
    /// Field name containing the release date in each version object.
    /// </summary>
    public string? DateField { get; init; }

    /// <summary>
    /// For Static type: list of version strings.
    /// </summary>
    public List<string>? StaticVersions { get; init; }
}

/// <summary>
/// Installation configuration for JSON-defined games.
/// </summary>
public record JsonInstallConfig
{
    /// <summary>
    /// Installation type: "SteamCmd", "HttpDownload", "Manual".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Steam App ID for SteamCmd installation.
    /// </summary>
    public string? SteamAppId { get; init; }

    /// <summary>
    /// URL template for HTTP download.
    /// Supports placeholders: {version}, {platform}, etc.
    /// </summary>
    public string? DownloadUrlTemplate { get; init; }

    /// <summary>
    /// Commands to run after installation.
    /// </summary>
    public List<string>? PostInstallCommands { get; init; }

    /// <summary>
    /// Files to extract from the download (for archives).
    /// </summary>
    public List<string>? ExtractFiles { get; init; }

    /// <summary>
    /// Whether to validate installation via SteamCmd.
    /// </summary>
    public bool ValidateAfterInstall { get; init; } = true;
}
