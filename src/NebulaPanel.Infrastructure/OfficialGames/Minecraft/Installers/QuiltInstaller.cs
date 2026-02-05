using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Installs Quilt server using the Quilt installer.
/// Quilt is a Fabric fork with different governance.
/// </summary>
public class QuiltInstaller(
    IHttpClientFactory httpClientFactory,
    MinecraftJavaDetector javaDetector,
    ILogger<QuiltInstaller> logger) : IMinecraftLoaderInstaller
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly MinecraftJavaDetector _javaDetector = javaDetector;
    private readonly ILogger<QuiltInstaller> _logger = logger;

    private const string InstallerVersionsUrl = "https://meta.quiltmc.org/v3/versions/installer";
    private const string LoaderVersionsUrl = "https://meta.quiltmc.org/v3/versions/loader";

    public MinecraftLoader LoaderType => MinecraftLoader.Quilt;

    public async Task<LoaderInstallResult> InstallAsync(
        MinecraftInstallContext context,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? installerPath = null;

        try
        {
            _logger.LogInformation("Starting Quilt installation. MC={McVersion}, LoaderVersion={LoaderVersion}",
                context.MinecraftVersion, context.LoaderVersion);

            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Fetching installer information...", 2));

            // Resolve loader version if needed
            var actualLoaderVersion = context.LoaderVersion;
            if (string.IsNullOrEmpty(actualLoaderVersion) ||
                actualLoaderVersion.Equals("latest", StringComparison.OrdinalIgnoreCase) ||
                actualLoaderVersion.Equals("recommended", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedVersion = await ResolveQuiltLoaderVersionAsync(cancellationToken).ConfigureAwait(false);
                if (resolvedVersion is null)
                {
                    return LoaderInstallResult.Failed("Could not resolve Quilt loader version");
                }

                _logger.LogInformation("Resolved Quilt loader version: {Version}", resolvedVersion);
                actualLoaderVersion = resolvedVersion;
            }

            // Get installer versions
            _logger.LogDebug("Fetching Quilt installer versions");
            var installerVersions = await _httpClient.GetFromJsonAsync<List<QuiltInstallerVersion>>(
                InstallerVersionsUrl, cancellationToken).ConfigureAwait(false);

            if (installerVersions is null || installerVersions.Count == 0)
                return LoaderInstallResult.Failed("Failed to fetch Quilt installer versions");

            // Get latest installer
            var installer = installerVersions.First();

            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading Quilt installer...", 5));

            // Download installer
            installerPath = Path.Combine(context.InstallPath, $"quilt-installer-{installer.Version}.jar");

            _logger.LogInformation("Downloading Quilt installer {Version} from {Url}",
                installer.Version, installer.Url);

            await MinecraftDownloadHelper.DownloadFileAsync(
                _httpClient,
                installer.Url,
                installerPath,
                progress,
                progressBasePercent: 5,
                progressTargetPercent: 30,
                cancellationToken).ConfigureAwait(false);

            progress?.Report(OfficialInstallProgress.Progress("Installing", "Running Quilt installer...", 35));

            // Run installer
            // java -jar quilt-installer.jar install server {mcVersion} {loaderVersion} --download-server --install-dir={dir}
            var javaPath = _javaDetector.ResolveJavaPath(context.JavaPath);

            var arguments = $"-jar \"{installerPath}\" install server " +
                $"{context.MinecraftVersion} {actualLoaderVersion} " +
                $"--download-server " +
                $"--install-dir=\"{context.InstallPath}\"";

            _logger.LogInformation("Running Quilt installer: {Java} {Args}", javaPath, arguments);

            var exitCode = await RunInstallerProcessAsync(
                javaPath,
                arguments,
                context.InstallPath,
                progress,
                35, 80,
                cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
                return LoaderInstallResult.Failed($"Quilt installer failed with exit code {exitCode}");

            // Verify installation
            var launcherJar = Path.Combine(context.InstallPath, "quilt-server-launch.jar");
            if (!File.Exists(launcherJar))
            {
                // Try alternate location
                var serverJar = Path.Combine(context.InstallPath, "server.jar");
                if (!File.Exists(serverJar))
                    return LoaderInstallResult.Failed("Quilt installation failed: quilt-server-launch.jar not found");
            }

            progress?.Report(OfficialInstallProgress.Progress("Finalizing", "Cleaning up...", 95));

            // Clean up installer
            try
            {
                if (File.Exists(installerPath))
                    File.Delete(installerPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up Quilt installer");
            }

            _logger.LogInformation("Successfully installed Quilt {MC} with loader {Loader}",
                context.MinecraftVersion, actualLoaderVersion);

            return LoaderInstallResult.Succeeded("quilt-server-launch.jar", context.VersionString);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to install Quilt {MC} with loader {Loader}",
                context.MinecraftVersion, context.LoaderVersion);

            // Clean up on failure
            try
            {
                if (installerPath is not null && File.Exists(installerPath))
                    File.Delete(installerPath);
            }
            catch { /* ignore cleanup errors */ }

            return LoaderInstallResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    public StartupCommandInfo GetStartupCommand(MinecraftInstallContext context) => new()
    {
        ExecutablePath = "quilt-server-launch.jar",
        Arguments = "nogui"
    };

    private async Task<int> RunInstallerProcessAsync(
        string javaPath,
        string arguments,
        string workingDirectory,
        IProgress<OfficialInstallProgress>? progress,
        double basePercent,
        double targetPercent,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var outputLines = new List<string>();
        var totalLines = 0;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                _logger.LogDebug("[Quilt] {Output}", e.Data);
                outputLines.Add(e.Data);
                totalLines++;

                // Simple progress estimation based on output lines
                var estimatedProgress = Math.Min(totalLines / 80.0, 1.0);
                var scaledProgress = basePercent + (estimatedProgress * (targetPercent - basePercent));

                progress?.Report(OfficialInstallProgress.Progress(
                    "Installing",
                    e.Data.Length > 60 ? e.Data[..60] + "..." : e.Data,
                    scaledProgress));
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                _logger.LogWarning("[Quilt] {Error}", e.Data);
                outputLines.Add($"ERROR: {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogError("Quilt installer failed. Output:\n{Output}", string.Join("\n", outputLines.TakeLast(20)));
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Resolves the latest stable Quilt loader version.
    /// </summary>
    private async Task<string?> ResolveQuiltLoaderVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var loaderVersions = await _httpClient.GetFromJsonAsync<List<QuiltLoaderVersion>>(
                LoaderVersionsUrl, cancellationToken).ConfigureAwait(false);

            if (loaderVersions is null || loaderVersions.Count == 0)
                return null;

            // Get latest stable version (not beta)
            var stableVersion = loaderVersions.FirstOrDefault(v => !v.Version.Contains("beta", StringComparison.OrdinalIgnoreCase));
            return stableVersion?.Version ?? loaderVersions.First().Version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Quilt loader versions");
            return null;
        }
    }

    /// <summary>
    /// Quilt installer version metadata.
    /// </summary>
    private sealed class QuiltInstallerVersion
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    /// <summary>
    /// Quilt loader version metadata.
    /// </summary>
    private sealed class QuiltLoaderVersion
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }
}
