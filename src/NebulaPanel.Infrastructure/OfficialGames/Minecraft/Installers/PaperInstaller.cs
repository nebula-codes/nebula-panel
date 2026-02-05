using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Installs Paper server from PaperMC API.
/// </summary>
public class PaperInstaller(
    IHttpClientFactory httpClientFactory,
    ILogger<PaperInstaller> logger) : IMinecraftLoaderInstaller
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly ILogger<PaperInstaller> _logger = logger;

    private const string BaseUrl = "https://api.papermc.io/v2/projects/paper";

    public MinecraftLoader LoaderType => MinecraftLoader.Paper;

    public async Task<LoaderInstallResult> InstallAsync(
        MinecraftInstallContext context,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(context.LoaderVersion))
                return LoaderInstallResult.Failed("Paper build number is required");

            if (!int.TryParse(context.LoaderVersion, out var buildNumber))
                return LoaderInstallResult.Failed($"Invalid Paper build number: {context.LoaderVersion}");

            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Fetching build information...", 2));

            // 1. Fetch build info to get the download filename and SHA256
            _logger.LogDebug("Fetching Paper build info for {Version} build {Build}",
                context.MinecraftVersion, buildNumber);

            var buildInfoUrl = $"{BaseUrl}/versions/{context.MinecraftVersion}/builds/{buildNumber}";
            var buildInfo = await _httpClient.GetFromJsonAsync<PaperBuildResponse>(
                buildInfoUrl, cancellationToken).ConfigureAwait(false);

            if (buildInfo is null)
                return LoaderInstallResult.Failed($"Failed to fetch Paper build {buildNumber} info");

            // 2. Build download URL
            var fileName = buildInfo.Downloads.Application.Name;
            var downloadUrl = $"{BaseUrl}/versions/{context.MinecraftVersion}/builds/{buildNumber}/downloads/{fileName}";
            var expectedSha256 = buildInfo.Downloads.Application.Sha256;

            // 3. Download to server.jar
            var serverJarPath = Path.Combine(context.InstallPath, "server.jar");

            _logger.LogInformation("Downloading Paper {Version} build {Build} from {Url}",
                context.MinecraftVersion, buildNumber, downloadUrl);

            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading Paper server...", 5));

            await MinecraftDownloadHelper.DownloadAndVerifySha256Async(
                _httpClient,
                downloadUrl,
                serverJarPath,
                expectedSha256,
                progress,
                progressBasePercent: 5,
                progressTargetPercent: 95,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully installed Paper {Version} build {Build}",
                context.MinecraftVersion, buildNumber);

            return LoaderInstallResult.Succeeded("server.jar", context.VersionString);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to install Paper {Version} build {Build}",
                context.MinecraftVersion, context.LoaderVersion);
            return LoaderInstallResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    public StartupCommandInfo GetStartupCommand(MinecraftInstallContext context) => new()
    {
        ExecutablePath = "server.jar",
        Arguments = "nogui"
    };
}
