using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

namespace NebulaPanel.Infrastructure.OfficialGames.FikaHeadless;

/// <summary>
/// Official game provider for Fika Headless Client.
/// Docker-only deployment using ghcr.io/zhliau/fika-headless-docker.
/// Configuration schemas are defined in partial class files under Config/.
/// </summary>
public partial class FikaHeadlessProvider(
    FikaHeadlessVersionFetcher versionFetcher,
    IHttpClientFactory httpClientFactory,
    IServerPathResolver pathResolver,
    ILogger<FikaHeadlessProvider> logger) : IOfficialGameProvider
{
    private readonly FikaHeadlessVersionFetcher _versionFetcher = versionFetcher;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
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

    private const string FikaHeadlessReleasesUrl =
        "https://api.github.com/repos/project-fika/Fika-Headless/releases/latest";

    public async Task<ServerInstallationResult> InstallServerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Preparing Fika Headless Client {Version} at {Path}", version, server.InstallPath);

        progress?.Report(OfficialInstallProgress.Starting("Preparing Fika Headless Client"));

        Directory.CreateDirectory(server.InstallPath);

        progress?.Report(OfficialInstallProgress.Progress("Setup",
            "Server directory created. Docker will pull the image on first start.", 20));

        // Download Fika.Headless.dll if not already present
        await DownloadFikaHeadlessDllAsync(server, progress, cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(OfficialInstallProgress.Completed());
        return ServerInstallationResult.Succeeded(server.InstallPath, version);
    }

    /// <summary>
    /// Resolves the target path for Fika.Headless.dll extraction.
    /// For Docker servers, extracts into the volume host path (so files appear inside the volume
    /// in the file browser). Falls back to the server's install path.
    /// </summary>
    private static string GetExtractPath(GameServer server)
    {
        var volume = server.DockerConfig?.Volumes?
            .FirstOrDefault(v => !v.IsNamedVolume
                && !string.IsNullOrEmpty(v.HostPath)
                && v.ContainerPath == DataPath);

        return volume?.HostPath ?? server.InstallPath;
    }

    private async Task DownloadFikaHeadlessDllAsync(
        GameServer server,
        IProgress<OfficialInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var extractPath = GetExtractPath(server);
        var dllPath = Path.Combine(extractPath, "BepInEx", "plugins", "Fika", "Fika.Headless.dll");

        if (File.Exists(dllPath))
        {
            _logger.LogInformation("Fika.Headless.dll already exists at {Path}, skipping download", dllPath);
            progress?.Report(OfficialInstallProgress.Progress("Setup",
                "Fika.Headless.dll already present, skipping download.", 80));
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("FikaHeadless");

            // Fetch latest release metadata
            progress?.Report(OfficialInstallProgress.Progress("Setup",
                "Fetching latest Fika.Headless release info...", 30));

            var response = await client.GetAsync(FikaHeadlessReleasesUrl, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            // Find the zip asset download URL
            string? downloadUrl = null;
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (name is not null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                     && name.Contains("Fika.Headless", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                _logger.LogWarning("No Fika.Headless zip asset found in latest release");
                progress?.Report(OfficialInstallProgress.Progress("Setup",
                    "Could not find Fika.Headless download — you may need to add it manually.", 80));
                return;
            }

            // Download the zip to a temp file
            var tempZipPath = Path.Combine(Path.GetTempPath(), $"fika-headless-{Guid.NewGuid()}.zip");
            try
            {
                progress?.Report(OfficialInstallProgress.Progress("Downloading",
                    "Downloading Fika.Headless.dll...", 40));

                // Use a separate client with longer timeout for the download
                var downloadClient = _httpClientFactory.CreateClient("FikaHeadless");
                downloadClient.Timeout = TimeSpan.FromMinutes(2);

                await MinecraftDownloadHelper.DownloadFileAsync(
                    downloadClient, downloadUrl, tempZipPath, progress,
                    40, 70, cancellationToken).ConfigureAwait(false);

                // Extract the zip into the install path
                progress?.Report(OfficialInstallProgress.Progress("Extracting",
                    "Extracting Fika.Headless.dll...", 75));

                ZipFile.ExtractToDirectory(tempZipPath, extractPath, overwriteFiles: true);

                _logger.LogInformation("Successfully installed Fika.Headless.dll to {Path}", dllPath);
                progress?.Report(OfficialInstallProgress.Progress("Setup",
                    "Fika.Headless.dll installed successfully.", 80));
            }
            finally
            {
                if (File.Exists(tempZipPath))
                    File.Delete(tempZipPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download Fika.Headless.dll — the container can still start " +
                                   "but may not run in headless mode. You can add the DLL manually.");
            progress?.Report(OfficialInstallProgress.Progress("Setup",
                "Failed to download Fika.Headless.dll — you may need to add it manually.", 80));
        }
    }

    public async Task PrepareForStartAsync(
        GameServer server,
        CancellationToken cancellationToken = default)
    {
        await DownloadFikaHeadlessDllAsync(server, progress: null, cancellationToken)
            .ConfigureAwait(false);
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
