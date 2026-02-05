using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Official game provider for Minecraft: Java Edition.
/// Configuration schemas are defined in partial class files under Config/.
/// </summary>
public partial class MinecraftProvider(
    MinecraftVersionAggregator versionAggregator,
    MinecraftServerInstaller serverInstaller,
    ILogger<MinecraftProvider> logger) : IOfficialGameProvider
{
    private readonly MinecraftVersionAggregator _versionAggregator = versionAggregator;
    private readonly MinecraftServerInstaller _serverInstaller = serverInstaller;
    private readonly ILogger<MinecraftProvider> _logger = logger;

    public string GameSlug => "minecraft-java";

    public GameDefinition GetGameDefinition() => new()
    {
        Name = "Minecraft: Java Edition",
        Slug = "minecraft-java",
        ExecutableType = ExecutableType.Jar,
        DefaultExecutablePath = "server.jar",
        DefaultStartCommand = "-Xmx{maxMemory}M -Xms{minMemory}M -jar server.jar nogui",
        DefaultStopCommand = "stop",
        SupportsDocker = true,
        DefaultDockerImage = "itzg/minecraft-server",
        IconPath = "/images/games/minecraft.svg",
        SupportsMods = true,
        ModProviders =
        [
            new ModProviderConfiguration
            {
                Provider = ModProviderType.Modrinth,
                Enabled = true,
                Priority = 1,
                GameSlug = "minecraft",
                ModInstallPath = "mods/"
            },
            new ModProviderConfiguration
            {
                Provider = ModProviderType.CurseForge,
                Enabled = true,
                Priority = 2,
                GameSlug = "minecraft",
                ModInstallPath = "mods/"
            },
            new ModProviderConfiguration
            {
                Provider = ModProviderType.Hangar,
                Enabled = true,
                Priority = 3,
                GameSlug = "paper",
                ModInstallPath = "plugins/"
            },
            new ModProviderConfiguration
            {
                Provider = ModProviderType.Modrinth,
                Enabled = true,
                Priority = 4,
                GameSlug = "minecraft",
                ModInstallPath = "plugins/",
                ProviderSettings = new Dictionary<string, string> { ["categories"] = "bukkit" }
            },
            new ModProviderConfiguration
            {
                Provider = ModProviderType.Local,
                Enabled = true,
                Priority = 99,
                GameSlug = "minecraft",
                ModInstallPath = "mods/"
            }
        ],
        RconDefaults = new RconDefaults
        {
            DefaultEnabled = true,
            Protocol = RconProtocolType.Minecraft,
            DefaultPort = 25575
        },
        ConfigurationSchemas = BuildConfigurationSchemas()
    };

    /// <summary>
    /// Builds the complete configuration schemas dictionary.
    /// Individual schemas are defined in partial class files under Config/.
    /// </summary>
    private static Dictionary<string, ConfigurationSchema> BuildConfigurationSchemas()
    {
        var schemas = new Dictionary<string, ConfigurationSchema>();

        // Add server.properties schema (defined in MinecraftProvider.ServerProperties.cs)
        AddServerPropertiesSchema(schemas);

        // Add player management schemas (defined in MinecraftProvider.PlayerManagement.cs)
        AddPlayerManagementSchemas(schemas);

        // Add Bukkit config schema (defined in MinecraftProvider.BukkitConfig.cs)
        AddBukkitConfigSchema(schemas);

        // Add Spigot config schema (defined in MinecraftProvider.SpigotConfig.cs)
        AddSpigotConfigSchema(schemas);

        // Add Paper config schema (defined in MinecraftProvider.PaperConfig.cs)
        AddPaperConfigSchema(schemas);

        // Add Fabric config schema (defined in MinecraftProvider.FabricConfig.cs)
        AddFabricConfigSchema(schemas);

        return schemas;
    }

    // Partial methods implemented in Config/ files
    static partial void AddServerPropertiesSchema(Dictionary<string, ConfigurationSchema> schemas);
    static partial void AddPlayerManagementSchemas(Dictionary<string, ConfigurationSchema> schemas);
    static partial void AddBukkitConfigSchema(Dictionary<string, ConfigurationSchema> schemas);
    static partial void AddSpigotConfigSchema(Dictionary<string, ConfigurationSchema> schemas);
    static partial void AddPaperConfigSchema(Dictionary<string, ConfigurationSchema> schemas);
    static partial void AddFabricConfigSchema(Dictionary<string, ConfigurationSchema> schemas);

    public string GetDefaultInstallPath(string serverName)
    {
        var sanitized = SanitizeForPath(serverName);
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return OperatingSystem.IsWindows()
            ? Path.Combine(basePath, "MinecraftServers", sanitized)
            : Path.Combine(basePath, "minecraft-servers", sanitized);
    }

    private static string SanitizeForPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "server";

        var sanitized = name.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');

        sanitized = new string(sanitized.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        while (sanitized.Contains("--"))
            sanitized = sanitized.Replace("--", "-");

        sanitized = sanitized.Trim('-');
        return string.IsNullOrEmpty(sanitized) ? "server" : sanitized;
    }

    public async Task<IReadOnlyList<GameVersionInfo>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching available Minecraft versions");
        return await _versionAggregator.GetAllVersionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ServerInstallationResult> InstallServerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Installing Minecraft server {Version} at {Path}",
            version, server.InstallPath);

        return await _serverInstaller.InstallAsync(server, version, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ServerInstallationResult> UpdateServerAsync(
        GameServer server,
        string targetVersion,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating Minecraft server to {Version} at {Path}",
            targetVersion, server.InstallPath);

        return await _serverInstaller.UpdateAsync(server, targetVersion, progress, cancellationToken)
            .ConfigureAwait(false);
    }
}
