using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Infrastructure.ModpackProviders;

/// <summary>
/// Service for checking and applying modpack updates.
/// </summary>
public class ModpackUpdateService(
    IModpackProviderRegistry providerRegistry,
    IModpackInstallationService installationService,
    IBackupService backupService,
    IGameServerService serverService,
    IGameServerRepository serverRepository,
    ModpackFilePreserver filePreserver,
    ILogger<ModpackUpdateService> logger) : IModpackUpdateService
{
    private readonly IModpackProviderRegistry _providerRegistry = providerRegistry;
    private readonly IModpackInstallationService _installationService = installationService;
    private readonly IBackupService _backupService = backupService;
    private readonly IGameServerService _serverService = serverService;
    private readonly IGameServerRepository _serverRepository = serverRepository;
    private readonly ModpackFilePreserver _filePreserver = filePreserver;
    private readonly ILogger<ModpackUpdateService> _logger = logger;

    /// <inheritdoc />
    public async Task<ModpackUpdateInfo?> CheckForUpdateAsync(
        GameServer server,
        CancellationToken ct = default)
    {
        if (server.InstalledModpack is null)
        {
            _logger.LogDebug("Server {ServerId} has no modpack installed", server.Id);
            return null;
        }

        var installedModpack = server.InstalledModpack;
        _logger.LogInformation(
            "Checking for updates to {ModpackName} (current: {CurrentVersion}) on server {ServerId}",
            installedModpack.ModpackName, installedModpack.VersionName ?? installedModpack.VersionId, server.Id);

        try
        {
            var provider = _providerRegistry.GetProvider(installedModpack.Provider);
            if (provider is null)
            {
                _logger.LogWarning("Provider {Provider} not available for update check", installedModpack.Provider);
                return null;
            }

            // Get all versions from the provider
            var versions = await provider.GetVersionsAsync(installedModpack.ModpackId, ct).ConfigureAwait(false);
            if (versions is null || versions.Count == 0)
            {
                _logger.LogDebug("No versions found for modpack {ModpackId}", installedModpack.ModpackId);
                return null;
            }

            // Find the installed version to get its release date
            var installedVersion = versions.FirstOrDefault(v => v.Id == installedModpack.VersionId);
            var installedDate = installedVersion?.ReleasedAt ?? installedModpack.InstalledAt;

            // Find versions newer than installed (by release date)
            // Also filter for versions that have server packs
            var newerVersions = versions
                .Where(v => v.ReleasedAt > installedDate && v.HasServerPack)
                .OrderByDescending(v => v.ReleasedAt)
                .ToList();

            if (newerVersions.Count == 0)
            {
                _logger.LogDebug("No newer versions available for {ModpackName}", installedModpack.ModpackName);

                // Update cache to indicate no update available
                installedModpack.UpdateAvailable = false;
                installedModpack.LastUpdateCheckAt = DateTime.UtcNow;
                installedModpack.LatestVersionId = null;
                installedModpack.LatestVersionName = null;

                return null;
            }

            var latestVersion = newerVersions.First();

            // Check if this version was dismissed by user
            if (installedModpack.DismissedVersionId == latestVersion.Id)
            {
                _logger.LogDebug("User dismissed update to version {Version}", latestVersion.Name);
                return null;
            }

            // Detect breaking changes
            var isMinecraftVersionChange = !string.Equals(
                installedModpack.MinecraftVersion,
                latestVersion.MinecraftVersion,
                StringComparison.OrdinalIgnoreCase);

            var isLoaderChange = installedModpack.LoaderType != latestVersion.Loader;

            var isBreakingChange = isMinecraftVersionChange || isLoaderChange;

            // Generate warnings
            var warnings = new List<string>();

            if (isMinecraftVersionChange)
            {
                warnings.Add($"This update changes Minecraft version from {installedModpack.MinecraftVersion} to {latestVersion.MinecraftVersion}");
            }

            if (isLoaderChange)
            {
                warnings.Add($"This update changes mod loader from {installedModpack.LoaderType} to {latestVersion.Loader}");
            }

            if (isBreakingChange)
            {
                warnings.Add("This is a major update that may require world reset or cause compatibility issues");
            }

            if (latestVersion.ServerPackSize > 500 * 1024 * 1024) // 500 MB
            {
                warnings.Add($"Large download size: {FormatBytes(latestVersion.ServerPackSize)}");
            }

            // Build intermediate versions list
            var intermediateVersions = newerVersions
                .Skip(1) // Skip latest (already shown)
                .Take(5) // Show up to 5 intermediate versions
                .Select(v => new ModpackVersionSummary
                {
                    Id = v.Id,
                    Name = v.Name,
                    ReleasedAt = v.ReleasedAt,
                    Type = v.Type,
                    MinecraftVersion = v.MinecraftVersion,
                    LoaderType = v.Loader,
                    HasServerPack = v.HasServerPack,
                    ServerPackSizeBytes = v.ServerPackSize
                })
                .ToList();

            // Update cache
            installedModpack.UpdateAvailable = true;
            installedModpack.LatestVersionId = latestVersion.Id;
            installedModpack.LatestVersionName = latestVersion.Name;
            installedModpack.LatestVersionReleasedAt = latestVersion.ReleasedAt;
            installedModpack.LastUpdateCheckAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Update available for {ModpackName}: {CurrentVersion} -> {LatestVersion}",
                installedModpack.ModpackName,
                installedModpack.VersionName ?? installedModpack.VersionId,
                latestVersion.Name);

            return new ModpackUpdateInfo
            {
                CurrentVersionId = installedModpack.VersionId,
                CurrentVersionName = installedModpack.VersionName ?? installedModpack.VersionId,
                LatestVersionId = latestVersion.Id,
                LatestVersionName = latestVersion.Name,
                ReleasedAt = latestVersion.ReleasedAt,
                Changelog = latestVersion.Changelog,
                IsBreakingChange = isBreakingChange,
                IsMinecraftVersionChange = isMinecraftVersionChange,
                IsLoaderChange = isLoaderChange,
                CurrentMinecraftVersion = installedModpack.MinecraftVersion,
                NewMinecraftVersion = latestVersion.MinecraftVersion,
                CurrentLoaderType = installedModpack.LoaderType,
                NewLoaderType = latestVersion.Loader,
                NewLoaderVersion = latestVersion.LoaderVersion,
                ServerPackSizeBytes = latestVersion.ServerPackSize,
                Warnings = warnings,
                IntermediateVersions = intermediateVersions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates for modpack {ModpackName}", installedModpack.ModpackName);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModpackVersionSummary>> GetAvailableVersionsAsync(
        GameServer server,
        CancellationToken ct = default)
    {
        if (server.InstalledModpack is null)
        {
            return [];
        }

        var installedModpack = server.InstalledModpack;

        try
        {
            var provider = _providerRegistry.GetProvider(installedModpack.Provider);
            if (provider is null)
            {
                return [];
            }

            var versions = await provider.GetVersionsAsync(installedModpack.ModpackId, ct).ConfigureAwait(false);
            if (versions is null)
            {
                return [];
            }

            // Find the installed version to get its release date
            var installedVersion = versions.FirstOrDefault(v => v.Id == installedModpack.VersionId);
            var installedDate = installedVersion?.ReleasedAt ?? installedModpack.InstalledAt;

            // Return versions newer than installed with server packs
            return versions
                .Where(v => v.ReleasedAt > installedDate && v.HasServerPack)
                .OrderByDescending(v => v.ReleasedAt)
                .Select(v => new ModpackVersionSummary
                {
                    Id = v.Id,
                    Name = v.Name,
                    ReleasedAt = v.ReleasedAt,
                    Type = v.Type,
                    MinecraftVersion = v.MinecraftVersion,
                    LoaderType = v.Loader,
                    HasServerPack = v.HasServerPack,
                    ServerPackSizeBytes = v.ServerPackSize
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available versions for modpack {ModpackName}", installedModpack.ModpackName);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<ModpackUpdateResult> UpdateModpackAsync(
        GameServer server,
        string targetVersionId,
        ModpackUpdateOptions options,
        IProgress<ModpackUpdateProgressDto>? progress = null,
        CancellationToken ct = default)
    {
        if (server.InstalledModpack is null)
        {
            return ModpackUpdateResult.Failed("No modpack installed on this server");
        }

        var installedModpack = server.InstalledModpack;
        var previousVersionId = installedModpack.VersionId;
        var wasRunning = server.Status == ServerStatus.Running;
        Guid? backupId = null;
        string? tempPreservePath = null;

        _logger.LogInformation(
            "Starting modpack update: {ModpackName} from {CurrentVersion} to {TargetVersion} on server {ServerId}",
            installedModpack.ModpackName, installedModpack.VersionName, targetVersionId, server.Id);

        try
        {
            // Phase 1: Initialize
            progress?.Report(ModpackUpdateProgressDto.Initializing());

            var provider = _providerRegistry.GetProvider(installedModpack.Provider);
            if (provider is null)
            {
                return ModpackUpdateResult.Failed($"Provider {installedModpack.Provider} not available");
            }

            // Get target version details
            var versions = await provider.GetVersionsAsync(installedModpack.ModpackId, ct).ConfigureAwait(false);
            var targetVersion = versions?.FirstOrDefault(v => v.Id == targetVersionId);

            if (targetVersion is null)
            {
                return ModpackUpdateResult.Failed($"Version {targetVersionId} not found");
            }

            // Phase 2: Stop server if running
            if (wasRunning)
            {
                progress?.Report(ModpackUpdateProgressDto.StoppingServer());
                _logger.LogInformation("Stopping server before update");

                var stopResult = await _serverService.StopServerAsync(server.Id, ct).ConfigureAwait(false);
                if (!stopResult.IsSuccess)
                {
                    return ModpackUpdateResult.Failed($"Failed to stop server: {stopResult.Error}");
                }

                // Wait for server to fully stop
                await Task.Delay(2000, ct).ConfigureAwait(false);
            }

            // Phase 3: Create backup
            if (options.CreateBackupBeforeUpdate)
            {
                progress?.Report(ModpackUpdateProgressDto.CreatingBackup());
                _logger.LogInformation("Creating pre-update backup");

                var backupResult = await _backupService.CreateFullBackupAsync(
                    server.Id,
                    $"Pre-Update Backup ({installedModpack.VersionName} -> {targetVersion.Name})",
                    "Automatic backup before modpack update",
                    BackupType.PreUpdate,
                    cancellationToken: ct).ConfigureAwait(false);

                if (backupResult.IsSuccess)
                {
                    backupId = backupResult.Value.Id;
                    _logger.LogInformation("Pre-update backup created: {BackupId}", backupId);
                }
                else
                {
                    _logger.LogWarning("Failed to create backup: {Error}", backupResult.Error);
                    // Continue anyway - backup is optional
                }
            }

            // Phase 4: Preserve user files
            progress?.Report(ModpackUpdateProgressDto.PreservingFiles());
            var preserveResult = await _filePreserver.SaveUserFilesAsync(
                server.InstallPath, options, ct).ConfigureAwait(false);

            if (!preserveResult.Success)
            {
                _logger.LogWarning("Failed to preserve user files: {Error}", preserveResult.Error);
                // Continue anyway - we have a backup
            }
            else
            {
                tempPreservePath = preserveResult.TempPath;
                _logger.LogInformation("Preserved {Count} paths", preserveResult.PreservedPaths.Count);
            }

            // Phase 5: Download and install new version
            // We need to clear the mods/libraries but keep preserved files
            await ClearModpackFilesAsync(server.InstallPath, ct).ConfigureAwait(false);

            // Create install request for new version
            var installRequest = new ModpackInstallRequest
            {
                Provider = installedModpack.Provider,
                ModpackId = installedModpack.ModpackId,
                ModpackName = installedModpack.ModpackName,
                VersionId = targetVersionId,
                VersionName = targetVersion.Name,
                MinecraftVersion = targetVersion.MinecraftVersion,
                Loader = targetVersion.Loader,
                LoaderVersion = targetVersion.LoaderVersion,
                RecommendedRamMb = installedModpack.ModCount.HasValue
                    ? (long)(installedModpack.ModCount.Value * 50 + 2048)
                    : 4096
            };

            // Create progress adapter
            var installProgress = progress is not null
                ? new Progress<ModpackInstallProgress>(p =>
                {
                    // Map install progress to update progress
                    var phase = p.Phase switch
                    {
                        ModpackInstallPhase.Downloading => ModpackUpdatePhase.Downloading,
                        ModpackInstallPhase.Extracting => ModpackUpdatePhase.Extracting,
                        ModpackInstallPhase.InstallingLoader => ModpackUpdatePhase.InstallingLoader,
                        ModpackInstallPhase.Configuring => ModpackUpdatePhase.Configuring,
                        _ => ModpackUpdatePhase.Downloading
                    };

                    // Scale percentage: install is 15-85% of update
                    var scaledPercentage = 15 + (p.Percentage * 0.7);

                    progress.Report(new ModpackUpdateProgressDto
                    {
                        Phase = phase,
                        Message = p.Message,
                        Percentage = scaledPercentage,
                        BytesDownloaded = p.BytesDownloaded,
                        TotalBytes = p.TotalBytes,
                        CurrentFile = p.CurrentFile
                    });
                })
                : null;

            var installResult = await _installationService.InstallAsync(
                server, installRequest, installProgress, ct).ConfigureAwait(false);

            if (!installResult.Success)
            {
                _logger.LogError("Failed to install new modpack version: {Error}", installResult.Error);

                // Try to restore from backup
                if (backupId.HasValue)
                {
                    _logger.LogInformation("Attempting to restore from backup after failed update");
                    await _backupService.RestoreBackupAsync(backupId.Value, false, false, ct).ConfigureAwait(false);
                }

                return ModpackUpdateResult.Failed($"Failed to install new version: {installResult.Error}");
            }

            // Phase 6: Restore user files
            var restorationResult = new RestorationResult { Success = true };
            if (!string.IsNullOrEmpty(tempPreservePath))
            {
                progress?.Report(ModpackUpdateProgressDto.PreservingFiles());

                restorationResult = await _filePreserver.RestoreUserFilesAsync(
                    server.InstallPath, tempPreservePath, options, ct).ConfigureAwait(false);

                if (!restorationResult.Success)
                {
                    _logger.LogWarning("Failed to restore some user files: {Error}", restorationResult.Error);
                    // Continue - the update itself succeeded
                }

                // Cleanup temp files
                await _filePreserver.CleanupTempFilesAsync(tempPreservePath, ct).ConfigureAwait(false);
            }

            // Phase 7: Update server entity
            server.InstalledModpack.VersionId = targetVersionId;
            server.InstalledModpack.VersionName = targetVersion.Name;
            server.InstalledModpack.MinecraftVersion = targetVersion.MinecraftVersion;
            server.InstalledModpack.LoaderType = targetVersion.Loader;
            server.InstalledModpack.LoaderVersion = targetVersion.LoaderVersion;
            server.InstalledModpack.UpdateAvailable = false;
            server.InstalledModpack.LatestVersionId = null;
            server.InstalledModpack.LatestVersionName = null;
            server.InstalledModpack.DismissedVersionId = null;
            server.InstalledVersion = targetVersion.MinecraftVersion;

            await _serverRepository.UpdateAsync(server, ct).ConfigureAwait(false);

            // Phase 8: Restart server if it was running
            if (wasRunning && options.RestartServerAfterUpdate)
            {
                progress?.Report(ModpackUpdateProgressDto.StartingServer());
                _logger.LogInformation("Restarting server after update");

                await _serverService.StartServerAsync(server.Id, ct).ConfigureAwait(false);
            }

            progress?.Report(ModpackUpdateProgressDto.Completed());

            _logger.LogInformation(
                "Successfully updated modpack {ModpackName} from {PreviousVersion} to {NewVersion}",
                installedModpack.ModpackName, previousVersionId, targetVersionId);

            return ModpackUpdateResult.Succeeded(
                backupId,
                previousVersionId,
                targetVersionId,
                restorationResult.RestoredPaths,
                restorationResult.Conflicts);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Modpack update was cancelled");
            progress?.Report(ModpackUpdateProgressDto.Failed("Update cancelled"));

            // Try to restore from backup
            if (backupId.HasValue)
            {
                await _backupService.RestoreBackupAsync(backupId.Value, false, false, ct).ConfigureAwait(false);
            }

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during modpack update");
            progress?.Report(ModpackUpdateProgressDto.Failed(ex.Message));

            // Try to restore from backup
            if (backupId.HasValue)
            {
                try
                {
                    await _backupService.RestoreBackupAsync(backupId.Value, false, false).ConfigureAwait(false);
                }
                catch (Exception restoreEx)
                {
                    _logger.LogError(restoreEx, "Failed to restore from backup after update failure");
                }
            }

            return ModpackUpdateResult.Failed($"Unexpected error: {ex.Message}");
        }
        finally
        {
            // Cleanup temp files if they exist
            if (!string.IsNullOrEmpty(tempPreservePath))
            {
                await _filePreserver.CleanupTempFilesAsync(tempPreservePath, ct).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task DismissUpdateAsync(
        Guid serverId,
        string versionId,
        CancellationToken ct = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
        if (server?.InstalledModpack is null)
        {
            return;
        }

        server.InstalledModpack.DismissedVersionId = versionId;
        server.InstalledModpack.UpdateAvailable = false;

        await _serverRepository.UpdateAsync(server, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "User dismissed update to version {VersionId} for server {ServerId}",
            versionId, serverId);
    }

    /// <inheritdoc />
    public async Task<ModpackUpdateStatusDto> GetUpdateStatusAsync(
        Guid serverId,
        CancellationToken ct = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);

        if (server?.InstalledModpack is null)
        {
            return new ModpackUpdateStatusDto { HasModpack = false };
        }

        var modpack = server.InstalledModpack;

        return new ModpackUpdateStatusDto
        {
            HasModpack = true,
            UpdateAvailable = modpack.UpdateAvailable &&
                             modpack.DismissedVersionId != modpack.LatestVersionId,
            CurrentVersion = modpack.VersionName ?? modpack.VersionId,
            LatestVersion = modpack.LatestVersionName,
            LastCheckedAt = modpack.LastUpdateCheckAt
        };
    }

    /// <summary>
    /// Clears modpack-specific files (mods, libraries) but preserves user data.
    /// </summary>
    private async Task ClearModpackFilesAsync(string serverPath, CancellationToken ct)
    {
        var dirsToDelete = new[] { "mods", "libraries", ".fabric", "versions" };

        foreach (var dir in dirsToDelete)
        {
            var path = Path.Combine(serverPath, dir);
            if (Directory.Exists(path))
            {
                try
                {
                    await Task.Run(() => Directory.Delete(path, recursive: true), ct).ConfigureAwait(false);
                    _logger.LogDebug("Deleted directory: {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete directory: {Path}", path);
                }
            }
        }

        // Delete jar files in root (old server jars, forge installers, etc.)
        var jarFiles = Directory.GetFiles(serverPath, "*.jar");
        foreach (var jar in jarFiles)
        {
            try
            {
                File.Delete(jar);
                _logger.LogDebug("Deleted jar file: {Path}", jar);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete jar file: {Path}", jar);
            }
        }
    }

    private static string FormatBytes(long? bytes)
    {
        if (!bytes.HasValue) return "Unknown size";

        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes.Value;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
