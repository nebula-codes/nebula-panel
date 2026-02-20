using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.FikaHeadless;

/// <summary>
/// Official game provider for Fika Headless Client.
/// Docker-only deployment using ghcr.io/zhliau/fika-headless-docker.
/// Configuration schemas are defined in partial class files under Config/.
/// </summary>
public partial class FikaHeadlessProvider(
    FikaHeadlessVersionFetcher versionFetcher,
    IServerPathResolver pathResolver,
    ILogger<FikaHeadlessProvider> logger) : IOfficialGameProvider
{
    private readonly FikaHeadlessVersionFetcher _versionFetcher = versionFetcher;
    private readonly IServerPathResolver _pathResolver = pathResolver;
    private readonly ILogger<FikaHeadlessProvider> _logger = logger;

    public const string DockerImage = "ghcr.io/zhliau/fika-headless-docker";
    public const string DataPath = "/opt/tarkov";
    public const int DefaultServerPort = 6969;

    public string GameSlug => "fika-headless";

    public GameDefinition GetGameDefinition() => new()
    {
        Name = "Fika Headless Client",
        Slug = "fika-headless",
        ExecutableType = ExecutableType.Shell,
        DefaultExecutablePath = "",
        DefaultStartCommand = "",
        DefaultStopCommand = null,
        SupportsDocker = true,
        DefaultDockerImage = DockerImage,
        DockerDataPath = DataPath,
        DefaultPort = DefaultServerPort,
        IconPath = "/images/games/fika-headless.png",
        SupportsMods = false,
        ModProviders = [],
        RconDefaults = null,
        ConfigurationSchemas = BuildConfigurationSchemas()
    };

    private static Dictionary<string, ConfigurationSchema> BuildConfigurationSchemas()
    {
        var schemas = new Dictionary<string, ConfigurationSchema>();
        AddDockerEnvSchema(schemas);
        return schemas;
    }

    static partial void AddDockerEnvSchema(Dictionary<string, ConfigurationSchema> schemas);

    public string GetDefaultInstallPath(string serverName)
    {
        var sanitized = SanitizeForPath(serverName);
        var basePath = _pathResolver.GetServerBasePath();

        return OperatingSystem.IsWindows()
            ? Path.Combine(basePath, "FikaHeadlessClients", sanitized)
            : Path.Combine(basePath, "fika-headless-clients", sanitized);
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
        _logger.LogDebug("Fetching available Fika Headless versions");
        return await _versionFetcher.FetchVersionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<ServerInstallationResult> InstallServerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Preparing Fika Headless Client {Version} at {Path}", version, server.InstallPath);

        progress?.Report(OfficialInstallProgress.Starting("Preparing Fika Headless Client"));

        Directory.CreateDirectory(server.InstallPath);

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
        _logger.LogInformation("Updating Fika Headless Client to {Version} at {Path}",
            targetVersion, server.InstallPath);

        progress?.Report(OfficialInstallProgress.Starting("Updating Fika Headless Client"));
        progress?.Report(OfficialInstallProgress.Progress("Update",
            $"Docker image tag will be updated to {targetVersion} on next start.", 50));
        progress?.Report(OfficialInstallProgress.Completed());

        return Task.FromResult(ServerInstallationResult.Succeeded(server.InstallPath, targetVersion));
    }
}
