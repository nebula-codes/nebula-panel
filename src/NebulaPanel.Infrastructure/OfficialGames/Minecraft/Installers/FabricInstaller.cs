using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Installs Fabric server using the Fabric installer.
/// </summary>
public class FabricInstaller(
    IHttpClientFactory httpClientFactory,
    MinecraftJavaDetector javaDetector,
    ILogger<FabricInstaller> logger) : IMinecraftLoaderInstaller
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly MinecraftJavaDetector _javaDetector = javaDetector;
    private readonly ILogger<FabricInstaller> _logger = logger;

    private const string InstallerVersionsUrl = "https://meta.fabricmc.net/v2/versions/installer";

    public MinecraftLoader LoaderType => MinecraftLoader.Fabric;

    public async Task<LoaderInstallResult> InstallAsync(
        MinecraftInstallContext context,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? installerPath = null;

        try
        {
            if (string.IsNullOrEmpty(context.LoaderVersion))
                return LoaderInstallResult.Failed("Fabric loader version is required");

            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Fetching installer information...", 2));

            // 1. Get installer versions
            _logger.LogDebug("Fetching Fabric installer versions");
            var installerVersions = await _httpClient.GetFromJsonAsync<List<FabricInstallerVersion>>(
                InstallerVersionsUrl, cancellationToken).ConfigureAwait(false);

            if (installerVersions is null || installerVersions.Count == 0)
                return LoaderInstallResult.Failed("Failed to fetch Fabric installer versions");

            // Get latest stable installer
            var installer = installerVersions.FirstOrDefault(v => v.Stable)
                ?? installerVersions.First();

            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading Fabric installer...", 5));

            // 2. Download installer
            installerPath = Path.Combine(context.InstallPath, $"fabric-installer-{installer.Version}.jar");

            _logger.LogInformation("Downloading Fabric installer {Version} from {Url}",
                installer.Version, installer.Url);

            await MinecraftDownloadHelper.DownloadFileAsync(
                _httpClient,
                installer.Url,
                installerPath,
                progress,
                progressBasePercent: 5,
                progressTargetPercent: 30,
                cancellationToken).ConfigureAwait(false);

            progress?.Report(OfficialInstallProgress.Progress("Installing", "Running Fabric installer...", 35));

            // 3. Run installer
            // java -jar fabric-installer.jar server -mcversion {mc} -loader {loader} -downloadMinecraft -dir {dir}
            var javaPath = _javaDetector.ResolveJavaPath(context.JavaPath);

            var arguments = $"-jar \"{installerPath}\" server " +
                $"-mcversion {context.MinecraftVersion} " +
                $"-loader {context.LoaderVersion} " +
                $"-downloadMinecraft " +
                $"-dir \"{context.InstallPath}\"";

            _logger.LogInformation("Running Fabric installer: {Java} {Args}", javaPath, arguments);

            var exitCode = await RunInstallerProcessAsync(
                javaPath,
                arguments,
                context.InstallPath,
                progress,
                35, 80,
                cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
                return LoaderInstallResult.Failed($"Fabric installer failed with exit code {exitCode}");

            // 4. Verify installation
            var launcherJar = Path.Combine(context.InstallPath, "fabric-server-launch.jar");
            if (!File.Exists(launcherJar))
            {
                // Try alternate location for older installers
                var serverJar = Path.Combine(context.InstallPath, "server.jar");
                if (!File.Exists(serverJar))
                    return LoaderInstallResult.Failed("Fabric installation failed: fabric-server-launch.jar not found");
            }

            progress?.Report(OfficialInstallProgress.Progress("Finalizing", "Cleaning up...", 95));

            // 5. Clean up installer
            try
            {
                if (File.Exists(installerPath))
                    File.Delete(installerPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up Fabric installer");
            }

            _logger.LogInformation("Successfully installed Fabric {MC} with loader {Loader}",
                context.MinecraftVersion, context.LoaderVersion);

            return LoaderInstallResult.Succeeded("fabric-server-launch.jar", context.VersionString);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to install Fabric {MC} with loader {Loader}",
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
        ExecutablePath = "fabric-server-launch.jar",
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
                _logger.LogDebug("[Fabric] {Output}", e.Data);
                outputLines.Add(e.Data);
                totalLines++;

                // Simple progress estimation based on output lines
                // Fabric installer typically outputs ~50-100 lines
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
                _logger.LogWarning("[Fabric] {Error}", e.Data);
                outputLines.Add($"ERROR: {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogError("Fabric installer failed. Output:\n{Output}", string.Join("\n", outputLines.TakeLast(20)));
        }

        return process.ExitCode;
    }
}
