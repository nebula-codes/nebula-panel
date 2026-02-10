using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Hytale;

public class HytaleUpdateService : IHytaleUpdateService
{
    private readonly IHytaleDownloaderService _downloaderService;
    private readonly IHytaleAuthService _authService;
    private readonly IGameServerRepository _serverRepository;
    private readonly IGameServerService _gameServerService;
    private readonly IBackupService _backupService;
    private readonly IHytaleUpdateNotifier _updateNotifier;
    private readonly ILogger<HytaleUpdateService> _logger;

    /// <summary>
    /// Tracks active update progress by server ID.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, HytaleUpdateProgressDto> _activeUpdates = new();

    public HytaleUpdateService(
        IHytaleDownloaderService downloaderService,
        IHytaleAuthService authService,
        IGameServerRepository serverRepository,
        IGameServerService gameServerService,
        IBackupService backupService,
        IHytaleUpdateNotifier updateNotifier,
        ILogger<HytaleUpdateService> logger)
    {
        _downloaderService = downloaderService;
        _authService = authService;
        _serverRepository = serverRepository;
        _gameServerService = gameServerService;
        _backupService = backupService;
        _updateNotifier = updateNotifier;
        _logger = logger;
    }

    public async Task<HytaleServerUpdateCheckResult> CheckForUpdateAsync(
        GameServer server,
        Guid userId,
        CancellationToken ct = default)
    {
        try
        {
            // Load the full server entity from database to avoid FK constraint issues
            // when updating (the passed-in entity may be a partial/detached entity)
            var fullServer = await _serverRepository.GetByIdAsync(server.Id, ct).ConfigureAwait(false);
            if (fullServer is null)
            {
                return HytaleServerUpdateCheckResult.Failed("Server not found.");
            }

            // Use the loaded entity from here on
            server = fullServer;

            if (server.HytaleInfo is null)
            {
                return HytaleServerUpdateCheckResult.Failed("This server does not have Hytale version information.");
            }

            // Get credentials
            var credentials = await _authService.GetDecryptedCredentialsAsync(userId, ct).ConfigureAwait(false);
            if (credentials == null || !credentials.IsValid)
            {
                return HytaleServerUpdateCheckResult.Failed("Hytale credentials are missing or expired. Please reconnect your Hytale account.");
            }

            // Get latest version for the patchline
            var latestVersion = await _downloaderService.GetServerVersionAsync(
                server.HytaleInfo.Patchline,
                credentials,
                ct).ConfigureAwait(false);

            if (latestVersion == null)
            {
                return HytaleServerUpdateCheckResult.Failed("Failed to retrieve latest version information from Hytale.");
            }

            // Compare versions
            var updateAvailable = !string.Equals(
                server.HytaleInfo.InstalledVersion,
                latestVersion.Version,
                StringComparison.OrdinalIgnoreCase);

            // Update the cached update info
            // Create a new HytaleServerInfo object to ensure EF Core detects the change
            // (JSON value converters don't track internal property changes)
            server.HytaleInfo = new HytaleServerInfo
            {
                InstalledVersion = server.HytaleInfo.InstalledVersion,
                Patchline = server.HytaleInfo.Patchline,
                InstalledAt = server.HytaleInfo.InstalledAt,
                LastUpdateCheckAt = DateTime.UtcNow,
                AvailableVersion = latestVersion.Version,
                UpdateAvailable = updateAvailable,
                DismissedVersion = server.HytaleInfo.DismissedVersion
            };

            await _serverRepository.UpdateAsync(server, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Update check for server {ServerId}: current={CurrentVersion}, latest={LatestVersion}, updateAvailable={UpdateAvailable}",
                server.Id, server.HytaleInfo.InstalledVersion, latestVersion.Version, updateAvailable);

            // Notify if update available
            if (updateAvailable)
            {
                await _updateNotifier.NotifyUpdateAvailableAsync(
                    server.Id,
                    server.HytaleInfo.InstalledVersion,
                    latestVersion.Version,
                    ct).ConfigureAwait(false);
            }

            return HytaleServerUpdateCheckResult.Succeeded(
                updateAvailable,
                server.HytaleInfo.InstalledVersion,
                latestVersion.Version,
                server.HytaleInfo.Patchline);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates for server {ServerId}", server.Id);
            return HytaleServerUpdateCheckResult.Failed($"Failed to check for updates: {ex.Message}");
        }
    }

    public async Task<HytaleUpdateResult> UpdateServerAsync(
        GameServer server,
        Guid userId,
        HytaleUpdateOptions options,
        IProgress<HytaleUpdateProgressDto>? progress = null,
        CancellationToken ct = default)
    {
        return await PerformUpdateAsync(
            server,
            server.HytaleInfo?.Patchline ?? "release",
            userId,
            options,
            progress,
            ct).ConfigureAwait(false);
    }

    public async Task<HytaleUpdateResult> ChangePatchlineAsync(
        GameServer server,
        string newPatchline,
        Guid userId,
        HytaleUpdateOptions options,
        IProgress<HytaleUpdateProgressDto>? progress = null,
        CancellationToken ct = default)
    {
        return await PerformUpdateAsync(
            server,
            newPatchline,
            userId,
            options,
            progress,
            ct).ConfigureAwait(false);
    }

    private async Task<HytaleUpdateResult> PerformUpdateAsync(
        GameServer server,
        string patchline,
        Guid userId,
        HytaleUpdateOptions options,
        IProgress<HytaleUpdateProgressDto>? progress,
        CancellationToken ct)
    {
        // Load the full server entity from database to avoid FK constraint issues
        // when updating (the passed-in entity may be a partial/detached entity)
        var fullServer = await _serverRepository.GetByIdAsync(server.Id, ct).ConfigureAwait(false);
        if (fullServer is null)
        {
            return HytaleUpdateResult.Failed("Server not found.");
        }

        // Use the loaded entity from here on
        server = fullServer;

        var previousVersion = server.HytaleInfo?.InstalledVersion ?? server.InstalledVersion ?? "unknown";
        var wasRunning = server.Status == ServerStatus.Running;
        Guid? backupId = null;

        try
        {
            if (server.HytaleInfo is null)
            {
                return HytaleUpdateResult.Failed("This server does not have Hytale version information.");
            }

            // Report: Initializing
            var progressDto = HytaleUpdateProgressDto.Create(
                server.Id,
                HytaleUpdatePhase.Initializing,
                "Preparing update...",
                0);
            TrackProgress(server.Id, progressDto);
            progress?.Report(progressDto);
            await _updateNotifier.NotifyProgressAsync(server.Id, progressDto, ct).ConfigureAwait(false);

            // Get and verify credentials
            var credentials = await _authService.GetDecryptedCredentialsAsync(userId, ct).ConfigureAwait(false);
            if (credentials == null || !credentials.IsValid)
            {
                return HytaleUpdateResult.Failed("Hytale credentials are missing or expired. Please reconnect your Hytale account.");
            }

            // Report: Stopping server
            if (wasRunning)
            {
                progressDto = HytaleUpdateProgressDto.Create(
                    server.Id,
                    HytaleUpdatePhase.StoppingServer,
                    "Stopping server...",
                    5);
                TrackProgress(server.Id, progressDto);
                progress?.Report(progressDto);
                await _updateNotifier.NotifyProgressAsync(server.Id, progressDto, ct).ConfigureAwait(false);

                var stopResult = await _gameServerService.StopServerAsync(server.Id, ct).ConfigureAwait(false);
                if (!stopResult.IsSuccess)
                {
                    _logger.LogWarning("Failed to stop server {ServerId} gracefully, continuing with update", server.Id);
                }

                // Wait for server to stop
                await Task.Delay(2000, ct).ConfigureAwait(false);
            }

            // Report: Creating backup
            if (options.CreateBackupBeforeUpdate)
            {
                progressDto = HytaleUpdateProgressDto.Create(
                    server.Id,
                    HytaleUpdatePhase.CreatingBackup,
                    "Creating backup...",
                    10);
                TrackProgress(server.Id, progressDto);
                progress?.Report(progressDto);
                await _updateNotifier.NotifyProgressAsync(server.Id, progressDto, ct).ConfigureAwait(false);

                var backupRequest = new CreateBackupRequest(
                    server.Id,
                    BackupScope.Full,
                    $"Pre-update backup ({previousVersion})",
                    $"Automatic backup before updating from {previousVersion}");

                var backupResult = await _backupService.CreateBackupAsync(
                    backupRequest,
                    BackupType.PreUpdate,
                    null,
                    ct).ConfigureAwait(false);

                if (backupResult.IsSuccess)
                {
                    backupId = backupResult.Value.Id;
                    _logger.LogInformation("Created pre-update backup {BackupId} for server {ServerId}", backupId, server.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to create backup for server {ServerId}: {Error}", server.Id, backupResult.Error);
                }
            }

            // Report: Authenticating
            progressDto = HytaleUpdateProgressDto.Create(
                server.Id,
                HytaleUpdatePhase.Authenticating,
                "Authenticating with Hytale...",
                15);
            TrackProgress(server.Id, progressDto);
            progress?.Report(progressDto);
            await _updateNotifier.NotifyProgressAsync(server.Id, progressDto, ct).ConfigureAwait(false);

            // Update server status
            server.Status = ServerStatus.Updating;
            await _serverRepository.UpdateAsync(server, ct).ConfigureAwait(false);

            // Preserve data directories if needed
            var preservedPaths = new List<(string source, string dest)>();
            if (options.PreserveWorldData || options.PreserveCustomConfigs)
            {
                progressDto = HytaleUpdateProgressDto.Create(
                    server.Id,
                    HytaleUpdatePhase.PreservingFiles,
                    "Preserving user data...",
                    18);
                TrackProgress(server.Id, progressDto);
                progress?.Report(progressDto);
                await _updateNotifier.NotifyProgressAsync(server.Id, progressDto, ct).ConfigureAwait(false);

                preservedPaths = await PreserveDataAsync(server, options, ct).ConfigureAwait(false);
            }

            // Re-fetch credentials right before download in case they were refreshed during backup
            // (Hytale invalidates old tokens when a new one is issued via refresh)
            credentials = await _authService.GetDecryptedCredentialsAsync(userId, ct).ConfigureAwait(false);
            if (credentials == null || !credentials.IsValid)
            {
                server.Status = ServerStatus.Stopped;
                await _serverRepository.UpdateAsync(server, ct).ConfigureAwait(false);
                return HytaleUpdateResult.Failed("Hytale credentials expired during update. Please try again.");
            }

            // Download new version
            // Note: Progress<T> callbacks are invoked synchronously (Action<T>), so we cannot
            // await the async notifier call. Task.Run prevents deadlocks if a SynchronizationContext
            // is present, and the try/catch ensures a notification failure doesn't crash the download.
            var downloadProgress = new Progress<HytaleDownloadProgress>(p =>
            {
                var downloadDto = HytaleUpdateProgressDto.Downloading(
                    server.Id,
                    p.Percentage,
                    p.BytesDownloaded,
                    p.TotalBytes);
                TrackProgress(server.Id, downloadDto);
                progress?.Report(downloadDto);
                try
                {
                    Task.Run(() => _updateNotifier.NotifyProgressAsync(server.Id, downloadDto, CancellationToken.None))
                        .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to notify download progress for server {ServerId}", server.Id);
                }
            });

            var downloadResult = await _downloaderService.DownloadServerAsync(
                server.InstallPath,
                patchline,
                credentials,
                downloadProgress,
                ct).ConfigureAwait(false);

            if (!downloadResult.Success)
            {
                // Restore server status
                server.Status = wasRunning ? ServerStatus.Stopped : ServerStatus.Stopped;
                await _serverRepository.UpdateAsync(server, ct).ConfigureAwait(false);

                return HytaleUpdateResult.Failed(downloadResult.Error ?? "Download failed.");
            }

            // Report: Extracting
            progressDto = HytaleUpdateProgressDto.Create(
                server.Id,
                HytaleUpdatePhase.Extracting,
                "Extracting files...",
                75);
            TrackProgress(server.Id, progressDto);
            progress?.Report(progressDto);
            await _updateNotifier.NotifyProgressAsync(server.Id, progressDto, ct).ConfigureAwait(false);

            // Restore preserved data
            if (preservedPaths.Count > 0)
            {
                progressDto = HytaleUpdateProgressDto.Create(
                    server.Id,
                    HytaleUpdatePhase.PreservingFiles,
                    "Restoring user data...",
                    85);
                TrackProgress(server.Id, progressDto);
                progress?.Report(progressDto);
                await _updateNotifier.NotifyProgressAsync(server.Id, progressDto, ct).ConfigureAwait(false);

                await RestoreDataAsync(preservedPaths, ct).ConfigureAwait(false);
            }

            // Report: Configuring
            progressDto = HytaleUpdateProgressDto.Create(
                server.Id,
                HytaleUpdatePhase.Configuring,
                "Updating configuration...",
                90);
            TrackProgress(server.Id, progressDto);
            progress?.Report(progressDto);
            await _updateNotifier.NotifyProgressAsync(server.Id, progressDto, ct).ConfigureAwait(false);

            // Update server info
            // Create a new HytaleServerInfo object to ensure EF Core detects the change
            // (JSON value converters don't track internal property changes)
            var newVersion = downloadResult.Version ?? "unknown";
            server.InstalledVersion = newVersion;
            server.HytaleInfo = new HytaleServerInfo
            {
                InstalledVersion = newVersion,
                Patchline = patchline,
                InstalledAt = DateTime.UtcNow,
                UpdateAvailable = false,
                AvailableVersion = newVersion,
                LastUpdateCheckAt = DateTime.UtcNow,
                DismissedVersion = server.HytaleInfo?.DismissedVersion
            };
            server.Status = ServerStatus.Stopped;

            await _serverRepository.UpdateAsync(server, ct).ConfigureAwait(false);

            // Restart server if needed
            if (wasRunning && options.RestartServerAfterUpdate)
            {
                progressDto = HytaleUpdateProgressDto.Create(
                    server.Id,
                    HytaleUpdatePhase.StartingServer,
                    "Starting server...",
                    95);
                TrackProgress(server.Id, progressDto);
                progress?.Report(progressDto);
                await _updateNotifier.NotifyProgressAsync(server.Id, progressDto, ct).ConfigureAwait(false);

                await _gameServerService.StartServerAsync(server.Id, ct).ConfigureAwait(false);
            }

            // Report: Complete
            var completeDto = HytaleUpdateProgressDto.Complete(server.Id);
            ClearActiveUpdate(server.Id);
            progress?.Report(completeDto);
            await _updateNotifier.NotifyCompleteAsync(server.Id, true, newVersion, null, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Server {ServerId} updated from {PreviousVersion} to {NewVersion}",
                server.Id, previousVersion, newVersion);

            return HytaleUpdateResult.Succeeded(previousVersion, newVersion, backupId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update for server {ServerId} was cancelled", server.Id);

            // Restore server status
            server.Status = ServerStatus.Stopped;
            await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);

            ClearActiveUpdate(server.Id);
            var errorDto = HytaleUpdateProgressDto.Error(server.Id, "Update was cancelled.");
            progress?.Report(errorDto);
            await _updateNotifier.NotifyCompleteAsync(server.Id, false, null, "Update was cancelled.", CancellationToken.None)
                .ConfigureAwait(false);

            return HytaleUpdateResult.Failed("Update was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update server {ServerId}", server.Id);

            // Restore server status
            server.Status = ServerStatus.Stopped;
            await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);

            ClearActiveUpdate(server.Id);
            var errorDto = HytaleUpdateProgressDto.Error(server.Id, ex.Message);
            progress?.Report(errorDto);
            await _updateNotifier.NotifyCompleteAsync(server.Id, false, null, ex.Message, CancellationToken.None)
                .ConfigureAwait(false);

            return HytaleUpdateResult.Failed($"Update failed: {ex.Message}");
        }
    }

    public async Task DismissUpdateAsync(
        Guid serverId,
        string version,
        CancellationToken ct = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
        if (server?.HytaleInfo == null)
        {
            return;
        }

        // Create a new HytaleServerInfo object to ensure EF Core detects the change
        server.HytaleInfo = new HytaleServerInfo
        {
            InstalledVersion = server.HytaleInfo.InstalledVersion,
            Patchline = server.HytaleInfo.Patchline,
            InstalledAt = server.HytaleInfo.InstalledAt,
            LastUpdateCheckAt = server.HytaleInfo.LastUpdateCheckAt,
            AvailableVersion = server.HytaleInfo.AvailableVersion,
            UpdateAvailable = server.HytaleInfo.UpdateAvailable,
            DismissedVersion = version
        };
        await _serverRepository.UpdateAsync(server, ct).ConfigureAwait(false);

        _logger.LogInformation("User dismissed update notification for version {Version} on server {ServerId}",
            version, serverId);
    }

    public async Task<HytaleUpdateStatusDto> GetUpdateStatusAsync(
        Guid serverId,
        CancellationToken ct = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);

        if (server?.HytaleInfo == null)
        {
            return new HytaleUpdateStatusDto { IsHytaleServer = false };
        }

        return new HytaleUpdateStatusDto
        {
            IsHytaleServer = true,
            UpdateAvailable = server.HytaleInfo.UpdateAvailable &&
                              server.HytaleInfo.DismissedVersion != server.HytaleInfo.AvailableVersion,
            CurrentVersion = server.HytaleInfo.InstalledVersion,
            AvailableVersion = server.HytaleInfo.AvailableVersion,
            Patchline = server.HytaleInfo.Patchline,
            InstalledAt = server.HytaleInfo.InstalledAt,
            LastCheckedAt = server.HytaleInfo.LastUpdateCheckAt
        };
    }

    public Task<HytaleUpdateProgressDto?> GetActiveUpdateProgressAsync(
        Guid serverId,
        CancellationToken ct = default)
    {
        _activeUpdates.TryGetValue(serverId, out var progress);
        return Task.FromResult(progress);
    }

    private void TrackProgress(Guid serverId, HytaleUpdateProgressDto progress)
    {
        _activeUpdates[serverId] = progress;
    }

    private void ClearActiveUpdate(Guid serverId)
    {
        _activeUpdates.TryRemove(serverId, out _);
    }

    private async Task<List<(string source, string dest)>> PreserveDataAsync(
        GameServer server,
        HytaleUpdateOptions options,
        CancellationToken ct)
    {
        var preservedPaths = new List<(string source, string dest)>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"nebula-hytale-update-{server.Id}");

        // Clean up any existing temp directory
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
        Directory.CreateDirectory(tempDir);

        // Preserve world data
        if (options.PreserveWorldData)
        {
            var worldsPath = Path.Combine(server.InstallPath, "worlds");
            if (Directory.Exists(worldsPath))
            {
                var destPath = Path.Combine(tempDir, "worlds");
                await CopyDirectoryAsync(worldsPath, destPath, ct).ConfigureAwait(false);
                preservedPaths.Add((worldsPath, destPath));
            }
        }

        // Preserve custom configs
        if (options.PreserveCustomConfigs)
        {
            var configPath = Path.Combine(server.InstallPath, "server.json");
            if (File.Exists(configPath))
            {
                var destPath = Path.Combine(tempDir, "server.json");
                File.Copy(configPath, destPath, true);
                preservedPaths.Add((configPath, destPath));
            }
        }

        return preservedPaths;
    }

    private async Task RestoreDataAsync(
        List<(string source, string dest)> preservedPaths,
        CancellationToken ct)
    {
        foreach (var (source, dest) in preservedPaths)
        {
            if (Directory.Exists(dest))
            {
                // Restore directory
                if (Directory.Exists(source))
                {
                    Directory.Delete(source, true);
                }
                await CopyDirectoryAsync(dest, source, ct).ConfigureAwait(false);
            }
            else if (File.Exists(dest))
            {
                // Restore file
                File.Copy(dest, source, true);
            }
        }

        // Clean up temp directory
        var tempDir = Path.GetDirectoryName(preservedPaths.FirstOrDefault().dest);
        if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static async Task CopyDirectoryAsync(string sourceDir, string destDir, CancellationToken ct)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            await using var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await sourceStream.CopyToAsync(destStream, ct).ConfigureAwait(false);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, destSubDir, ct).ConfigureAwait(false);
        }
    }
}
