using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Installs vanilla Minecraft server from Mojang.
/// </summary>
public class VanillaInstaller(
    IHttpClientFactory httpClientFactory,
    ILogger<VanillaInstaller> logger) : IMinecraftLoaderInstaller
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly ILogger<VanillaInstaller> _logger = logger;

    private const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    public MinecraftLoader LoaderType => MinecraftLoader.Vanilla;

    public async Task<LoaderInstallResult> InstallAsync(
        MinecraftInstallContext context,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Fetching version manifest...", 2));

            // 1. Fetch version manifest
            _logger.LogDebug("Fetching Mojang version manifest");
            var manifest = await _httpClient.GetFromJsonAsync<VanillaVersionManifest>(
                ManifestUrl, cancellationToken).ConfigureAwait(false);

            if (manifest is null)
                return LoaderInstallResult.Failed("Failed to fetch Mojang version manifest");

            // 2. Find the requested version
            var versionEntry = manifest.Versions.FirstOrDefault(
                v => v.Id.Equals(context.MinecraftVersion, StringComparison.OrdinalIgnoreCase));

            if (versionEntry is null)
                return LoaderInstallResult.Failed($"Minecraft version {context.MinecraftVersion} not found");

            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Fetching version details...", 5));

            // 3. Fetch version details
            _logger.LogDebug("Fetching version details for {Version}", context.MinecraftVersion);
            var versionDetails = await _httpClient.GetFromJsonAsync<VanillaVersionDetails>(
                versionEntry.Url, cancellationToken).ConfigureAwait(false);

            if (versionDetails?.Downloads?.Server is null)
                return LoaderInstallResult.Failed($"No server download available for Minecraft {context.MinecraftVersion}");

            // 4. Download server.jar
            var serverJarPath = Path.Combine(context.InstallPath, "server.jar");
            var downloadUrl = versionDetails.Downloads.Server.Url;
            var expectedSha1 = versionDetails.Downloads.Server.Sha1;

            _logger.LogInformation("Downloading Vanilla server {Version} from {Url}",
                context.MinecraftVersion, downloadUrl);

            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading server.jar...", 10));

            await MinecraftDownloadHelper.DownloadAndVerifySha1Async(
                _httpClient,
                downloadUrl,
                serverJarPath,
                expectedSha1,
                progress,
                progressBasePercent: 10,
                progressTargetPercent: 95,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully installed Vanilla Minecraft {Version}", context.MinecraftVersion);

            return LoaderInstallResult.Succeeded("server.jar", context.VersionString);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to install Vanilla Minecraft {Version}", context.MinecraftVersion);
            return LoaderInstallResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    public StartupCommandInfo GetStartupCommand(MinecraftInstallContext context) => new()
    {
        ExecutablePath = "server.jar",
        Arguments = "nogui"
    };

    // Internal models for Mojang API
    private record VanillaVersionManifest
    {
        [JsonPropertyName("versions")]
        public required List<VanillaVersionEntry> Versions { get; init; }
    }

    private record VanillaVersionEntry
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("url")]
        public required string Url { get; init; }
    }

    private record VanillaVersionDetails
    {
        [JsonPropertyName("downloads")]
        public VanillaDownloads? Downloads { get; init; }
    }

    private record VanillaDownloads
    {
        [JsonPropertyName("server")]
        public VanillaServerDownload? Server { get; init; }
    }

    private record VanillaServerDownload
    {
        [JsonPropertyName("sha1")]
        public required string Sha1 { get; init; }

        [JsonPropertyName("url")]
        public required string Url { get; init; }
    }
}
