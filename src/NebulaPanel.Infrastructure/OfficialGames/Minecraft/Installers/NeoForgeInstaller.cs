using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Installs NeoForge server using the NeoForge installer.
/// NeoForge is the continuation of Forge for Minecraft 1.20.2+.
/// </summary>
public class NeoForgeInstaller(
    IHttpClientFactory httpClientFactory,
    MinecraftJavaDetector javaDetector,
    ILogger<NeoForgeInstaller> logger) : IMinecraftLoaderInstaller
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly MinecraftJavaDetector _javaDetector = javaDetector;
    private readonly ILogger<NeoForgeInstaller> _logger = logger;

    private const string MavenBase = "https://maven.neoforged.net/releases/net/neoforged/neoforge";
    private const string VersionsApiUrl = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge";

    public MinecraftLoader LoaderType => MinecraftLoader.NeoForge;

    public async Task<LoaderInstallResult> InstallAsync(
        MinecraftInstallContext context,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? installerPath = null;

        try
        {
            _logger.LogInformation("Starting NeoForge installation. MC={McVersion}, LoaderVersion={LoaderVersion}",
                context.MinecraftVersion, context.LoaderVersion);

            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Resolving NeoForge version...", 2));

            // Resolve version - NeoForge versions are like "21.0.167" where 21 = MC 1.21, etc.
            var actualLoaderVersion = context.LoaderVersion;
            if (string.IsNullOrEmpty(actualLoaderVersion) ||
                actualLoaderVersion.Equals("latest", StringComparison.OrdinalIgnoreCase) ||
                actualLoaderVersion.Equals("recommended", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedVersion = await ResolveNeoForgeVersionAsync(
                    context.MinecraftVersion,
                    cancellationToken).ConfigureAwait(false);

                if (resolvedVersion is null)
                {
                    return LoaderInstallResult.Failed(
                        $"Could not find a NeoForge version for Minecraft {context.MinecraftVersion}. " +
                        "This Minecraft version may not have NeoForge support yet.");
                }

                _logger.LogInformation("Resolved NeoForge version for MC {McVersion}: {NeoForgeVersion}",
                    context.MinecraftVersion, resolvedVersion);

                actualLoaderVersion = resolvedVersion;
            }

            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading NeoForge installer...", 5));

            // Download NeoForge installer
            // URL format: https://maven.neoforged.net/releases/net/neoforged/neoforge/{version}/neoforge-{version}-installer.jar
            var installerFileName = $"neoforge-{actualLoaderVersion}-installer.jar";
            var downloadUrl = $"{MavenBase}/{actualLoaderVersion}/{installerFileName}";
            installerPath = Path.Combine(context.InstallPath, installerFileName);

            _logger.LogInformation("Downloading NeoForge installer from {Url}", downloadUrl);

            try
            {
                await MinecraftDownloadHelper.DownloadFileAsync(
                    _httpClient,
                    downloadUrl,
                    installerPath,
                    progress,
                    progressBasePercent: 5,
                    progressTargetPercent: 30,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download NeoForge installer from {Url}", downloadUrl);
                return LoaderInstallResult.Failed($"Failed to download NeoForge installer: {ex.Message}");
            }

            progress?.Report(OfficialInstallProgress.Progress("Installing", "Running NeoForge installer...", 35));

            // Run installer
            // java -jar neoforge-installer.jar --install-server
            var javaPath = _javaDetector.ResolveJavaPath(context.JavaPath);
            var arguments = $"-jar \"{installerPath}\" --install-server";

            _logger.LogInformation("Running NeoForge installer: {Java} {Args}", javaPath, arguments);

            var exitCode = await RunInstallerProcessAsync(
                javaPath,
                arguments,
                context.InstallPath,
                progress,
                35, 85,
                cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
                return LoaderInstallResult.Failed($"NeoForge installer failed with exit code {exitCode}");

            progress?.Report(OfficialInstallProgress.Progress("Finalizing", "Configuring server...", 90));

            // Verify installation - NeoForge creates similar structure to modern Forge
            var argsFileName = OperatingSystem.IsWindows() ? "win_args.txt" : "unix_args.txt";
            var argsPath = Path.Combine(context.InstallPath, "libraries", "net", "neoforged", "neoforge", actualLoaderVersion, argsFileName);

            // Create user_jvm_args.txt if it doesn't exist
            await MinecraftConfigFiles.CreateForgeJvmArgsAsync(
                context.InstallPath,
                "-Xmx4G -Xms2G",
                cancellationToken).ConfigureAwait(false);

            if (!File.Exists(argsPath))
            {
                // Try to find any args file in the neoforge libraries
                var librariesDir = Path.Combine(context.InstallPath, "libraries", "net", "neoforged", "neoforge");
                if (Directory.Exists(librariesDir))
                {
                    var argsFiles = Directory.GetFiles(librariesDir, argsFileName, SearchOption.AllDirectories);
                    if (argsFiles.Length == 0)
                    {
                        _logger.LogWarning("Could not find NeoForge args file at expected location: {Path}", argsPath);
                    }
                    else
                    {
                        _logger.LogInformation("Found NeoForge args file at: {Path}", argsFiles[0]);
                    }
                }
            }

            // Clean up installer
            try
            {
                if (File.Exists(installerPath))
                    File.Delete(installerPath);

                var logPath = Path.Combine(context.InstallPath, "installer.log");
                if (File.Exists(logPath))
                    File.Delete(logPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up NeoForge installer files");
            }

            _logger.LogInformation("Successfully installed NeoForge {Version}", actualLoaderVersion);

            return LoaderInstallResult.Succeeded(null, $"neoforge-{actualLoaderVersion}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to install NeoForge {MC}-{Version}",
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

    public StartupCommandInfo GetStartupCommand(MinecraftInstallContext context)
    {
        // NeoForge uses the same args file syntax as modern Forge
        var argsFileName = OperatingSystem.IsWindows() ? "win_args.txt" : "unix_args.txt";

        // Find the actual args file path
        var argsPath = FindNeoForgeArgsFile(context.InstallPath, argsFileName)
                       ?? $"libraries/net/neoforged/neoforge/{context.LoaderVersion}/{argsFileName}";

        return new StartupCommandInfo
        {
            ExecutablePath = null, // Java runs directly with args
            Arguments = $"@user_jvm_args.txt @{argsPath} nogui",
            UseArgsFile = true
        };
    }

    /// <summary>
    /// Finds the actual NeoForge args file path.
    /// </summary>
    private static string? FindNeoForgeArgsFile(string installPath, string argsFileName)
    {
        var neoforgeLibDir = Path.Combine(installPath, "libraries", "net", "neoforged", "neoforge");
        if (!Directory.Exists(neoforgeLibDir))
            return null;

        // Find directories containing the args file
        var neoForgeDirs = Directory.GetDirectories(neoforgeLibDir);
        foreach (var dir in neoForgeDirs.OrderByDescending(d => new DirectoryInfo(d).CreationTime))
        {
            var argsFile = Path.Combine(dir, argsFileName);
            if (File.Exists(argsFile))
            {
                var dirName = Path.GetFileName(dir);
                return $"libraries/net/neoforged/neoforge/{dirName}/{argsFileName}";
            }
        }

        return null;
    }

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
                _logger.LogDebug("[NeoForge] {Output}", e.Data);
                outputLines.Add(e.Data);
                totalLines++;

                // NeoForge installer outputs many lines during library downloads
                var estimatedProgress = Math.Min(totalLines / 200.0, 1.0);
                var scaledProgress = basePercent + (estimatedProgress * (targetPercent - basePercent));

                // Show meaningful progress messages
                var message = e.Data;
                if (message.Contains("Downloading", StringComparison.OrdinalIgnoreCase))
                    message = "Downloading libraries...";
                else if (message.Contains("Extracting", StringComparison.OrdinalIgnoreCase))
                    message = "Extracting files...";
                else if (message.Length > 50)
                    message = message[..50] + "...";

                progress?.Report(OfficialInstallProgress.Progress("Installing", message, scaledProgress));
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                _logger.LogDebug("[NeoForge] {Error}", e.Data);
                outputLines.Add(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogError("NeoForge installer failed. Output:\n{Output}", string.Join("\n", outputLines.TakeLast(30)));
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Resolves the latest NeoForge version for a given Minecraft version.
    /// NeoForge version numbering: first number corresponds to MC minor version (21 = 1.21, 20 = 1.20, etc.)
    /// </summary>
    private async Task<string?> ResolveNeoForgeVersionAsync(
        string minecraftVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Resolving NeoForge version for MC {McVersion}", minecraftVersion);

            var response = await _httpClient.GetAsync(VersionsApiUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var versionsResponse = await response.Content.ReadFromJsonAsync<NeoForgeVersionsResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (versionsResponse?.Versions is null || versionsResponse.Versions.Count == 0)
            {
                _logger.LogWarning("NeoForge versions API returned null or empty versions");
                return null;
            }

            _logger.LogDebug("NeoForge API returned {Count} versions", versionsResponse.Versions.Count);

            // Parse MC version to determine NeoForge version prefix
            // MC 1.20.4 -> NeoForge versions starting with "20.4"
            // MC 1.21.0 -> NeoForge versions starting with "21.0"
            var versionPrefix = GetNeoForgeVersionPrefix(minecraftVersion);
            if (versionPrefix is null)
            {
                _logger.LogWarning("Could not determine NeoForge version prefix for MC {McVersion}", minecraftVersion);
                return null;
            }

            _logger.LogDebug("Looking for NeoForge versions starting with {Prefix}", versionPrefix);

            // Find all versions matching the prefix, take the latest (last in list, typically)
            var matchingVersions = versionsResponse.Versions
                .Where(v => v.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matchingVersions.Count == 0)
            {
                _logger.LogWarning("No NeoForge versions found for prefix {Prefix}. Available prefixes: {Prefixes}",
                    versionPrefix,
                    string.Join(", ", versionsResponse.Versions
                        .Select(v => v.Split('.').Take(2).Aggregate((a, b) => $"{a}.{b}"))
                        .Distinct()
                        .Take(10)));
                return null;
            }

            var latestVersion = matchingVersions.First();
            _logger.LogDebug("Found {Count} matching versions, using latest: {Version}",
                matchingVersions.Count, latestVersion);

            return latestVersion;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch NeoForge versions from API");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error resolving NeoForge version");
            return null;
        }
    }

    /// <summary>
    /// Gets the NeoForge version prefix for a Minecraft version.
    /// MC 1.20.4 -> "20.4"
    /// MC 1.21.0 -> "21.0" or "21." (for latest patch)
    /// </summary>
    private static string? GetNeoForgeVersionPrefix(string minecraftVersion)
    {
        var parts = minecraftVersion.Split('.');
        if (parts.Length < 2)
            return null;

        if (!int.TryParse(parts[1], out var minor))
            return null;

        // NeoForge only supports 1.20.2+
        if (parts[0] != "1" || minor < 20)
            return null;

        if (parts.Length >= 3 && int.TryParse(parts[2], out var patch))
        {
            // For 1.20.x where x >= 2, prefix is "20.x"
            // For 1.21.x, prefix is "21.x"
            return $"{minor}.{patch}";
        }

        // For versions like "1.21" without patch, use "21."
        return $"{minor}.";
    }

    /// <summary>
    /// Response from the NeoForge versions API.
    /// </summary>
    private sealed class NeoForgeVersionsResponse
    {
        [JsonPropertyName("versions")]
        public List<string> Versions { get; set; } = [];
    }
}
