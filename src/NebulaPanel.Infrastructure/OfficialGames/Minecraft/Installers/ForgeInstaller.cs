using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Installs Forge server using the Forge installer.
/// </summary>
public class ForgeInstaller(
    IHttpClientFactory httpClientFactory,
    MinecraftJavaDetector javaDetector,
    ILogger<ForgeInstaller> logger) : IMinecraftLoaderInstaller
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly MinecraftJavaDetector _javaDetector = javaDetector;
    private readonly ILogger<ForgeInstaller> _logger = logger;

    private const string MavenBase = "https://maven.minecraftforge.net/net/minecraftforge/forge";
    private const string PromotionsUrl = "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";

    public MinecraftLoader LoaderType => MinecraftLoader.Forge;

    public async Task<LoaderInstallResult> InstallAsync(
        MinecraftInstallContext context,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? installerPath = null;

        try
        {
            if (string.IsNullOrEmpty(context.LoaderVersion))
                return LoaderInstallResult.Failed("Forge version is required");

            _logger.LogInformation("Starting Forge installation. MC={McVersion}, LoaderVersion={LoaderVersion}",
                context.MinecraftVersion, context.LoaderVersion);

            // Resolve "recommended" or "latest" to actual Forge version number
            var actualLoaderVersion = context.LoaderVersion;
            if (context.LoaderVersion.Equals("recommended", StringComparison.OrdinalIgnoreCase) ||
                context.LoaderVersion.Equals("latest", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(OfficialInstallProgress.Progress("Resolving", "Looking up Forge version...", 2));

                var resolvedVersion = await ResolveForgeVersionAsync(
                    context.MinecraftVersion,
                    context.LoaderVersion,
                    cancellationToken).ConfigureAwait(false);

                if (resolvedVersion is null)
                {
                    return LoaderInstallResult.Failed(
                        $"Could not find a {context.LoaderVersion} Forge version for Minecraft {context.MinecraftVersion}. " +
                        "This Minecraft version may not have Forge support, or the Forge API may be unavailable.");
                }

                _logger.LogInformation("Resolved Forge {Type} for MC {McVersion} to version {ForgeVersion}",
                    context.LoaderVersion, context.MinecraftVersion, resolvedVersion);

                actualLoaderVersion = resolvedVersion;
            }

            var forgeVersion = $"{context.MinecraftVersion}-{actualLoaderVersion}";

            _logger.LogInformation("Using Forge version string: {ForgeVersion} (actualLoaderVersion={ActualLoaderVersion})",
                forgeVersion, actualLoaderVersion);

            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading Forge installer...", 5));

            // 1. Download Forge installer
            // URL format: https://maven.minecraftforge.net/net/minecraftforge/forge/{mc}-{forge}/forge-{mc}-{forge}-installer.jar
            var installerFileName = $"forge-{forgeVersion}-installer.jar";
            var downloadUrl = $"{MavenBase}/{forgeVersion}/{installerFileName}";
            installerPath = Path.Combine(context.InstallPath, installerFileName);

            _logger.LogInformation("Downloading Forge installer from {Url}", downloadUrl);

            // Try multiple URL formats as Forge Maven structure has changed over versions
            // Legacy versions (pre-1.13) have MC version twice: 1.7.10-10.13.4.1614-1.7.10
            var legacyForgeVersion = $"{forgeVersion}-{context.MinecraftVersion}";
            var legacyInstallerFileName = $"forge-{legacyForgeVersion}-installer.jar";

            var urlsToTry = new[]
            {
                // Primary Maven URL (modern format)
                // Example: forge/1.20.4-49.0.38/forge-1.20.4-49.0.38-installer.jar
                downloadUrl,

                // Legacy format with MC version suffix (for 1.7.10, 1.8.9, 1.10.2, 1.11.2, 1.12.2, etc.)
                // Example: forge/1.7.10-10.13.4.1614-1.7.10/forge-1.7.10-10.13.4.1614-1.7.10-installer.jar
                $"{MavenBase}/{legacyForgeVersion}/{legacyInstallerFileName}",

                // Alternative file hosting URL (modern)
                $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{forgeVersion}/{installerFileName}",

                // Alternative file hosting URL (legacy)
                $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{legacyForgeVersion}/{legacyInstallerFileName}",

                // Universal jar fallback for very old versions
                $"{MavenBase}/{forgeVersion}/forge-{forgeVersion}-universal.jar",
                $"{MavenBase}/{legacyForgeVersion}/forge-{legacyForgeVersion}-universal.jar"
            };

            HttpRequestException? lastException = null;
            string? successfulUrl = null;
            bool isUniversalJar = false;
            var attemptedUrls = new List<string>();

            _logger.LogInformation("Attempting to download Forge {ForgeVersion} for MC {McVersion}",
                context.LoaderVersion, context.MinecraftVersion);
            _logger.LogInformation("Will try {Count} URL formats", urlsToTry.Length);

            foreach (var url in urlsToTry)
            {
                attemptedUrls.Add(url);
                try
                {
                    // Determine the local filename based on the URL
                    var fileName = Path.GetFileName(new Uri(url).LocalPath);
                    var localPath = Path.Combine(context.InstallPath, fileName);

                    _logger.LogInformation("Trying Forge download URL ({Index}/{Total}): {Url}",
                        attemptedUrls.Count, urlsToTry.Length, url);

                    await MinecraftDownloadHelper.DownloadFileAsync(
                        _httpClient,
                        url,
                        localPath,
                        progress,
                        progressBasePercent: 5,
                        progressTargetPercent: 30,
                        cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Successfully downloaded Forge from {Url}", url);
                    installerPath = localPath;
                    successfulUrl = url;
                    isUniversalJar = url.Contains("-universal.jar");
                    lastException = null;
                    break;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning("Failed to download from URL {Index}/{Total}: {Url} - HTTP Error: {Message}",
                        attemptedUrls.Count, urlsToTry.Length, url, ex.Message);
                    lastException = ex;
                }
                catch (InvalidOperationException ex) when (ex.InnerException is HttpRequestException httpEx)
                {
                    // MinecraftDownloadHelper wraps HttpRequestException in InvalidOperationException after retries
                    _logger.LogWarning("Failed to download from URL {Index}/{Total} after retries: {Url} - HTTP Error: {Message}",
                        attemptedUrls.Count, urlsToTry.Length, url, httpEx.Message);
                    lastException = httpEx;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning("Failed to download from URL {Index}/{Total}: {Url} - Error: {Message}",
                        attemptedUrls.Count, urlsToTry.Length, url, ex.Message);
                    lastException = new HttpRequestException(ex.Message, ex);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexpected error downloading from URL {Index}/{Total}: {Url}",
                        attemptedUrls.Count, urlsToTry.Length, url);
                    lastException = new HttpRequestException($"Unexpected error: {ex.Message}", ex);
                }
            }

            if (lastException is not null)
            {
                // Log all attempted URLs for debugging
                _logger.LogError("Failed to download Forge from all {Count} URLs. Last error: {Error}",
                    attemptedUrls.Count, lastException.Message);
                foreach (var attemptedUrl in attemptedUrls)
                {
                    _logger.LogError("  Attempted URL: {Url}", attemptedUrl);
                }

                // Check if user is trying to use old Forge on a version where NeoForge is recommended
                var isNeoForgeRecommended = IsNeoForgeRecommended(context.MinecraftVersion);
                var errorMessage = $"Failed to download Forge installer after trying {attemptedUrls.Count} URLs. " +
                                   $"Last error: {lastException.Message}. ";

                if (isNeoForgeRecommended)
                {
                    errorMessage +=
                        $"Note: For Minecraft {context.MinecraftVersion}, the original Forge team now develops NeoForge instead. " +
                        "Many mods and Forge installers may not be available for this MC version on the original Forge Maven. " +
                        "Consider using Fabric, or wait for NeoForge support to be added.";
                }
                else
                {
                    errorMessage += $"Forge version: {forgeVersion}. Check logs for attempted URLs.";
                }

                return LoaderInstallResult.Failed(errorMessage);
            }

            // Handle universal jar (no installer needed - it's the server jar itself)
            if (isUniversalJar)
            {
                _logger.LogInformation("Downloaded universal jar directly (no installer needed)");
                progress?.Report(OfficialInstallProgress.Progress("Finalizing", "Setting up universal jar...", 90));

                // Rename universal jar to a standard name
                var universalJarName = Path.GetFileName(installerPath);
                var targetJarPath = Path.Combine(context.InstallPath, $"forge-{forgeVersion}-universal.jar");

                if (installerPath != targetJarPath && File.Exists(installerPath))
                {
                    if (File.Exists(targetJarPath))
                        File.Delete(targetJarPath);
                    File.Move(installerPath, targetJarPath);
                }

                _logger.LogInformation("Successfully set up Forge universal jar: {Version}", forgeVersion);
                return LoaderInstallResult.Succeeded(
                    $"forge-{forgeVersion}-universal.jar",
                    context.VersionString);
            }

            progress?.Report(OfficialInstallProgress.Progress("Installing", "Running Forge installer...", 35));

            // 2. Run installer
            // java -jar forge-installer.jar --installServer
            var javaPath = _javaDetector.ResolveJavaPath(context.JavaPath);
            var arguments = $"-jar \"{installerPath}\" --installServer";

            _logger.LogInformation("Running Forge installer: {Java} {Args}", javaPath, arguments);

            var exitCode = await RunInstallerProcessAsync(
                javaPath,
                arguments,
                context.InstallPath,
                progress,
                35, 85,
                cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
                return LoaderInstallResult.Failed($"Forge installer failed with exit code {exitCode}");

            progress?.Report(OfficialInstallProgress.Progress("Finalizing", "Cleaning up...", 90));

            // 3. Determine startup method and verify installation
            var isModernForge = IsModernForge(context.MinecraftVersion);

            if (isModernForge)
            {
                // Modern Forge (1.17+) creates run scripts and args files
                var argsFileName = OperatingSystem.IsWindows() ? "win_args.txt" : "unix_args.txt";
                var argsPath = Path.Combine(context.InstallPath, "libraries", "net", "minecraftforge", "forge", forgeVersion, argsFileName);

                // Create user_jvm_args.txt if it doesn't exist
                await MinecraftConfigFiles.CreateForgeJvmArgsAsync(
                    context.InstallPath,
                    "-Xmx4G -Xms2G",
                    cancellationToken).ConfigureAwait(false);

                if (!File.Exists(argsPath))
                {
                    // Try to find any args file
                    var librariesDir = Path.Combine(context.InstallPath, "libraries");
                    if (Directory.Exists(librariesDir))
                    {
                        var argsFiles = Directory.GetFiles(librariesDir, argsFileName, SearchOption.AllDirectories);
                        if (argsFiles.Length == 0)
                        {
                            _logger.LogWarning("Could not find Forge args file at expected location");
                        }
                    }
                }
            }
            else
            {
                // Legacy Forge creates a server jar directly
                var legacyJar = Path.Combine(context.InstallPath, $"forge-{forgeVersion}.jar");
                var universalJar = Path.Combine(context.InstallPath, $"forge-{forgeVersion}-universal.jar");

                if (!File.Exists(legacyJar) && !File.Exists(universalJar))
                {
                    // Check for minecraft_server jar as fallback indicator
                    var serverJar = Directory.GetFiles(context.InstallPath, "minecraft_server*.jar").FirstOrDefault();
                    if (serverJar is null)
                        _logger.LogWarning("Could not verify Forge installation - no expected jar files found");
                }
            }

            // 4. Clean up installer
            try
            {
                if (File.Exists(installerPath))
                    File.Delete(installerPath);

                // Also clean up installer log
                var logPath = Path.Combine(context.InstallPath, "installer.log");
                if (File.Exists(logPath))
                    File.Delete(logPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up Forge installer files");
            }

            _logger.LogInformation("Successfully installed Forge {Version}", forgeVersion);

            // Find the actual jar file for legacy Forge (naming conventions vary)
            string? executablePath = null;
            if (!isModernForge)
            {
                executablePath = FindForgeJar(context.InstallPath, context.MinecraftVersion);
                if (executablePath is null)
                {
                    // Fallback to expected name
                    executablePath = $"forge-{forgeVersion}.jar";
                    _logger.LogWarning("Could not find Forge jar file, using expected name: {Name}", executablePath);
                }
                else
                {
                    _logger.LogInformation("Found Forge jar file: {Name}", executablePath);
                }
            }

            return LoaderInstallResult.Succeeded(
                executablePath,
                context.VersionString);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to install Forge {MC}-{Forge}",
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
        var forgeVersion = $"{context.MinecraftVersion}-{context.LoaderVersion}";
        var isModernForge = IsModernForge(context.MinecraftVersion);

        if (isModernForge)
        {
            // Modern Forge uses @file argument syntax
            var argsFileName = OperatingSystem.IsWindows() ? "win_args.txt" : "unix_args.txt";

            // Find the actual args file path - LoaderVersion might be "recommended" instead of actual version
            var argsPath = FindForgeArgsFile(context.InstallPath, context.MinecraftVersion, argsFileName)
                           ?? $"libraries/net/minecraftforge/forge/{forgeVersion}/{argsFileName}";

            return new StartupCommandInfo
            {
                ExecutablePath = null, // Java runs directly with args
                Arguments = $"@user_jvm_args.txt @{argsPath} nogui",
                UseArgsFile = true
            };
        }
        else
        {
            // Legacy Forge uses a jar file - find the actual filename
            var jarFile = FindForgeJar(context.InstallPath, context.MinecraftVersion)
                          ?? $"forge-{forgeVersion}.jar"; // Fallback to expected name

            return new StartupCommandInfo
            {
                ExecutablePath = jarFile,
                Arguments = "nogui"
            };
        }
    }

    /// <summary>
    /// Finds the actual Forge args file path for modern Forge installations.
    /// The folder name contains the resolved Forge version, not "recommended" or "latest".
    /// </summary>
    private static string? FindForgeArgsFile(string installPath, string minecraftVersion, string argsFileName)
    {
        var forgeLibDir = Path.Combine(installPath, "libraries", "net", "minecraftforge", "forge");
        if (!Directory.Exists(forgeLibDir))
            return null;

        // Look for directories that start with the MC version
        var forgeDirs = Directory.GetDirectories(forgeLibDir, $"{minecraftVersion}-*");
        if (forgeDirs.Length == 0)
            return null;

        // Find one that contains the args file
        foreach (var dir in forgeDirs.OrderByDescending(d => new DirectoryInfo(d).CreationTime))
        {
            var argsFile = Path.Combine(dir, argsFileName);
            if (File.Exists(argsFile))
            {
                var dirName = Path.GetFileName(dir);
                return $"libraries/net/minecraftforge/forge/{dirName}/{argsFileName}";
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the actual Forge server jar file in the install directory.
    /// Legacy Forge creates jars with various naming conventions.
    /// </summary>
    private static string? FindForgeJar(string installPath, string minecraftVersion)
    {
        if (!Directory.Exists(installPath))
            return null;

        // Look for forge jar files - they can have various naming patterns:
        // - forge-1.7.10-10.13.4.1614-1.7.10-universal.jar (legacy with MC version suffix)
        // - forge-1.12.2-14.23.5.2859.jar (legacy without suffix)
        // - forge-1.16.5-36.2.39.jar (intermediate)
        var forgeJars = Directory.GetFiles(installPath, "forge-*.jar")
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                // Must start with forge- and contain the MC version
                // Exclude installer jars
                return name.StartsWith($"forge-{minecraftVersion}", StringComparison.OrdinalIgnoreCase) &&
                       !name.Contains("-installer", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(f => new FileInfo(f).Length) // Prefer larger files (universal jars are bigger)
            .ToList();

        if (forgeJars.Count > 0)
            return Path.GetFileName(forgeJars[0]);

        // Fallback: look for any forge jar that's not an installer
        var anyForgeJar = Directory.GetFiles(installPath, "forge-*.jar")
            .FirstOrDefault(f => !Path.GetFileName(f).Contains("-installer", StringComparison.OrdinalIgnoreCase));

        return anyForgeJar is not null ? Path.GetFileName(anyForgeJar) : null;
    }

    /// <summary>
    /// Determines if NeoForge is the recommended platform for a Minecraft version.
    /// NeoForge is recommended for MC 1.20.2 and later.
    /// </summary>
    private static bool IsNeoForgeRecommended(string minecraftVersion)
    {
        var parts = minecraftVersion.Split('.');
        if (parts.Length < 2)
            return false;

        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
            return false;

        // MC 1.20.2+ should use NeoForge
        if (major > 1) return true;
        if (major == 1 && minor > 20) return true;
        if (major == 1 && minor == 20 && parts.Length > 2)
        {
            if (int.TryParse(parts[2], out var patch) && patch >= 2)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a Minecraft version uses modern Forge (with args files).
    /// Modern Forge started with MC 1.17.
    /// </summary>
    private static bool IsModernForge(string minecraftVersion)
    {
        var parts = minecraftVersion.Split('.');
        if (parts.Length < 2)
            return true; // Assume modern for unknown versions

        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
            return true;

        // MC 1.17+ uses modern Forge
        return major > 1 || (major == 1 && minor >= 17);
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
                _logger.LogDebug("[Forge] {Output}", e.Data);
                outputLines.Add(e.Data);
                totalLines++;

                // Forge installer typically outputs many lines during library downloads
                // Estimate ~200 lines for full installation
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
                // Forge installer writes info to stderr as well
                _logger.LogDebug("[Forge] {Error}", e.Data);
                outputLines.Add(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogError("Forge installer failed. Output:\n{Output}", string.Join("\n", outputLines.TakeLast(30)));
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Resolves "recommended" or "latest" to an actual Forge version number by querying the promotions API.
    /// </summary>
    /// <param name="minecraftVersion">The Minecraft version (e.g., "1.7.10").</param>
    /// <param name="versionType">Either "recommended" or "latest".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The actual Forge version number, or null if not found.</returns>
    private async Task<string?> ResolveForgeVersionAsync(
        string minecraftVersion,
        string versionType,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Resolving Forge {Type} version for MC {McVersion}", versionType, minecraftVersion);

            var response = await _httpClient.GetAsync(PromotionsUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var promotions = await response.Content.ReadFromJsonAsync<ForgePromotionsResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (promotions?.Promos is null || promotions.Promos.Count == 0)
            {
                _logger.LogWarning("Forge promotions API returned null or empty promos (Count={Count})",
                    promotions?.Promos?.Count ?? 0);
                return null;
            }

            _logger.LogDebug("Forge promotions API returned {Count} entries", promotions.Promos.Count);

            // Build the key to look up (e.g., "1.7.10-recommended" or "1.7.10-latest")
            var preferredKey = $"{minecraftVersion}-{versionType.ToLowerInvariant()}";
            var fallbackKey = versionType.Equals("recommended", StringComparison.OrdinalIgnoreCase)
                ? $"{minecraftVersion}-latest"
                : $"{minecraftVersion}-recommended";

            // Try the preferred version type first
            if (promotions.Promos.TryGetValue(preferredKey, out var version))
            {
                _logger.LogDebug("Found Forge version via {Key}: {Version}", preferredKey, version);
                return version;
            }

            // Fall back to the other version type
            if (promotions.Promos.TryGetValue(fallbackKey, out version))
            {
                _logger.LogDebug("Found Forge version via fallback {Key}: {Version}", fallbackKey, version);
                return version;
            }

            _logger.LogWarning("No Forge version found for MC {McVersion}. Available keys: {Keys}",
                minecraftVersion,
                string.Join(", ", promotions.Promos.Keys.Where(k => k.StartsWith(minecraftVersion)).Take(5)));

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch Forge promotions from API");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error resolving Forge version");
            return null;
        }
    }

    /// <summary>
    /// Response from the Forge promotions API.
    /// </summary>
    private sealed class ForgePromotionsResponse
    {
        [JsonPropertyName("promos")]
        public Dictionary<string, string> Promos { get; set; } = new();
    }
}
