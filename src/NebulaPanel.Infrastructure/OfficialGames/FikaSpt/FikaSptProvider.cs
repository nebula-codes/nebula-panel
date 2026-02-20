using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.FikaSpt;

/// <summary>
/// Official game provider for FIKA SPT (Single Player Tarkov with FIKA multiplayer mod).
/// Docker-only deployment using ghcr.io/zhliau/fika-spt-server-docker.
/// Configuration schemas are defined in partial class files under Config/.
/// </summary>
public partial class FikaSptProvider(
    FikaSptVersionFetcher versionFetcher,
    IServerPathResolver pathResolver,
    ILogger<FikaSptProvider> logger) : IOfficialGameProvider
{
    private readonly FikaSptVersionFetcher _versionFetcher = versionFetcher;
    private readonly IServerPathResolver _pathResolver = pathResolver;
    private readonly ILogger<FikaSptProvider> _logger = logger;

    public const string DockerImage = "ghcr.io/zhliau/fika-spt-server-docker";
    public const string DataPath = "/opt/server";
    public const int DefaultServerPort = 6969;

    public string GameSlug => "fika-spt";

    public GameDefinition GetGameDefinition() => new()
    {
        Name = "FIKA SPT",
        Slug = "fika-spt",
        ExecutableType = ExecutableType.Shell,
        DefaultExecutablePath = "SPT.Server.exe",
        DefaultStartCommand = "./SPT.Server.exe",
        DefaultStopCommand = null,
        SupportsDocker = true,
        DefaultDockerImage = DockerImage,
        DockerDataPath = DataPath,
        DefaultPort = DefaultServerPort,
        IconPath = "/images/games/fika-spt.png",
        SupportsMods = true,
        ModProviders =
        [
            new ModProviderConfiguration
            {
                Provider = ModProviderType.SptForge,
                Enabled = true,
                Priority = 1,
                GameSlug = "fika-spt",
                ModInstallPath = "SPT/user/mods/"
            },
            new ModProviderConfiguration
            {
                Provider = ModProviderType.Local,
                Enabled = true,
                Priority = 99,
                GameSlug = "fika-spt",
                ModInstallPath = "SPT/user/mods/"
            }
        ],
        RconDefaults = null, // FIKA SPT does not support RCON
        ConfigurationSchemas = BuildConfigurationSchemas()
    };

    private static Dictionary<string, ConfigurationSchema> BuildConfigurationSchemas()
    {
        var schemas = new Dictionary<string, ConfigurationSchema>();
        AddDockerEnvSchema(schemas);
        AddFikaConfigSchema(schemas);
        return schemas;
    }

    // Partial methods implemented in Config/ partial class files
    static partial void AddDockerEnvSchema(Dictionary<string, ConfigurationSchema> schemas);
    static partial void AddFikaConfigSchema(Dictionary<string, ConfigurationSchema> schemas);

    public string GetDefaultInstallPath(string serverName)
    {
        var sanitized = SanitizeForPath(serverName);
        var basePath = _pathResolver.GetServerBasePath();

        return OperatingSystem.IsWindows()
            ? Path.Combine(basePath, "FikaSptServers", sanitized)
            : Path.Combine(basePath, "fika-spt-servers", sanitized);
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
        _logger.LogDebug("Fetching available FIKA SPT versions");
        return await _versionFetcher.FetchVersionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<ServerInstallationResult> InstallServerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // FIKA SPT is Docker-only. The Docker executor handles pulling the image.
        // Installation just ensures the data directory exists.
        _logger.LogInformation("Preparing FIKA SPT server {Version} at {Path}", version, server.InstallPath);

        progress?.Report(OfficialInstallProgress.Starting("Preparing FIKA SPT server"));

        Directory.CreateDirectory(server.InstallPath);

        // Pre-create directories that the container would otherwise create as root,
        // so NebulaPanel retains write access for mod installation and file management.
        Directory.CreateDirectory(Path.Combine(server.InstallPath, "SPT", "user", "mods"));

        progress?.Report(OfficialInstallProgress.Progress("Setup",
            "Server directory created. Docker will pull the image on first start.", 50));

        progress?.Report(OfficialInstallProgress.Completed());
        return Task.FromResult(ServerInstallationResult.Succeeded(server.InstallPath, version));
    }

    public Task<ServerInstallationResult> UpdateServerAsync(
        GameServer server,
        string targetVersion,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // For Docker-based servers, updating means changing the image tag.
        // The Docker executor handles pulling the new image.
        _logger.LogInformation("Updating FIKA SPT server to {Version} at {Path}",
            targetVersion, server.InstallPath);

        progress?.Report(OfficialInstallProgress.Starting("Updating FIKA SPT server"));
        progress?.Report(OfficialInstallProgress.Progress("Update",
            $"Docker image tag will be updated to {targetVersion} on next start.", 50));
        progress?.Report(OfficialInstallProgress.Completed());

        return Task.FromResult(ServerInstallationResult.Succeeded(server.InstallPath, targetVersion));
    }
}
