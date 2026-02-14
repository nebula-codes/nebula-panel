using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.ArkSurvivalAscended;

/// <summary>
/// Official game provider for Ark: Survival Ascended.
/// Configuration schemas are defined in partial class files under Config/.
/// </summary>
public partial class ArkSurvivalAscendedProvider(
    ArkVersionFetcher versionFetcher,
    ArkServerInstaller serverInstaller,
    IServerPathResolver pathResolver,
    ILogger<ArkSurvivalAscendedProvider> logger) : IOfficialGameProvider
{
    private readonly ArkVersionFetcher _versionFetcher = versionFetcher;
    private readonly ArkServerInstaller _serverInstaller = serverInstaller;
    private readonly IServerPathResolver _pathResolver = pathResolver;
    private readonly ILogger<ArkSurvivalAscendedProvider> _logger = logger;

    /// <summary>
    /// ASA Dedicated Server Steam App ID.
    /// </summary>
    public const string AsaServerAppId = "2430930";

    public string GameSlug => "ark-survival-ascended";

    public GameDefinition GetGameDefinition() => new()
    {
        Name = "Ark: Survival Ascended",
        Slug = "ark-survival-ascended",
        SteamAppId = AsaServerAppId,
        ExecutableType = ExecutableType.Exe,
        DefaultExecutablePath = "ShooterGame/Binaries/Win64/ArkAscendedServer.exe",
        DefaultStartCommand = "ArkAscendedServer.exe TheIsland_WP?listen?Port={port}?QueryPort=27015?RCONPort=27020 -server -log -NoBattlEye",
        DefaultStopCommand = "saveworld",
        SupportsDocker = true,
        DefaultDockerImage = "acekorneya/asa_server",
        DockerDataPath = "/usr/games/.wine/drive_c/POK/Steam/steamapps/common/ARK Survival Ascended Dedicated Server",
        DefaultPort = 7777,
        IconPath = "/images/games/ark-survival-ascended.svg",
        SupportsMods = true,
        ModProviders =
        [
            new ModProviderConfiguration
            {
                Provider = ModProviderType.CurseForge,
                Enabled = true,
                Priority = 1,
                GameSlug = "ark-survival-ascended",
                ModInstallPath = "ShooterGame/Content/Mods/"
            },
            new ModProviderConfiguration
            {
                Provider = ModProviderType.Local,
                Enabled = true,
                Priority = 99,
                GameSlug = "ark-survival-ascended",
                ModInstallPath = "ShooterGame/Content/Mods/"
            }
        ],
        RconDefaults = new RconDefaults
        {
            DefaultEnabled = true,
            Protocol = RconProtocolType.Source,
            DefaultPort = 27020
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

        AddGameUserSettingsSchema(schemas);
        AddGameIniSchema(schemas);

        return schemas;
    }

    // Partial methods implemented in Config/ files
    static partial void AddGameUserSettingsSchema(Dictionary<string, ConfigurationSchema> schemas);
    static partial void AddGameIniSchema(Dictionary<string, ConfigurationSchema> schemas);

    public string GetDefaultInstallPath(string serverName)
    {
        var sanitized = SanitizeForPath(serverName);
        var basePath = _pathResolver.GetServerBasePath();

        return OperatingSystem.IsWindows()
            ? Path.Combine(basePath, "ArkServers", sanitized)
            : Path.Combine(basePath, "ark-servers", sanitized);
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
        _logger.LogDebug("Fetching available ASA versions");
        return await _versionFetcher.FetchVersionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ServerInstallationResult> InstallServerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Installing ASA server {Version} at {Path}",
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
        _logger.LogInformation("Updating ASA server to {Version} at {Path}",
            targetVersion, server.InstallPath);

        return await _serverInstaller.UpdateAsync(server, targetVersion, progress, cancellationToken)
            .ConfigureAwait(false);
    }
}
