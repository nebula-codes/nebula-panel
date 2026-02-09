using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Terraria;

/// <summary>
/// Official game provider for Terraria with tModLoader support.
/// Configuration schemas are defined in partial class files under Config/.
/// </summary>
public partial class TerrariaProvider(
    TerrariaVersionFetcher versionFetcher,
    TerrariaServerInstaller serverInstaller,
    IServerPathResolver pathResolver,
    ILogger<TerrariaProvider> logger) : IOfficialGameProvider
{
    private readonly TerrariaVersionFetcher _versionFetcher = versionFetcher;
    private readonly TerrariaServerInstaller _serverInstaller = serverInstaller;
    private readonly IServerPathResolver _pathResolver = pathResolver;
    private readonly ILogger<TerrariaProvider> _logger = logger;

    /// <summary>
    /// tModLoader Steam App ID.
    /// </summary>
    public const string TModLoaderAppId = "1281930";

    /// <summary>
    /// Base Terraria Steam App ID.
    /// </summary>
    public const string TerrariaAppId = "105600";

    public string GameSlug => "terraria-tmodloader";

    public GameDefinition GetGameDefinition() => new()
    {
        Name = "Terraria (tModLoader)",
        Slug = "terraria-tmodloader",
        SteamAppId = TModLoaderAppId,
        ExecutableType = ExecutableType.Shell,
        DefaultExecutablePath = "start-tModLoaderServer.sh",
        DefaultStartCommand = "./start-tModLoaderServer.sh -config serverconfig.txt -port {port}",
        DefaultStopCommand = "exit",
        SupportsDocker = true,
        DefaultDockerImage = "jacobsmile/tmodloader1.4",
        DockerDataPath = "/data",
        DefaultPort = 7777,
        IconPath = "/images/games/terraria.svg",
        SupportsMods = true,
        ModProviders =
        [
            new ModProviderConfiguration
            {
                Provider = ModProviderType.SteamWorkshop,
                Enabled = true,
                Priority = 1,
                GameSlug = TModLoaderAppId, // Steam Workshop uses App ID
                ModInstallPath = "Mods/"
            },
            new ModProviderConfiguration
            {
                Provider = ModProviderType.CurseForge,
                Enabled = true,
                Priority = 2,
                GameSlug = "terraria",
                ModInstallPath = "Mods/"
            },
            new ModProviderConfiguration
            {
                Provider = ModProviderType.Local,
                Enabled = true,
                Priority = 99,
                GameSlug = "terraria",
                ModInstallPath = "Mods/"
            }
        ],
        RconDefaults = new RconDefaults
        {
            DefaultEnabled = false, // TShock required for RCON
            Protocol = RconProtocolType.Telnet,
            DefaultPort = 7023
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

        // Add serverconfig.txt schema (defined in TerrariaProvider.ServerConfig.cs)
        AddServerConfigSchema(schemas);

        return schemas;
    }

    // Partial methods implemented in Config/ files
    static partial void AddServerConfigSchema(Dictionary<string, ConfigurationSchema> schemas);

    public string GetDefaultInstallPath(string serverName)
    {
        var sanitized = SanitizeForPath(serverName);
        var basePath = _pathResolver.GetServerBasePath();

        return OperatingSystem.IsWindows()
            ? Path.Combine(basePath, "TerrariaServers", sanitized)
            : Path.Combine(basePath, "terraria-servers", sanitized);
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
        _logger.LogDebug("Fetching available tModLoader versions");
        return await _versionFetcher.FetchVersionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ServerInstallationResult> InstallServerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Installing tModLoader server {Version} at {Path}",
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
        _logger.LogInformation("Updating tModLoader server to {Version} at {Path}",
            targetVersion, server.InstallPath);

        return await _serverInstaller.UpdateAsync(server, targetVersion, progress, cancellationToken)
            .ConfigureAwait(false);
    }
}
