using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Installs Purpur server from Purpur API.
/// </summary>
public class PurpurInstaller(
    IHttpClientFactory httpClientFactory,
    ILogger<PurpurInstaller> logger) : IMinecraftLoaderInstaller
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly ILogger<PurpurInstaller> _logger = logger;

    private const string BaseUrl = "https://api.purpurmc.org/v2/purpur";

    public MinecraftLoader LoaderType => MinecraftLoader.Purpur;

    public async Task<LoaderInstallResult> InstallAsync(
        MinecraftInstallContext context,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(context.LoaderVersion))
                return LoaderInstallResult.Failed("Purpur build number is required");

            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Fetching build information...", 2));

            // 1. Fetch build info to get MD5 hash
            _logger.LogDebug("Fetching Purpur build info for {Version} build {Build}",
                context.MinecraftVersion, context.LoaderVersion);

            var buildInfoUrl = $"{BaseUrl}/{context.MinecraftVersion}/{context.LoaderVersion}";
            var buildInfo = await _httpClient.GetFromJsonAsync<PurpurBuildResponse>(
                buildInfoUrl, cancellationToken).ConfigureAwait(false);

            if (buildInfo is null)
                return LoaderInstallResult.Failed($"Failed to fetch Purpur build {context.LoaderVersion} info");

            // 2. Download server jar
            var downloadUrl = $"{BaseUrl}/{context.MinecraftVersion}/{context.LoaderVersion}/download";
            var serverJarPath = Path.Combine(context.InstallPath, "server.jar");

            _logger.LogInformation("Downloading Purpur {Version} build {Build} from {Url}",
                context.MinecraftVersion, context.LoaderVersion, downloadUrl);

            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading Purpur server...", 5));

            // Purpur provides MD5, but we'll just download without verification for simplicity
            // The API doesn't provide SHA1/SHA256 consistently
            await MinecraftDownloadHelper.DownloadFileAsync(
                _httpClient,
                downloadUrl,
                serverJarPath,
                progress,
                progressBasePercent: 5,
                progressTargetPercent: 95,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully installed Purpur {Version} build {Build}",
                context.MinecraftVersion, context.LoaderVersion);

            return LoaderInstallResult.Succeeded("server.jar", context.VersionString);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to install Purpur {Version} build {Build}",
                context.MinecraftVersion, context.LoaderVersion);
            return LoaderInstallResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    public StartupCommandInfo GetStartupCommand(MinecraftInstallContext context) => new()
    {
        ExecutablePath = "server.jar",
        Arguments = "nogui"
    };

    // Internal models for Purpur API
    private record PurpurBuildResponse
    {
        [JsonPropertyName("project")]
        public required string Project { get; init; }

        [JsonPropertyName("version")]
        public required string Version { get; init; }

        [JsonPropertyName("build")]
        public required string Build { get; init; }

        [JsonPropertyName("result")]
        public required string Result { get; init; }

        [JsonPropertyName("md5")]
        public string? Md5 { get; init; }
    }
}
