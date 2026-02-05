using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.Steam;

/// <summary>
/// Service for managing SteamCMD operations.
/// Handles automatic download, installation, and game server management via SteamCMD.
/// </summary>
public partial class SteamCmdService : ISteamCmdService
{
    private const string SteamCmdWindowsUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
    private const string SteamCmdLinuxUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz";

    private readonly ILogger<SteamCmdService> _logger;
    private readonly string _steamCmdDirectory;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _installLock = new(1, 1);

    private string? _steamCmdPath;

    public SteamCmdService(ILogger<SteamCmdService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("SteamCMD");

        // Default SteamCMD location - can be made configurable via options
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _steamCmdDirectory = Path.Combine(basePath, "NebulaPanel", "SteamCMD");

        // Check if SteamCMD is already installed
        var expectedPath = GetSteamCmdExecutablePath();
        if (File.Exists(expectedPath))
        {
            _steamCmdPath = expectedPath;
        }
    }

    public bool IsInstalled => _steamCmdPath is not null && File.Exists(_steamCmdPath);

    public string? SteamCmdPath => _steamCmdPath;

    public async Task<bool> EnsureInstalledAsync(
        IProgress<SteamCmdProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsInstalled)
        {
            _logger.LogDebug("SteamCMD already installed at {Path}", _steamCmdPath);
            return true;
        }

        await _installLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (IsInstalled) return true;

            _logger.LogInformation("Installing SteamCMD to {Directory}", _steamCmdDirectory);
            progress?.Report(SteamCmdProgress.Downloading("SteamCMD"));

            // Create directory
            Directory.CreateDirectory(_steamCmdDirectory);

            // Determine platform and download URL
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var downloadUrl = isWindows ? SteamCmdWindowsUrl : SteamCmdLinuxUrl;
            var archiveExtension = isWindows ? ".zip" : ".tar.gz";
            var archivePath = Path.Combine(_steamCmdDirectory, $"steamcmd{archiveExtension}");

            // Download SteamCMD
            await DownloadFileAsync(downloadUrl, archivePath, progress, cancellationToken).ConfigureAwait(false);

            // Extract
            progress?.Report(SteamCmdProgress.Extracting());
            _logger.LogDebug("Extracting SteamCMD archive");

            if (isWindows)
            {
                ZipFile.ExtractToDirectory(archivePath, _steamCmdDirectory, overwriteFiles: true);
            }
            else
            {
                await ExtractTarGzAsync(archivePath, _steamCmdDirectory, cancellationToken).ConfigureAwait(false);
            }

            // Clean up archive
            File.Delete(archivePath);

            // Set executable path
            _steamCmdPath = GetSteamCmdExecutablePath();

            if (!File.Exists(_steamCmdPath))
            {
                _logger.LogError("SteamCMD executable not found at expected path: {Path}", _steamCmdPath);
                progress?.Report(SteamCmdProgress.Failed("SteamCMD executable not found after extraction"));
                return false;
            }

            // On Linux, make executable
            if (!isWindows)
            {
                await SetExecutablePermissionAsync(_steamCmdPath, cancellationToken).ConfigureAwait(false);
            }

            // Initialize SteamCMD (first run downloads additional files)
            progress?.Report(SteamCmdProgress.Initializing());
            _logger.LogInformation("Initializing SteamCMD (first run)");

            var initResult = await RunSteamCmdAsync("+quit", null, cancellationToken).ConfigureAwait(false);
            if (!initResult)
            {
                _logger.LogWarning("SteamCMD initialization returned non-zero exit code, but this may be normal for first run");
            }

            _logger.LogInformation("SteamCMD installed successfully at {Path}", _steamCmdPath);
            progress?.Report(SteamCmdProgress.Completed());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install SteamCMD");
            progress?.Report(SteamCmdProgress.Failed($"Installation failed: {ex.Message}"));
            return false;
        }
        finally
        {
            _installLock.Release();
        }
    }

    public async Task<bool> InstallOrUpdateGameAsync(
        string appId,
        string installDir,
        string? branch = null,
        string? betaPassword = null,
        bool validate = true,
        IProgress<SteamCmdProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!await EnsureInstalledAsync(progress, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        _logger.LogInformation("Installing/updating Steam app {AppId} to {InstallDir}", appId, installDir);
        progress?.Report(SteamCmdProgress.Starting());

        // Build the SteamCMD arguments
        var arguments = BuildInstallArguments(appId, installDir, branch, betaPassword, validate);

        try
        {
            // Create install directory if it doesn't exist
            Directory.CreateDirectory(installDir);

            progress?.Report(SteamCmdProgress.LoggingIn());
            var success = await RunSteamCmdWithProgressAsync(arguments, progress, cancellationToken).ConfigureAwait(false);

            if (success)
            {
                _logger.LogInformation("Successfully installed/updated Steam app {AppId}", appId);
                progress?.Report(SteamCmdProgress.Completed());
            }
            else
            {
                _logger.LogError("Failed to install/update Steam app {AppId}", appId);
                progress?.Report(SteamCmdProgress.Failed($"Installation of app {appId} failed"));
            }

            return success;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Installation of Steam app {AppId} was cancelled", appId);
            progress?.Report(SteamCmdProgress.Failed("Installation cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing/updating Steam app {AppId}", appId);
            progress?.Report(SteamCmdProgress.Failed($"Installation error: {ex.Message}"));
            return false;
        }
    }

    public async Task<SteamAppInfo?> GetAppInfoAsync(
        string appId,
        string installDir,
        CancellationToken cancellationToken = default)
    {
        // Check for appmanifest file
        var manifestPath = Path.Combine(installDir, "..", "steamapps", $"appmanifest_{appId}.acf");
        if (!File.Exists(manifestPath))
        {
            // Try alternate location
            manifestPath = Path.Combine(installDir, "steamapps", $"appmanifest_{appId}.acf");
        }

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            return ParseAppManifest(appId, installDir, content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read app manifest for {AppId}", appId);
            return null;
        }
    }

    public async Task<WorkshopDownloadResult> DownloadWorkshopItemAsync(
        string appId,
        string workshopItemId,
        string installDir,
        IProgress<SteamCmdProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!await EnsureInstalledAsync(progress, cancellationToken).ConfigureAwait(false))
        {
            return new WorkshopDownloadResult(false, null, "SteamCMD is not installed");
        }

        _logger.LogInformation(
            "Downloading Workshop item {WorkshopItemId} for app {AppId} to {InstallDir}",
            workshopItemId, appId, installDir);

        progress?.Report(SteamCmdProgress.Starting());

        // Build the SteamCMD arguments for Workshop download
        // +login anonymous +force_install_dir <path> +workshop_download_item <appid> <workshopid> +quit
        var arguments = new StringBuilder();
        arguments.Append("+force_install_dir \"").Append(installDir).Append("\" ");
        arguments.Append("+login anonymous ");
        arguments.Append("+workshop_download_item ").Append(appId).Append(' ').Append(workshopItemId);
        arguments.Append(" +quit");

        try
        {
            // Create install directory if it doesn't exist
            Directory.CreateDirectory(installDir);

            progress?.Report(SteamCmdProgress.LoggingIn());
            var success = await RunSteamCmdWithProgressAsync(arguments.ToString(), progress, cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                _logger.LogError(
                    "Failed to download Workshop item {WorkshopItemId} for app {AppId}",
                    workshopItemId, appId);
                return new WorkshopDownloadResult(false, null, $"Download of Workshop item {workshopItemId} failed");
            }

            // Workshop items are downloaded to: <installDir>/steamapps/workshop/content/<appid>/<workshopid>/
            var workshopPath = Path.Combine(installDir, "steamapps", "workshop", "content", appId, workshopItemId);

            _logger.LogInformation(
                "Successfully downloaded Workshop item {WorkshopItemId} to {Path}",
                workshopItemId, workshopPath);

            progress?.Report(SteamCmdProgress.Completed());
            return new WorkshopDownloadResult(true, workshopPath, null);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Download of Workshop item {WorkshopItemId} was cancelled", workshopItemId);
            progress?.Report(SteamCmdProgress.Failed("Download cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading Workshop item {WorkshopItemId}", workshopItemId);
            progress?.Report(SteamCmdProgress.Failed($"Download error: {ex.Message}"));
            return new WorkshopDownloadResult(false, null, $"Download error: {ex.Message}");
        }
    }

    private string BuildInstallArguments(string appId, string installDir, string? branch, string? betaPassword, bool validate)
    {
        var sb = new StringBuilder();

        // Force install directory (must come before login)
        sb.Append("+force_install_dir \"").Append(installDir).Append("\" ");

        // Anonymous login (works for most dedicated servers)
        sb.Append("+login anonymous ");

        // App update command
        sb.Append("+app_update ").Append(appId);

        // Beta branch selection
        if (!string.IsNullOrWhiteSpace(branch))
        {
            sb.Append(" -beta ").Append(branch);
            if (!string.IsNullOrWhiteSpace(betaPassword))
            {
                sb.Append(" -betapassword ").Append(betaPassword);
            }
        }

        // Validate files
        if (validate)
        {
            sb.Append(" validate");
        }

        // Quit when done
        sb.Append(" +quit");

        return sb.ToString();
    }

    private async Task<bool> RunSteamCmdAsync(string arguments, IProgress<SteamCmdProgress>? progress, CancellationToken cancellationToken)
    {
        if (_steamCmdPath is null)
        {
            throw new InvalidOperationException("SteamCMD is not installed");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _steamCmdPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _steamCmdDirectory
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Read output asynchronously
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("SteamCMD stderr: {Error}", error);
        }

        _logger.LogDebug("SteamCMD output: {Output}", output);
        return process.ExitCode == 0;
    }

    private async Task<bool> RunSteamCmdWithProgressAsync(string arguments, IProgress<SteamCmdProgress>? progress, CancellationToken cancellationToken)
    {
        if (_steamCmdPath is null)
        {
            throw new InvalidOperationException("SteamCMD is not installed");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _steamCmdPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _steamCmdDirectory,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.Start();

        // Read output line by line to parse progress
        var readOutputTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                outputBuilder.AppendLine(line);
                ParseAndReportProgress(line, progress);
            }
        }, cancellationToken);

        var readErrorTask = Task.Run(async () =>
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            errorBuilder.Append(error);
        }, cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(readOutputTask, readErrorTask).ConfigureAwait(false);

        var error = errorBuilder.ToString();
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("SteamCMD stderr: {Error}", error);
        }

        _logger.LogDebug("SteamCMD completed with exit code {ExitCode}", process.ExitCode);

        // SteamCMD may return non-zero for various reasons that aren't failures
        // Check output for actual errors
        var output = outputBuilder.ToString();
        if (output.Contains("Success!", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("fully installed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return process.ExitCode == 0;
    }

    private void ParseAndReportProgress(string line, IProgress<SteamCmdProgress>? progress)
    {
        if (progress is null) return;

        _logger.LogTrace("SteamCMD: {Line}", line);

        // Parse various SteamCMD output patterns
        // Example: "Update state (0x61) downloading, progress: 45.23 (1234567890 / 2345678901)"
        var downloadMatch = DownloadProgressRegex().Match(line);
        if (downloadMatch.Success)
        {
            var percent = double.Parse(downloadMatch.Groups[1].Value);
            var downloaded = long.Parse(downloadMatch.Groups[2].Value);
            var total = long.Parse(downloadMatch.Groups[3].Value);
            progress.Report(SteamCmdProgress.DownloadingGame(percent, downloaded, total));
            return;
        }

        // Example: "Update state (0x5) verifying install, progress: 23.45 (...)"
        if (line.Contains("verifying install", StringComparison.OrdinalIgnoreCase))
        {
            progress.Report(SteamCmdProgress.Validating());
            return;
        }

        // Example: "Update state (0x81) committing, progress: ..."
        if (line.Contains("committing", StringComparison.OrdinalIgnoreCase))
        {
            progress.Report(SteamCmdProgress.Installing());
            return;
        }

        // Logging in
        if (line.Contains("Logging in user", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Connecting anonymously", StringComparison.OrdinalIgnoreCase))
        {
            progress.Report(SteamCmdProgress.LoggingIn());
            return;
        }

        // Success
        if (line.Contains("Success!", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("fully installed", StringComparison.OrdinalIgnoreCase))
        {
            progress.Report(SteamCmdProgress.Completed());
            return;
        }

        // Error patterns
        if (line.Contains("ERROR!", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("FAILED", StringComparison.OrdinalIgnoreCase))
        {
            progress.Report(SteamCmdProgress.Failed(line));
        }
    }

    [GeneratedRegex(@"progress:\s*([\d.]+)\s*\((\d+)\s*/\s*(\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadProgressRegex();

    private async Task DownloadFileAsync(string url, string destinationPath, IProgress<SteamCmdProgress>? progress, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Downloading {Url} to {Path}", url, destinationPath);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var downloadedBytes = 0L;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            downloadedBytes += bytesRead;

            if (totalBytes > 0)
            {
                var percent = downloadedBytes * 100.0 / totalBytes;
                progress?.Report(SteamCmdProgress.DownloadingGame(percent, downloadedBytes, totalBytes));
            }
        }
    }

    private string GetSteamCmdExecutablePath()
    {
        var executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "steamcmd.exe" : "steamcmd.sh";
        return Path.Combine(_steamCmdDirectory, executable);
    }

    private static async Task ExtractTarGzAsync(string archivePath, string destinationDir, CancellationToken cancellationToken)
    {
        // Use tar command on Linux
        var startInfo = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-xzf \"{archivePath}\" -C \"{destinationDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is not null)
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException($"Failed to extract tar.gz: {error}");
            }
        }
    }

    private static async Task SetExecutablePermissionAsync(string filePath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"+x \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is not null)
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private SteamAppInfo? ParseAppManifest(string appId, string installDir, string content)
    {
        // Simple VDF parser for app manifest
        var nameMatch = Regex.Match(content, @"""name""\s*""([^""]+)""");
        var buildIdMatch = Regex.Match(content, @"""buildid""\s*""(\d+)""");
        var sizeMatch = Regex.Match(content, @"""SizeOnDisk""\s*""(\d+)""");
        var lastUpdatedMatch = Regex.Match(content, @"""LastUpdated""\s*""(\d+)""");
        var branchMatch = Regex.Match(content, @"""betakey""\s*""([^""]+)""");

        DateTime? lastUpdated = null;
        if (lastUpdatedMatch.Success && long.TryParse(lastUpdatedMatch.Groups[1].Value, out var timestamp))
        {
            lastUpdated = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
        }

        return new SteamAppInfo
        {
            AppId = appId,
            Name = nameMatch.Success ? nameMatch.Groups[1].Value : string.Empty,
            InstallDir = installDir,
            BuildId = buildIdMatch.Success ? buildIdMatch.Groups[1].Value : null,
            SizeOnDisk = sizeMatch.Success && long.TryParse(sizeMatch.Groups[1].Value, out var size) ? size : 0,
            LastUpdated = lastUpdated,
            Branch = branchMatch.Success ? branchMatch.Groups[1].Value : null
        };
    }
}
