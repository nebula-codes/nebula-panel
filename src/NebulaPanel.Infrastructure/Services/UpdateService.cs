using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.Configuration;
using NebulaPanel.Shared;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Service for checking, downloading, and applying panel updates.
/// </summary>
public class UpdateService : IUpdateService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpdateConfiguration _config;
    private readonly IUpdateNotifier _notifier;
    private readonly IGameServerService? _gameServerService;
    private readonly string _updateDir;
    private readonly string _backupDir;

    private UpdateStatus _status = UpdateStatus.Idle;
    private readonly object _statusLock = new();
    private readonly ConcurrentDictionary<string, string> _checksumCache = new();

    /// <inheritdoc />
    public VersionInfo CurrentVersion { get; }

    /// <inheritdoc />
    public UpdateStatus Status
    {
        get
        {
            lock (_statusLock)
            {
                return _status;
            }
        }
        private set
        {
            lock (_statusLock)
            {
                if (_status != value)
                {
                    _status = value;
                    StatusChanged?.Invoke(this, new UpdateStatusChangedEventArgs(value));
                }
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<UpdateStatusChangedEventArgs>? StatusChanged;

    public UpdateService(
        ILogger<UpdateService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<UpdateConfiguration> config,
        IUpdateNotifier notifier,
        IGameServerService? gameServerService = null)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _notifier = notifier;
        _gameServerService = gameServerService;

        _updateDir = _config.GetUpdateDirectory();
        _backupDir = _config.GetBackupDirectory();

        // Parse current version from assembly
        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

        // Handle version strings with +metadata (e.g., "1.0.0+abc123")
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
        {
            version = version[..plusIndex];
        }

        CurrentVersion = VersionInfo.Parse(version);

        Directory.CreateDirectory(_updateDir);
        Directory.CreateDirectory(_backupDir);

        _logger.LogDebug("Update service initialized. Current version: {Version}", CurrentVersion.FullVersion);
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            return new UpdateCheckResult
            {
                UpdateAvailable = false,
                CheckedAt = DateTime.UtcNow,
                Error = "Update checking is disabled"
            };
        }

        await SetStatusAsync(UpdateStatus.Checking, cancellationToken).ConfigureAwait(false);

        try
        {
            var client = _httpClientFactory.CreateClient("GitHub");
            var response = await client.GetAsync(_config.UpdateServerUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json, JsonOptions);

            if (releases is null || releases.Count == 0)
            {
                await SetStatusAsync(UpdateStatus.Idle, cancellationToken).ConfigureAwait(false);
                return new UpdateCheckResult
                {
                    UpdateAvailable = false,
                    CheckedAt = DateTime.UtcNow
                };
            }

            // Filter releases based on channel
            var eligibleReleases = releases
                .Where(r => _config.Channel != UpdateChannel.Stable || !r.Prerelease)
                .Where(r => _config.Channel == UpdateChannel.Development || !r.Draft)
                .ToList();

            var latest = eligibleReleases.FirstOrDefault();
            if (latest is null)
            {
                await SetStatusAsync(UpdateStatus.Idle, cancellationToken).ConfigureAwait(false);
                return new UpdateCheckResult
                {
                    UpdateAvailable = false,
                    CheckedAt = DateTime.UtcNow
                };
            }

            var latestVersion = VersionInfo.Parse(latest.TagName);
            var updateAvailable = latestVersion.IsNewerThan(CurrentVersion);

            var newStatus = updateAvailable ? UpdateStatus.UpdateAvailable : UpdateStatus.Idle;
            await SetStatusAsync(newStatus, cancellationToken).ConfigureAwait(false);

            var releaseInfo = await MapToReleaseInfoAsync(latest, cancellationToken).ConfigureAwait(false);
            var result = new UpdateCheckResult
            {
                UpdateAvailable = updateAvailable,
                LatestVersion = latestVersion,
                LatestRelease = releaseInfo,
                IsSecurityUpdate = latest.Body?.Contains("[SECURITY]", StringComparison.OrdinalIgnoreCase) ?? false,
                IsMajorUpdate = latestVersion.Major > CurrentVersion.Major,
                CheckedAt = DateTime.UtcNow
            };

            await _notifier.NotifyCheckCompletedAsync(result, cancellationToken).ConfigureAwait(false);

            if (updateAvailable && releaseInfo is not null)
            {
                await _notifier.NotifyUpdateAvailableAsync(releaseInfo, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Update available: {Version}", latestVersion.FullVersion);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            await SetStatusAsync(UpdateStatus.Idle, cancellationToken).ConfigureAwait(false);

            return new UpdateCheckResult
            {
                UpdateAvailable = false,
                CheckedAt = DateTime.UtcNow,
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<ReleaseInfo?> GetReleaseInfoAsync(string version, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GitHub");
            var response = await client.GetAsync(_config.UpdateServerUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json, JsonOptions);

            var release = releases?.FirstOrDefault(r =>
                r.TagName.TrimStart('v', 'V').Equals(version.TrimStart('v', 'V'), StringComparison.OrdinalIgnoreCase));

            if (release is null)
                return null;

            return await MapToReleaseInfoAsync(release, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get release info for version {Version}", version);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<UpdateDownloadResult> DownloadUpdateAsync(
        string version,
        IProgress<PanelUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await SetStatusAsync(UpdateStatus.Downloading, cancellationToken).ConfigureAwait(false);

        try
        {
            var release = await GetReleaseInfoAsync(version, cancellationToken).ConfigureAwait(false);
            if (release is null)
            {
                await SetStatusAsync(UpdateStatus.Failed, cancellationToken).ConfigureAwait(false);
                return UpdateDownloadResult.Failed("Release not found");
            }

            // Find appropriate asset for this platform
            var platform = GetCurrentPlatform();
            var asset = release.Assets.FirstOrDefault(a => a.Platform == platform)
                ?? release.Assets.FirstOrDefault(a => a.Name.Contains("linux-x64", StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                await SetStatusAsync(UpdateStatus.Failed, cancellationToken).ConfigureAwait(false);
                return UpdateDownloadResult.Failed("No compatible download found for this platform");
            }

            var extension = Path.GetExtension(asset.Name);
            if (string.IsNullOrEmpty(extension))
                extension = ".tar.gz";
            // Handle .tar.gz double extension
            if (asset.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                extension = ".tar.gz";
            var downloadPath = Path.Combine(_updateDir, $"{version}{extension}");

            // Report initial progress
            var initialProgress = PanelUpdateProgress.Preparing($"Starting download of {version}...");
            progress?.Report(initialProgress);
            await _notifier.NotifyProgressChangedAsync(initialProgress, cancellationToken).ConfigureAwait(false);

            // Download with progress
            var client = _httpClientFactory.CreateClient("GitHub");
            using var response = await client.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? asset.Size;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = File.Create(downloadPath);

            var buffer = new byte[81920];
            long bytesDownloaded = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                bytesDownloaded += read;

                var downloadProgress = PanelUpdateProgress.Downloading(bytesDownloaded, totalBytes);
                progress?.Report(downloadProgress);
                await _notifier.NotifyProgressChangedAsync(downloadProgress, cancellationToken).ConfigureAwait(false);
            }

            // Verify checksum if available
            if (!string.IsNullOrEmpty(asset.Sha256))
            {
                var verifyProgress = PanelUpdateProgress.Verifying();
                progress?.Report(verifyProgress);
                await _notifier.NotifyProgressChangedAsync(verifyProgress, cancellationToken).ConfigureAwait(false);

                fileStream.Position = 0;
                var hash = await ComputeSha256Async(fileStream, cancellationToken).ConfigureAwait(false);

                if (!hash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    fileStream.Close();
                    File.Delete(downloadPath);
                    await SetStatusAsync(UpdateStatus.Failed, cancellationToken).ConfigureAwait(false);
                    return UpdateDownloadResult.Failed("Checksum verification failed");
                }
            }

            await SetStatusAsync(UpdateStatus.ReadyToInstall, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully downloaded update {Version} to {Path}", version, downloadPath);

            return UpdateDownloadResult.Succeeded(downloadPath, version);
        }
        catch (OperationCanceledException)
        {
            await SetStatusAsync(UpdateStatus.Idle, cancellationToken).ConfigureAwait(false);
            return UpdateDownloadResult.Failed("Download was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download update {Version}", version);
            await SetStatusAsync(UpdateStatus.Failed, cancellationToken).ConfigureAwait(false);
            return UpdateDownloadResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ApplyUpdateAsync(
        string version,
        UpdateOptions options,
        CancellationToken cancellationToken = default)
    {
        // Find the downloaded archive (could be .tar.gz or .zip)
        var downloadPath = FindDownloadedArchive(version);
        if (downloadPath is null)
        {
            return Result.Failure("Update not downloaded. Please download the update first.");
        }

        try
        {
            // Create backup if requested
            if (options.CreateBackup)
            {
                await SetStatusAsync(UpdateStatus.CreatingBackup, cancellationToken).ConfigureAwait(false);
                var backupProgress = PanelUpdateProgress.CreatingBackup();
                await _notifier.NotifyProgressChangedAsync(backupProgress, cancellationToken).ConfigureAwait(false);

                await CreateUpdateBackupAsync(version, options, cancellationToken).ConfigureAwait(false);
            }

            // Stop running game servers if requested
            var restartServerIds = new List<Guid>();
            if (options.StopGameServers && _gameServerService is not null)
            {
                await SetStatusAsync(UpdateStatus.StoppingServers, cancellationToken).ConfigureAwait(false);
                var stoppingProgress = PanelUpdateProgress.StoppingServers();
                await _notifier.NotifyProgressChangedAsync(stoppingProgress, cancellationToken).ConfigureAwait(false);

                var servers = await _gameServerService.GetAllServersAsync(cancellationToken).ConfigureAwait(false);
                restartServerIds = servers
                    .Where(s => s.Status == ServerStatus.Running)
                    .Select(s => s.Id)
                    .ToList();

                foreach (var serverId in restartServerIds)
                {
                    _logger.LogInformation("Stopping server {ServerId} before update", serverId);
                    await _gameServerService.StopServerAsync(serverId, cancellationToken).ConfigureAwait(false);
                }
            }

            // Write update manifest for the updater process
            await SetStatusAsync(UpdateStatus.Installing, cancellationToken).ConfigureAwait(false);
            var installProgress = PanelUpdateProgress.ExtractingFiles();
            await _notifier.NotifyProgressChangedAsync(installProgress, cancellationToken).ConfigureAwait(false);

            var manifest = new UpdateManifest
            {
                Version = version,
                DownloadPath = downloadPath,
                BackupId = options.CreateBackup ? GetLatestBackupId() : null,
                StopGameServers = options.StopGameServers,
                RestartGameServers = options.RestartGameServers,
                RestartServerIds = restartServerIds,
                Timestamp = DateTime.UtcNow
            };

            var manifestPath = Path.Combine(_updateDir, "pending-update.json");
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest), cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Initiating update to version {Version}", version);

            // Give clients time to receive the final message
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

            // Request application shutdown - the updater will take over
            // Exit code 100 signals that an update is pending
            Environment.Exit(100);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply update {Version}", version);
            await SetStatusAsync(UpdateStatus.Failed, cancellationToken).ConfigureAwait(false);
            return Result.Failure(ex.Message);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UpdateBackup>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        var backups = new List<UpdateBackup>();

        if (!Directory.Exists(_backupDir))
        {
            return Task.FromResult<IReadOnlyList<UpdateBackup>>(backups);
        }

        foreach (var dir in Directory.GetDirectories(_backupDir))
        {
            var metadataPath = Path.Combine(dir, "backup.json");
            if (File.Exists(metadataPath))
            {
                try
                {
                    var json = File.ReadAllText(metadataPath);
                    var backup = JsonSerializer.Deserialize<UpdateBackup>(json, JsonOptions);
                    if (backup is not null)
                    {
                        backups.Add(backup);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read backup metadata from {Path}", metadataPath);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<UpdateBackup>>(backups.OrderByDescending(b => b.CreatedAt).ToList());
    }

    /// <inheritdoc />
    public async Task<Result> RestoreBackupAsync(string backupId, CancellationToken cancellationToken = default)
    {
        var backupPath = Path.Combine(_backupDir, backupId);
        if (!Directory.Exists(backupPath))
        {
            return Result.Failure("Backup not found");
        }

        var metadataPath = Path.Combine(backupPath, "backup.json");
        if (!File.Exists(metadataPath))
        {
            return Result.Failure("Backup metadata not found");
        }

        try
        {
            _logger.LogInformation("Starting restore from backup {BackupId}", backupId);

            // Stop running game servers
            var runningServerIds = new List<Guid>();
            if (_gameServerService is not null)
            {
                var servers = await _gameServerService.GetAllServersAsync(cancellationToken).ConfigureAwait(false);
                runningServerIds = servers
                    .Where(s => s.Status == ServerStatus.Running)
                    .Select(s => s.Id)
                    .ToList();

                foreach (var serverId in runningServerIds)
                {
                    _logger.LogInformation("Stopping server {ServerId} before restore", serverId);
                    await _gameServerService.StopServerAsync(serverId, cancellationToken).ConfigureAwait(false);
                }
            }

            var baseDir = AppContext.BaseDirectory;

            // Restore database
            var dbBackup = Path.Combine(backupPath, "nebula.db");
            var dbTarget = Path.Combine(baseDir, "data", "nebula.db");
            if (File.Exists(dbBackup))
            {
                File.Copy(dbBackup, dbTarget, overwrite: true);

                // Restore WAL and SHM if they exist
                var walBackup = Path.Combine(backupPath, "nebula.db-wal");
                var shmBackup = Path.Combine(backupPath, "nebula.db-shm");
                if (File.Exists(walBackup))
                    File.Copy(walBackup, dbTarget + "-wal", overwrite: true);
                if (File.Exists(shmBackup))
                    File.Copy(shmBackup, dbTarget + "-shm", overwrite: true);

                _logger.LogInformation("Restored database from backup");
            }

            // Restore config files
            var configDir = Path.Combine(baseDir, "data");
            foreach (var configFile in Directory.GetFiles(backupPath, "appsettings*.json"))
            {
                var targetPath = Path.Combine(configDir, Path.GetFileName(configFile));
                File.Copy(configFile, targetPath, overwrite: true);
                _logger.LogInformation("Restored config file: {FileName}", Path.GetFileName(configFile));
            }

            // Restart previously running servers
            if (_gameServerService is not null)
            {
                foreach (var serverId in runningServerIds)
                {
                    _logger.LogInformation("Restarting server {ServerId} after restore", serverId);
                    await _gameServerService.StartServerAsync(serverId, cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Restore from backup {BackupId} completed successfully", backupId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore from backup {BackupId}", backupId);
            return Result.Failure($"Restore failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<Result> DeleteBackupAsync(string backupId, CancellationToken cancellationToken = default)
    {
        var backupPath = Path.Combine(_backupDir, backupId);
        if (!Directory.Exists(backupPath))
        {
            return Task.FromResult(Result.Failure("Backup not found"));
        }

        try
        {
            Directory.Delete(backupPath, recursive: true);
            _logger.LogInformation("Deleted backup {BackupId}", backupId);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete backup {BackupId}", backupId);
            return Task.FromResult(Result.Failure($"Failed to delete backup: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Task<UpdateBackup?> GetBackupByIdAsync(string backupId, CancellationToken cancellationToken = default)
    {
        var backupPath = Path.Combine(_backupDir, backupId);
        var metadataPath = Path.Combine(backupPath, "backup.json");

        if (!File.Exists(metadataPath))
        {
            return Task.FromResult<UpdateBackup?>(null);
        }

        try
        {
            var json = File.ReadAllText(metadataPath);
            var backup = JsonSerializer.Deserialize<UpdateBackup>(json, JsonOptions);
            return Task.FromResult(backup);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read backup metadata from {Path}", metadataPath);
            return Task.FromResult<UpdateBackup?>(null);
        }
    }

    private async Task SetStatusAsync(UpdateStatus status, CancellationToken cancellationToken)
    {
        Status = status;
        await _notifier.NotifyStatusChangedAsync(status, cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateUpdateBackupAsync(string forVersion, UpdateOptions options, CancellationToken cancellationToken)
    {
        var backupId = $"pre-update-{forVersion}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var backupPath = Path.Combine(_backupDir, backupId);
        Directory.CreateDirectory(backupPath);

        var baseDir = AppContext.BaseDirectory;

        // Backup database
        var dbPath = Path.Combine(baseDir, "data", "nebula.db");
        if (File.Exists(dbPath))
        {
            File.Copy(dbPath, Path.Combine(backupPath, "nebula.db"));

            // Also copy WAL and SHM files if they exist
            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";
            if (File.Exists(walPath)) File.Copy(walPath, Path.Combine(backupPath, "nebula.db-wal"));
            if (File.Exists(shmPath)) File.Copy(shmPath, Path.Combine(backupPath, "nebula.db-shm"));
        }

        // Backup configuration
        var configDir = Path.Combine(baseDir, "data");
        if (Directory.Exists(configDir))
        {
            foreach (var configFile in Directory.GetFiles(configDir, "appsettings*.json"))
            {
                File.Copy(configFile, Path.Combine(backupPath, Path.GetFileName(configFile)));
            }
        }

        // Backup additional paths
        foreach (var additionalPath in options.AdditionalBackupPaths)
        {
            if (Directory.Exists(additionalPath))
            {
                CopyDirectory(additionalPath, Path.Combine(backupPath, Path.GetFileName(additionalPath)));
            }
            else if (File.Exists(additionalPath))
            {
                File.Copy(additionalPath, Path.Combine(backupPath, Path.GetFileName(additionalPath)));
            }
        }

        // Write backup metadata
        var backup = new UpdateBackup
        {
            Id = backupId,
            CreatedAt = DateTime.UtcNow,
            FromVersion = CurrentVersion.FullVersion,
            ForVersion = forVersion,
            Path = backupPath,
            SizeBytes = GetDirectorySize(backupPath)
        };

        await File.WriteAllTextAsync(
            Path.Combine(backupPath, "backup.json"),
            JsonSerializer.Serialize(backup),
            cancellationToken).ConfigureAwait(false);

        // Cleanup old backups
        await CleanupOldBackupsAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Created update backup: {BackupId}", backupId);
    }

    private async Task CleanupOldBackupsAsync(CancellationToken cancellationToken)
    {
        var backups = await GetBackupsAsync(cancellationToken).ConfigureAwait(false);
        var toDelete = backups.Skip(_config.KeepBackupCount);

        foreach (var backup in toDelete)
        {
            try
            {
                if (Directory.Exists(backup.Path))
                {
                    Directory.Delete(backup.Path, true);
                    _logger.LogInformation("Deleted old update backup: {BackupId}", backup.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old backup: {BackupId}", backup.Id);
            }
        }
    }

    private string? GetLatestBackupId()
    {
        if (!Directory.Exists(_backupDir))
        {
            return null;
        }

        return Directory.GetDirectories(_backupDir)
            .Select(Path.GetFileName)
            .OrderByDescending(x => x)
            .FirstOrDefault();
    }

    private async Task<ReleaseInfo> MapToReleaseInfoAsync(
        GitHubRelease release,
        CancellationToken cancellationToken)
    {
        var assets = new List<ReleaseAsset>();

        foreach (var a in release.Assets)
        {
            // Skip .sha256 files - they're companion files, not actual release assets
            if (a.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
                continue;

            var sha256 = await FetchChecksumAsync(a.Name, release.Assets, cancellationToken)
                .ConfigureAwait(false);

            assets.Add(new ReleaseAsset
            {
                Name = a.Name,
                DownloadUrl = a.BrowserDownloadUrl,
                Size = a.Size,
                Sha256 = sha256,
                Platform = DeterminePlatform(a.Name)
            });
        }

        return new ReleaseInfo
        {
            Version = release.TagName.TrimStart('v', 'V'),
            Name = release.Name ?? release.TagName,
            Body = release.Body ?? string.Empty,
            PublishedAt = release.PublishedAt,
            IsPreRelease = release.Prerelease,
            Assets = assets
        };
    }

    private async Task<string?> FetchChecksumAsync(
        string assetName,
        List<GitHubAsset> assets,
        CancellationToken cancellationToken)
    {
        // Check cache first
        if (_checksumCache.TryGetValue(assetName, out var cached))
            return cached;

        // Look for a .sha256 companion file
        var sha256Asset = assets.FirstOrDefault(a =>
            a.Name.Equals($"{assetName}.sha256", StringComparison.OrdinalIgnoreCase));

        if (sha256Asset is null)
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient("GitHub");
            var response = await client.GetStringAsync(sha256Asset.BrowserDownloadUrl, cancellationToken)
                .ConfigureAwait(false);

            // Parse: "hash  filename" or just "hash"
            var checksum = response
                .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim();

            if (!string.IsNullOrEmpty(checksum))
            {
                _checksumCache[assetName] = checksum;
                _logger.LogDebug("Fetched checksum for {AssetName}: {Checksum}", assetName, checksum);
            }

            return checksum;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch checksum for {AssetName}", assetName);
            return null;
        }
    }

    private static PlatformTarget DeterminePlatform(string assetName)
    {
        var name = assetName.ToLowerInvariant();

        if (name.Contains("linux") && name.Contains("arm64"))
            return PlatformTarget.LinuxArm64;
        if (name.Contains("linux"))
            return PlatformTarget.LinuxX64;
        if (name.Contains("windows") || name.Contains("win"))
            return PlatformTarget.WindowsX64;
        if (name.Contains("docker"))
            return PlatformTarget.Docker;

        return PlatformTarget.LinuxX64;
    }

    private string? FindDownloadedArchive(string version)
    {
        // Check for both .zip and .tar.gz formats
        var zipPath = Path.Combine(_updateDir, $"{version}.zip");
        if (File.Exists(zipPath)) return zipPath;

        var tarGzPath = Path.Combine(_updateDir, $"{version}.tar.gz");
        if (File.Exists(tarGzPath)) return tarGzPath;

        return null;
    }

    private static PlatformTarget GetCurrentPlatform()
    {
        if (OperatingSystem.IsLinux())
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? PlatformTarget.LinuxArm64
                : PlatformTarget.LinuxX64;
        }

        if (OperatingSystem.IsWindows())
        {
            return PlatformTarget.WindowsX64;
        }

        // Default to Linux x64 for unknown platforms
        return PlatformTarget.LinuxX64;
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }

    private static long GetDirectorySize(string path)
    {
        return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    #region GitHub API Models

    private sealed record GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; init; } = [];
    }

    private sealed record GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; init; }

        [JsonPropertyName("content_type")]
        public string? ContentType { get; init; }
    }

    #endregion
}
