using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Installs Spigot server using BuildTools.
/// Requires Git to be installed on the system.
/// </summary>
public class SpigotInstaller(
    IHttpClientFactory httpClientFactory,
    MinecraftJavaDetector javaDetector,
    ILogger<SpigotInstaller> logger) : IMinecraftLoaderInstaller
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly MinecraftJavaDetector _javaDetector = javaDetector;
    private readonly ILogger<SpigotInstaller> _logger = logger;

    private const string BuildToolsUrl = "https://hub.spigotmc.org/jenkins/job/BuildTools/lastSuccessfulBuild/artifact/target/BuildTools.jar";

    public MinecraftLoader LoaderType => MinecraftLoader.Spigot;

    public async Task<LoaderInstallResult> InstallAsync(
        MinecraftInstallContext context,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? buildToolsPath = null;
        string? buildDirectory = null;

        try
        {
            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Checking Git installation...", 2));

            // 1. Check if Git is installed
            var gitAvailable = await CheckGitAvailableAsync(cancellationToken).ConfigureAwait(false);
            if (!gitAvailable)
            {
                return LoaderInstallResult.Failed(
                    "Git is not installed or not in PATH. Spigot BuildTools requires Git to compile the server. " +
                    "Please install Git from https://git-scm.com/ and try again.");
            }

            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading BuildTools...", 5));

            // 2. Create build directory (BuildTools needs a clean directory)
            buildDirectory = Path.Combine(context.InstallPath, ".buildtools-temp");
            if (Directory.Exists(buildDirectory))
                Directory.Delete(buildDirectory, recursive: true);
            Directory.CreateDirectory(buildDirectory);

            // 3. Download BuildTools.jar
            buildToolsPath = Path.Combine(buildDirectory, "BuildTools.jar");

            _logger.LogInformation("Downloading BuildTools from {Url}", BuildToolsUrl);

            await MinecraftDownloadHelper.DownloadFileAsync(
                _httpClient,
                BuildToolsUrl,
                buildToolsPath,
                progress,
                progressBasePercent: 5,
                progressTargetPercent: 15,
                cancellationToken).ConfigureAwait(false);

            progress?.Report(OfficialInstallProgress.Progress(
                "Building",
                $"Building Spigot {context.MinecraftVersion} (this may take 5-15 minutes)...",
                20));

            // 4. Run BuildTools
            // java -jar BuildTools.jar --rev {version}
            var javaPath = _javaDetector.ResolveJavaPath(context.JavaPath);
            var arguments = $"-jar \"{buildToolsPath}\" --rev {context.MinecraftVersion}";

            _logger.LogInformation("Running BuildTools: {Java} {Args}", javaPath, arguments);
            _logger.LogInformation("This may take 5-15 minutes to compile...");

            var exitCode = await RunBuildToolsAsync(
                javaPath,
                arguments,
                buildDirectory,
                progress,
                20, 85,
                cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
                return LoaderInstallResult.Failed($"BuildTools failed with exit code {exitCode}. Check logs for details.");

            progress?.Report(OfficialInstallProgress.Progress("Finalizing", "Copying server jar...", 90));

            // 5. Find and copy the built spigot jar
            var spigotJarPattern = $"spigot-{context.MinecraftVersion}*.jar";
            var builtJars = Directory.GetFiles(buildDirectory, spigotJarPattern);

            if (builtJars.Length == 0)
            {
                // Try without version suffix (older BuildTools versions)
                builtJars = Directory.GetFiles(buildDirectory, "spigot-*.jar");
            }

            if (builtJars.Length == 0)
                return LoaderInstallResult.Failed($"BuildTools completed but spigot-{context.MinecraftVersion}.jar was not found");

            var sourceJar = builtJars[0];
            var targetJar = Path.Combine(context.InstallPath, "server.jar");

            _logger.LogInformation("Copying {Source} to {Target}", sourceJar, targetJar);
            File.Copy(sourceJar, targetJar, overwrite: true);

            progress?.Report(OfficialInstallProgress.Progress("Finalizing", "Cleaning up build files...", 95));

            // 6. Clean up build directory
            try
            {
                if (Directory.Exists(buildDirectory))
                    Directory.Delete(buildDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up BuildTools directory: {Dir}", buildDirectory);
            }

            _logger.LogInformation("Successfully built and installed Spigot {Version}", context.MinecraftVersion);

            return LoaderInstallResult.Succeeded("server.jar", context.VersionString);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to install Spigot {Version}", context.MinecraftVersion);

            // Clean up on failure
            try
            {
                if (buildDirectory is not null && Directory.Exists(buildDirectory))
                    Directory.Delete(buildDirectory, recursive: true);
            }
            catch { /* ignore cleanup errors */ }

            return LoaderInstallResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    public StartupCommandInfo GetStartupCommand(MinecraftInstallContext context) => new()
    {
        ExecutablePath = "server.jar",
        Arguments = "nogui"
    };

    private static async Task<bool> CheckGitAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken)
                .ConfigureAwait(false);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return process.ExitCode == 0 && output.Contains("git version", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<int> RunBuildToolsAsync(
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

        // BuildTools has distinct phases we can track
        var currentPhase = "Initializing";
        var phaseProgress = new Dictionary<string, double>
        {
            ["Initializing"] = 0,
            ["Pulling"] = 0.1,           // Pulling from Git
            ["Downloading"] = 0.2,        // Downloading files
            ["Applying"] = 0.4,           // Applying patches
            ["Compiling"] = 0.6,          // Compiling
            ["Mapping"] = 0.8,            // Mapping classes
            ["Finalizing"] = 0.95
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                _logger.LogDebug("[BuildTools] {Output}", e.Data);
                outputLines.Add(e.Data);

                // Detect phase changes
                var line = e.Data.ToLowerInvariant();
                if (line.Contains("pulling") || line.Contains("cloning"))
                    currentPhase = "Pulling";
                else if (line.Contains("downloading"))
                    currentPhase = "Downloading";
                else if (line.Contains("applying") || line.Contains("patching"))
                    currentPhase = "Applying";
                else if (line.Contains("compiling") || line.Contains("maven"))
                    currentPhase = "Compiling";
                else if (line.Contains("mapping") || line.Contains("remapping"))
                    currentPhase = "Mapping";
                else if (line.Contains("success") || line.Contains("complete"))
                    currentPhase = "Finalizing";

                var phaseValue = phaseProgress.GetValueOrDefault(currentPhase, 0);
                var scaledProgress = basePercent + (phaseValue * (targetPercent - basePercent));

                var message = e.Data;
                if (message.Length > 60)
                    message = message[..60] + "...";

                progress?.Report(OfficialInstallProgress.Progress("Building", message, scaledProgress));
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                // BuildTools writes progress to stderr
                _logger.LogDebug("[BuildTools] {Error}", e.Data);
                outputLines.Add(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogError("BuildTools failed. Last 50 lines:\n{Output}",
                string.Join("\n", outputLines.TakeLast(50)));
        }

        return process.ExitCode;
    }
}
