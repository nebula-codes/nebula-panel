using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.OfficialGames.Hytale;

/// <summary>
/// Service for managing the hytale-downloader CLI tool and downloading Hytale server files.
/// </summary>
public partial class HytaleDownloaderService : IHytaleDownloaderService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HytaleDownloaderService> _logger;
    private readonly SemaphoreSlim _installLock = new(1, 1);

    private string? _downloaderPath;
    private readonly string _toolDirectory;
    private readonly string _credentialsFilePath;
    private readonly string _cacheDirectory;

    private const string DownloaderUrl = "https://downloader.hytale.com/hytale-downloader.zip";
    private const string DefaultCredentialsFileName = ".hytale-downloader-credentials.json";

    public HytaleDownloaderService(
        IHttpClientFactory httpClientFactory,
        ILogger<HytaleDownloaderService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _toolDirectory = Path.Combine(AppContext.BaseDirectory, "tools", "hytale");
        _credentialsFilePath = Path.Combine(_toolDirectory, DefaultCredentialsFileName);
        _cacheDirectory = Path.Combine(_toolDirectory, "cache");

        // Check if already installed
        var expectedPath = GetDownloaderExecutablePath();
        if (File.Exists(expectedPath))
        {
            _downloaderPath = expectedPath;
        }
    }

    /// <inheritdoc />
    public bool IsInstalled => _downloaderPath is not null && File.Exists(_downloaderPath);

    /// <inheritdoc />
    public string? DownloaderPath => _downloaderPath;

    /// <inheritdoc />
    public async Task<bool> EnsureInstalledAsync(CancellationToken ct = default)
    {
        if (IsInstalled)
        {
            return true;
        }

        await _installLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (IsInstalled)
            {
                return true;
            }

            _logger.LogInformation("Installing hytale-downloader to {Directory}", _toolDirectory);

            Directory.CreateDirectory(_toolDirectory);

            var zipPath = Path.Combine(_toolDirectory, "hytale-downloader.zip");

            // Download the ZIP file
            var httpClient = _httpClientFactory.CreateClient("HytaleDownloader");

            await using (var responseStream = await httpClient.GetStreamAsync(DownloaderUrl, ct).ConfigureAwait(false))
            await using (var fileStream = File.Create(zipPath))
            {
                await responseStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
            }

            _logger.LogDebug("Downloaded hytale-downloader.zip, extracting...");

            // Extract
            ZipFile.ExtractToDirectory(zipPath, _toolDirectory, overwriteFiles: true);

            // Clean up ZIP
            File.Delete(zipPath);

            var executablePath = GetDownloaderExecutablePath();

            // Make executable on Linux/macOS
            if (!OperatingSystem.IsWindows() && File.Exists(executablePath))
            {
                await SetExecutablePermissionAsync(executablePath, ct).ConfigureAwait(false);
            }

            if (!File.Exists(executablePath))
            {
                _logger.LogError("hytale-downloader executable not found at {Path} after extraction", executablePath);
                return false;
            }

            _downloaderPath = executablePath;
            _logger.LogInformation("hytale-downloader installed successfully at {Path}", _downloaderPath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install hytale-downloader");
            return false;
        }
        finally
        {
            _installLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string> GetDownloaderVersionAsync(CancellationToken ct = default)
    {
        await EnsureInstalledAsync(ct).ConfigureAwait(false);

        var result = await RunDownloaderAsync("--version", ct).ConfigureAwait(false);

        // Parse version from output
        var versionMatch = VersionRegex().Match(result.Output);
        return versionMatch.Success ? versionMatch.Groups[1].Value.Trim() : "unknown";
    }

    /// <inheritdoc />
    public async Task<HytaleUpdateCheckResult> CheckDownloaderUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var currentVersion = await GetDownloaderVersionAsync(ct).ConfigureAwait(false);

            // Run the downloader with update check (it will report if an update is available)
            var result = await RunDownloaderAsync("-print-version", ct).ConfigureAwait(false);

            // Look for update available message in output
            var updateAvailable = result.Output.Contains("update available", StringComparison.OrdinalIgnoreCase);
            var latestVersionMatch = LatestVersionRegex().Match(result.Output);

            return new HytaleUpdateCheckResult
            {
                UpdateAvailable = updateAvailable,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersionMatch.Success ? latestVersionMatch.Groups[1].Value : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for hytale-downloader updates");
            return new HytaleUpdateCheckResult
            {
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateDownloaderAsync(CancellationToken ct = default)
    {
        await _installLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Updating hytale-downloader");

            // Delete existing binary
            if (_downloaderPath is not null && File.Exists(_downloaderPath))
            {
                File.Delete(_downloaderPath);
            }
            _downloaderPath = null;

            // Re-install
            return await EnsureInstalledAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _installLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<HytaleVersionInfo?> GetServerVersionAsync(
        string patchline = "release",
        HytaleCredentials? credentials = null,
        CancellationToken ct = default)
    {
        await EnsureInstalledAsync(ct).ConfigureAwait(false);

        // Write credentials if provided
        if (credentials is not null)
        {
            _logger.LogInformation(
                "Writing credentials for version check - AccessToken starts with: {TokenPrefix}, ExpiresAt: {ExpiresAt}",
                credentials.AccessToken?.Substring(0, Math.Min(10, credentials.AccessToken?.Length ?? 0)) ?? "null",
                credentials.ExpiresAt);

            await WriteCredentialsAsync(credentials, ct).ConfigureAwait(false);

            // Verify the file was written
            if (File.Exists(_credentialsFilePath))
            {
                var fileContent = await File.ReadAllTextAsync(_credentialsFilePath, ct).ConfigureAwait(false);
                _logger.LogInformation("Credentials file written to {Path}, size: {Size} bytes",
                    _credentialsFilePath, fileContent.Length);
            }
            else
            {
                _logger.LogWarning("Credentials file was NOT created at {Path}", _credentialsFilePath);
            }
        }
        else
        {
            _logger.LogWarning("No credentials provided for version check");
        }

        var args = $"-print-version -patchline {patchline} -skip-update-check -credentials-path \"{_credentialsFilePath}\"";
        var result = await RunDownloaderAsync(args, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "GetServerVersionAsync for {Patchline} - ExitCode: {ExitCode}, Success: {Success}, Output: {Output}, Error: {Error}",
            patchline, result.ExitCode, result.Success, result.Output, result.Error);

        // Parse version from output
        // The CLI just outputs the version string directly, e.g. "2026.01.13-50e69c385"
        // But it might also have a "Version:" prefix in some cases
        var output = result.Output.Trim();
        string? version = null;

        var versionMatch = ServerVersionRegex().Match(output);
        if (versionMatch.Success)
        {
            version = versionMatch.Groups[1].Value.Trim();
        }
        else if (!string.IsNullOrEmpty(output) && result.Success)
        {
            // CLI outputs just the version string directly
            version = output.Split('\n')[0].Trim();
        }

        _logger.LogInformation("Parsed version for {Patchline}: {Version} (regex matched: {Matched})",
            patchline, version ?? "(null)", versionMatch.Success);

        // Return null if we couldn't get a valid version (API error, auth failure, etc.)
        if (string.IsNullOrEmpty(version))
        {
            _logger.LogWarning("Failed to get server version for patchline {Patchline}: {Error}",
                patchline, result.Error);
            return null;
        }

        return new HytaleVersionInfo
        {
            Version = version,
            Patchline = patchline,
            CheckedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<HytaleDownloadResult> DownloadServerAsync(
        string destinationPath,
        string patchline,
        HytaleCredentials credentials,
        IProgress<HytaleDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await EnsureInstalledAsync(ct).ConfigureAwait(false);
            await WriteCredentialsAsync(credentials, ct).ConfigureAwait(false);

            Directory.CreateDirectory(destinationPath);

            progress?.Report(HytaleDownloadProgress.Starting());

            // First, get the version we're about to download so we can check cache
            var versionInfo = await GetServerVersionAsync(patchline, credentials, ct).ConfigureAwait(false);
            if (versionInfo == null)
            {
                return new HytaleDownloadResult
                {
                    Success = false,
                    Error = "Failed to retrieve version information from Hytale API. Please check your credentials.",
                    Duration = TimeSpan.Zero
                };
            }
            var version = versionInfo.Version;

            _logger.LogInformation("Hytale server version for patchline {Patchline}: {Version}", patchline, version);

            // Check if we have this version cached
            var cachePath = GetCachePath(patchline, version);
            if (File.Exists(cachePath))
            {
                _logger.LogInformation("Found cached Hytale server at {CachePath}, extracting to {Destination}",
                    cachePath, destinationPath);

                progress?.Report(HytaleDownloadProgress.Extracting());

                try
                {
                    var cachedZipSize = new FileInfo(cachePath).Length;
                    ZipFile.ExtractToDirectory(cachePath, destinationPath, overwriteFiles: true);

                    progress?.Report(HytaleDownloadProgress.Complete());

                    return new HytaleDownloadResult
                    {
                        Success = true,
                        Version = version,
                        DownloadedFilePath = destinationPath,
                        FileSizeBytes = cachedZipSize,
                        Duration = DateTime.UtcNow - startTime
                    };
                }
                catch (InvalidDataException ex)
                {
                    _logger.LogWarning(ex, "Cached zip at {CachePath} is corrupt, deleting and re-downloading", cachePath);
                    File.Delete(cachePath);
                }
            }

            // Not cached - download to cache directory
            _logger.LogInformation("Version {Version} not in cache, downloading to {CachePath}", version, cachePath);

            // Ensure cache directory exists
            var cacheDir = Path.GetDirectoryName(cachePath);
            if (cacheDir is not null)
            {
                Directory.CreateDirectory(cacheDir);
            }

            var args = $"-download-path \"{cachePath}\" -patchline {patchline} -skip-update-check -credentials-path \"{_credentialsFilePath}\"";

            // Run with progress parsing
            var result = await RunDownloaderWithProgressAsync(args, progress, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                // Clean up partial download
                if (File.Exists(cachePath))
                {
                    File.Delete(cachePath);
                }

                // Check if CLI updated credentials even on failure (e.g., token refresh)
                var updatedCreds = await ReadCredentialsAsync(ct).ConfigureAwait(false);
                if (updatedCreds != null)
                {
                    _logger.LogInformation(
                        "Download failed but credentials file exists - ExpiresAt: {ExpiresAt}, IsValid: {IsValid}",
                        updatedCreds.ExpiresAt,
                        updatedCreds.IsValid);
                }

                // Provide more specific error messages for common issues
                var errorMessage = result.Error ?? "Download failed";
                if (errorMessage.Contains("403", StringComparison.OrdinalIgnoreCase) ||
                    errorMessage.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Download failed with 403 Forbidden. CLI output: {Output}, Error: {Error}",
                        result.Output,
                        result.Error);
                    errorMessage = "Access denied (403 Forbidden). Please try re-authenticating your Hytale account. " +
                                   "If the issue persists, verify your account has server download permissions on hytale.com.";
                }

                return new HytaleDownloadResult
                {
                    Success = false,
                    Error = errorMessage,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Extract from cache to destination
            progress?.Report(HytaleDownloadProgress.Extracting());

            if (File.Exists(cachePath))
            {
                var zipSize = new FileInfo(cachePath).Length;
                ZipFile.ExtractToDirectory(cachePath, destinationPath, overwriteFiles: true);

                progress?.Report(HytaleDownloadProgress.Complete());

                // Read back potentially refreshed credentials
                await ReadCredentialsAsync(ct).ConfigureAwait(false);

                _logger.LogInformation("Hytale server downloaded and cached at {CachePath} ({Size} bytes)",
                    cachePath, zipSize);

                return new HytaleDownloadResult
                {
                    Success = true,
                    Version = version,
                    DownloadedFilePath = destinationPath,
                    FileSizeBytes = zipSize,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            return new HytaleDownloadResult
            {
                Success = false,
                Error = "Downloaded file not found",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download Hytale server");
            return new HytaleDownloadResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <inheritdoc />
    public async Task WriteCredentialsAsync(HytaleCredentials credentials, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_toolDirectory);

        // Write expires_at as Unix timestamp (seconds since epoch) - this is what most OAuth CLIs expect
        // IMPORTANT: Ensure we treat ExpiresAt as UTC to avoid timezone issues
        // If Kind is Unspecified (common from EF Core), assume it's UTC
        var expiresAtUtc = credentials.ExpiresAt.Kind == DateTimeKind.Utc
            ? credentials.ExpiresAt
            : DateTime.SpecifyKind(credentials.ExpiresAt, DateTimeKind.Utc);
        var expiresAtUnix = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds();

        _logger.LogDebug(
            "WriteCredentialsAsync - ExpiresAt: {ExpiresAt} (Kind: {Kind}), Interpreted as UTC: {UtcTime}, Unix: {Unix}",
            credentials.ExpiresAt,
            credentials.ExpiresAt.Kind,
            expiresAtUtc,
            expiresAtUnix);

        var credentialsJson = new
        {
            access_token = credentials.AccessToken,
            refresh_token = credentials.RefreshToken,
            expires_at = expiresAtUnix,
            token_type = credentials.TokenType
        };

        var json = JsonSerializer.Serialize(credentialsJson, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_credentialsFilePath, json, ct).ConfigureAwait(false);

        // Log redacted credential info for debugging
        var accessTokenPreview = credentials.AccessToken?.Length > 20
            ? credentials.AccessToken[..20] + "..."
            : credentials.AccessToken ?? "null";
        _logger.LogInformation(
            "Wrote Hytale credentials to {Path} - AccessToken: {TokenPreview}, ExpiresAt: {ExpiresAt} (Unix: {Unix})",
            _credentialsFilePath,
            accessTokenPreview,
            expiresAtUtc,
            expiresAtUnix);
    }

    /// <inheritdoc />
    public async Task<HytaleCredentials?> ReadCredentialsAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_credentialsFilePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_credentialsFilePath, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            var tokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer";

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return null;
            }

            // Handle expires_at as either a Unix timestamp (number) or ISO date string
            DateTime expiresAt;
            if (root.TryGetProperty("expires_at", out var ea))
            {
                if (ea.ValueKind == JsonValueKind.Number)
                {
                    // Unix timestamp (seconds since epoch)
                    var unixTime = ea.GetInt64();
                    expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
                }
                else if (ea.ValueKind == JsonValueKind.String && DateTime.TryParse(ea.GetString(), out var parsed))
                {
                    expiresAt = parsed.ToUniversalTime();
                }
                else
                {
                    expiresAt = DateTime.UtcNow.AddHours(1);
                }
            }
            else
            {
                expiresAt = DateTime.UtcNow.AddHours(1);
            }

            return new HytaleCredentials
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                TokenType = tokenType ?? "Bearer"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read Hytale credentials from {Path}", _credentialsFilePath);
            return null;
        }
    }

    #region Private Methods

    /// <summary>
    /// Gets the cache file path for a specific patchline and version.
    /// </summary>
    private string GetCachePath(string patchline, string version)
    {
        // Sanitize version string to be a valid filename
        var safeVersion = version.Replace(':', '-').Replace('/', '-').Replace('\\', '-');
        return Path.Combine(_cacheDirectory, patchline, $"{safeVersion}.zip");
    }

    private string GetDownloaderExecutablePath()
    {
        var binaryName = OperatingSystem.IsWindows()
            ? "hytale-downloader-windows-amd64.exe"
            : "hytale-downloader-linux-amd64";

        return Path.Combine(_toolDirectory, binaryName);
    }

    private async Task<DownloaderResult> RunDownloaderAsync(string arguments, CancellationToken ct, TimeSpan? timeout = null)
    {
        if (_downloaderPath is null)
        {
            throw new InvalidOperationException("hytale-downloader is not installed");
        }

        timeout ??= TimeSpan.FromSeconds(30);

        var startInfo = new ProcessStartInfo
        {
            FileName = _downloaderPath,
            Arguments = arguments,
            WorkingDirectory = _toolDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,  // Redirect stdin to prevent hanging
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _logger.LogInformation("Running hytale-downloader with args: {Arguments}", arguments);

        using var process = new Process { StartInfo = startInfo };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout.Value);

        try
        {
            process.Start();

            // Close stdin immediately to signal we won't send any input
            process.StandardInput.Close();

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);

            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);

            _logger.LogDebug("hytale-downloader output: {Output}", output);
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogDebug("hytale-downloader stderr: {Error}", error);
            }

            return new DownloaderResult
            {
                ExitCode = process.ExitCode,
                Output = output,
                Error = error,
                Success = process.ExitCode == 0
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("hytale-downloader timed out after {Timeout}", timeout);
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { /* Ignore kill errors */ }

            return new DownloaderResult
            {
                ExitCode = -1,
                Output = string.Empty,
                Error = $"Process timed out after {timeout.Value.TotalSeconds} seconds",
                Success = false
            };
        }
    }

    private async Task<DownloaderResult> RunDownloaderWithProgressAsync(
        string arguments,
        IProgress<HytaleDownloadProgress>? progress,
        CancellationToken ct)
    {
        if (_downloaderPath is null)
        {
            throw new InvalidOperationException("hytale-downloader is not installed");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _downloaderPath,
            Arguments = arguments,
            WorkingDirectory = _toolDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,  // Redirect stdin to prevent hanging
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.Start();

        // Close stdin immediately to signal we won't send any input
        process.StandardInput.Close();

        // Read output line by line to parse progress
        var readOutputTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                outputBuilder.AppendLine(line);
                _logger.LogInformation("hytale-downloader stdout: {Line}", line);

                // Parse progress from output - try multiple patterns
                // Pattern 1: "progress: 50%" or "progress: 50 (500/1000)"
                var progressMatch = DownloadProgressRegex().Match(line);
                // Pattern 2: "50%" or "50.5%" anywhere in line
                var percentMatch = PercentageRegex().Match(line);
                // Pattern 3: "downloading" or "downloaded X of Y" style
                var downloadingMatch = DownloadingStatusRegex().Match(line);

                if (progressMatch.Success && double.TryParse(progressMatch.Groups[1].Value, out var percent))
                {
                    long.TryParse(progressMatch.Groups[2].Value, out var downloaded);
                    long.TryParse(progressMatch.Groups[3].Value, out var total);
                    progress?.Report(HytaleDownloadProgress.Downloading(percent, downloaded, total));
                }
                else if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out var pct))
                {
                    progress?.Report(HytaleDownloadProgress.Downloading(pct, 0, 0));
                }
                else if (downloadingMatch.Success)
                {
                    // If we see "downloading" without a percentage, report indeterminate progress
                    progress?.Report(HytaleDownloadProgress.Downloading(50, 0, 0));
                }
            }
        }, ct);

        var readErrorTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                errorBuilder.AppendLine(line);
                _logger.LogInformation("hytale-downloader stderr: {Line}", line);

                // Some CLIs output progress to stderr, so try to parse it too
                var percentMatch = PercentageRegex().Match(line);
                if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out var pct))
                {
                    progress?.Report(HytaleDownloadProgress.Downloading(pct, 0, 0));
                }
            }
        }, ct);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        await Task.WhenAll(readOutputTask, readErrorTask).ConfigureAwait(false);

        var success = process.ExitCode == 0 ||
                      outputBuilder.ToString().Contains("Success", StringComparison.OrdinalIgnoreCase);

        return new DownloaderResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString(),
            Success = success
        };
    }

    private static async Task SetExecutablePermissionAsync(string filePath, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"+x \"{filePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is not null)
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
    }

    private record DownloaderResult
    {
        public int ExitCode { get; init; }
        public string Output { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
        public bool Success { get; init; }
    }

    #endregion

    #region Regex Patterns

    [GeneratedRegex(@"version[:\s]+(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"latest[:\s]+(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex LatestVersionRegex();

    [GeneratedRegex(@"Version[:\s]+(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ServerVersionRegex();

    [GeneratedRegex(@"progress[:\s]*([\d.]+)[%\s]*(?:\((\d+)\s*/\s*(\d+)\))?", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadProgressRegex();

    // Match standalone percentage like "50%" or "50.5%" (must be at word boundary)
    [GeneratedRegex(@"\b([\d.]+)%")]
    private static partial Regex PercentageRegex();

    // Match downloading status messages
    [GeneratedRegex(@"download(?:ing|ed)", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadingStatusRegex();

    #endregion
}
