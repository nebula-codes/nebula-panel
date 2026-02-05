using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.ModpackProviders;

/// <summary>
/// Service for orchestrating modpack installation on game servers.
/// Coordinates downloading, loader installation, and server configuration.
/// </summary>
public class ModpackInstallationService(
    IModpackProviderRegistry providerRegistry,
    ModpackLoaderInstaller loaderInstaller,
    ModpackServerConfigurator serverConfigurator,
    ILogger<ModpackInstallationService> logger) : IModpackInstallationService
{
    private readonly IModpackProviderRegistry _providerRegistry = providerRegistry;
    private readonly ModpackLoaderInstaller _loaderInstaller = loaderInstaller;
    private readonly ModpackServerConfigurator _serverConfigurator = serverConfigurator;
    private readonly ILogger<ModpackInstallationService> _logger = logger;

    /// <inheritdoc />
    public async Task<ModpackInstallResult> InstallAsync(
        GameServer server,
        ModpackInstallRequest request,
        IProgress<ModpackInstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Starting modpack installation: {ModpackName} from {Provider} on server {ServerId}",
            request.ModpackName, request.Provider, server.Id);

        try
        {
            // Phase 1: Preparation (0-5%)
            progress?.Report(ModpackInstallProgress.Initializing());

            var serverPath = server.InstallPath;
            if (string.IsNullOrEmpty(serverPath))
            {
                return ModpackInstallResult.Failed("Server install path is not configured");
            }

            // Create server directory if needed
            Directory.CreateDirectory(serverPath);

            _logger.LogDebug("Installing modpack to {Path}", serverPath);

            // Phase 2: Download modpack (5-40%)
            progress?.Report(new ModpackInstallProgress
            {
                Phase = ModpackInstallPhase.Downloading,
                Message = "Downloading modpack files...",
                Percentage = 5
            });

            var downloadResult = await DownloadModpackAsync(
                server, request, progress, ct).ConfigureAwait(false);

            if (!downloadResult.Success)
            {
                _logger.LogError("Failed to download modpack: {Error}", downloadResult.Error);
                return ModpackInstallResult.Failed($"Failed to download modpack: {downloadResult.Error}");
            }

            _logger.LogInformation("Modpack files downloaded to {Path}", downloadResult.ExtractedPath);

            // Use manifest info if available (more accurate than metadata from provider)
            var effectiveMinecraftVersion = downloadResult.ManifestMinecraftVersion ?? request.MinecraftVersion;
            var effectiveLoader = downloadResult.ManifestLoaderType ?? request.Loader;
            var effectiveLoaderVersion = downloadResult.ManifestLoaderVersion ?? request.LoaderVersion;

            if (downloadResult.ManifestLoaderVersion is not null)
            {
                _logger.LogInformation(
                    "Using loader version from manifest: {Loader} {Version}",
                    effectiveLoader, effectiveLoaderVersion);
            }

            // Phase 3: Install loader (40-60%)
            progress?.Report(ModpackInstallProgress.InstallingLoader(effectiveLoader.ToString()));

            var loaderResult = await InstallLoaderAsync(
                server, effectiveMinecraftVersion, effectiveLoader, effectiveLoaderVersion, progress, ct).ConfigureAwait(false);

            if (!loaderResult.Success)
            {
                _logger.LogError("Failed to install loader: {Error}", loaderResult.Error);
                await CleanupOnFailureAsync(serverPath).ConfigureAwait(false);
                return ModpackInstallResult.Failed($"Failed to install {request.Loader}: {loaderResult.Error}");
            }

            _logger.LogInformation("Loader {Loader} installed successfully", effectiveLoader);

            // Phase 4: Configure server (80-95%)
            progress?.Report(ModpackInstallProgress.Configuring());

            var startupCommand = _loaderInstaller.GetStartupCommand(
                server, effectiveLoader, effectiveMinecraftVersion, effectiveLoaderVersion);

            // Update request with effective values for configuration
            var effectiveRequest = request with
            {
                MinecraftVersion = effectiveMinecraftVersion,
                Loader = effectiveLoader,
                LoaderVersion = effectiveLoaderVersion
            };

            await _serverConfigurator.ConfigureAsync(
                server, effectiveRequest, startupCommand, ct).ConfigureAwait(false);

            // Also create JVM args file for Forge/NeoForge
            if (effectiveLoader is ModLoaderType.Forge or ModLoaderType.NeoForge)
            {
                var ramMb = request.RecommendedRamMb ?? 4096;
                await _serverConfigurator.CreateJvmArgsFileAsync(serverPath, ramMb, ct).ConfigureAwait(false);
            }

            _logger.LogInformation("Server configuration completed");

            // Phase 5: Finalize (95-100%)
            progress?.Report(new ModpackInstallProgress
            {
                Phase = ModpackInstallPhase.Configuring,
                Message = "Finalizing installation...",
                Percentage = 95
            });

            // Update server entity with modpack info (use effective values from manifest)
            server.InstalledModpack = new InstalledModpackInfo
            {
                Provider = request.Provider,
                ModpackId = request.ModpackId,
                ModpackName = request.ModpackName,
                VersionId = request.VersionId,
                VersionName = request.VersionName,
                MinecraftVersion = effectiveMinecraftVersion,
                LoaderType = effectiveLoader,
                LoaderVersion = effectiveLoaderVersion,
                ModCount = request.ModCount,
                InstalledAt = DateTime.UtcNow
            };

            server.InstalledVersion = effectiveMinecraftVersion;

            progress?.Report(ModpackInstallProgress.Completed());

            _logger.LogInformation(
                "Successfully installed modpack {ModpackName} v{Version} on server {ServerId}",
                request.ModpackName, request.VersionName ?? request.VersionId, server.Id);

            return ModpackInstallResult.Succeeded(
                serverPath,
                request.ModpackName,
                request.VersionName ?? request.VersionId,
                effectiveMinecraftVersion,
                effectiveLoader,
                effectiveLoaderVersion,
                request.ModCount ?? 0);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Modpack installation was cancelled");
            await CleanupOnFailureAsync(server.InstallPath).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during modpack installation");
            await CleanupOnFailureAsync(server.InstallPath).ConfigureAwait(false);
            return ModpackInstallResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> UninstallAsync(GameServer server, CancellationToken ct = default)
    {
        if (server.InstalledModpack is null)
        {
            _logger.LogWarning("No modpack installed on server {ServerId}", server.Id);
            return false;
        }

        _logger.LogInformation(
            "Uninstalling modpack {ModpackName} from server {ServerId}",
            server.InstalledModpack.ModpackName, server.Id);

        try
        {
            var serverPath = server.InstallPath;
            if (!string.IsNullOrEmpty(serverPath) && Directory.Exists(serverPath))
            {
                // Clear the directory but keep the directory itself
                foreach (var file in Directory.GetFiles(serverPath))
                {
                    ct.ThrowIfCancellationRequested();
                    File.Delete(file);
                }

                foreach (var dir in Directory.GetDirectories(serverPath))
                {
                    ct.ThrowIfCancellationRequested();
                    Directory.Delete(dir, recursive: true);
                }
            }

            // Clear modpack info
            server.InstalledModpack = null;
            server.InstalledVersion = null;

            _logger.LogInformation("Modpack uninstalled from server {ServerId}", server.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall modpack from server {ServerId}", server.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        GameServer server,
        string newVersionId,
        IProgress<ModpackInstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (server.InstalledModpack is null)
        {
            _logger.LogWarning("No modpack installed on server {ServerId}", server.Id);
            return false;
        }

        var installedModpack = server.InstalledModpack;

        _logger.LogInformation(
            "Updating modpack {ModpackName} from {OldVersion} to {NewVersion} on server {ServerId}",
            installedModpack.ModpackName, installedModpack.VersionId, newVersionId, server.Id);

        try
        {
            // Get the provider
            var provider = _providerRegistry.GetProvider(installedModpack.Provider);
            if (provider is null)
            {
                _logger.LogError("Provider {Provider} not found for update", installedModpack.Provider);
                return false;
            }

            // Get version details
            var versions = await provider.GetVersionsAsync(installedModpack.ModpackId, ct).ConfigureAwait(false);
            var newVersion = versions.FirstOrDefault(v => v.Id == newVersionId);

            if (newVersion is null)
            {
                _logger.LogError("Version {VersionId} not found", newVersionId);
                return false;
            }

            // Create install request from existing modpack info + new version
            var updateRequest = new ModpackInstallRequest
            {
                Provider = installedModpack.Provider,
                ModpackId = installedModpack.ModpackId,
                ModpackName = installedModpack.ModpackName,
                VersionId = newVersionId,
                VersionName = newVersion.Name,
                MinecraftVersion = newVersion.MinecraftVersion,
                Loader = newVersion.Loader,
                LoaderVersion = newVersion.LoaderVersion,
                RecommendedRamMb = installedModpack.ModCount.HasValue
                    ? (long)(installedModpack.ModCount.Value * 50 + 2048) // Rough estimate
                    : 4096
            };

            // Clear existing installation and reinstall
            await UninstallAsync(server, ct).ConfigureAwait(false);
            var result = await InstallAsync(server, updateRequest, progress, ct).ConfigureAwait(false);

            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update modpack on server {ServerId}", server.Id);
            return false;
        }
    }

    /// <summary>
    /// Downloads the modpack files from the provider.
    /// </summary>
    private async Task<ModpackDownloadResult> DownloadModpackAsync(
        GameServer server,
        ModpackInstallRequest request,
        IProgress<ModpackInstallProgress>? progress,
        CancellationToken ct)
    {
        var provider = _providerRegistry.GetProvider(request.Provider);
        if (provider is null)
        {
            _logger.LogError("Provider {Provider} not found", request.Provider);
            return ModpackDownloadResult.Failed($"Provider {request.Provider} not found or not enabled");
        }

        // Create progress adapter for download phase
        var downloadProgress = progress is not null
            ? new Progress<ModpackDownloadProgress>(p =>
            {
                // Map download progress (0-100) to overall progress (5-40)
                var scaledPercentage = 5 + (p.Percentage * 0.35);
                progress.Report(new ModpackInstallProgress
                {
                    Phase = ModpackInstallPhase.Downloading,
                    Message = p.Message ?? "Downloading modpack files...",
                    Percentage = scaledPercentage,
                    BytesDownloaded = p.BytesDownloaded,
                    TotalBytes = p.TotalBytes,
                    CurrentFile = p.CurrentFile
                });
            })
            : null;

        return await provider.DownloadServerFilesAsync(
            request.ModpackId,
            request.VersionId,
            server.InstallPath,
            downloadProgress,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Installs the mod loader for the modpack.
    /// </summary>
    private async Task<OfficialGames.Minecraft.Models.LoaderInstallResult> InstallLoaderAsync(
        GameServer server,
        string minecraftVersion,
        ModLoaderType loader,
        string? loaderVersion,
        IProgress<ModpackInstallProgress>? progress,
        CancellationToken ct)
    {
        if (loader == ModLoaderType.None)
        {
            _logger.LogInformation("No mod loader required for this modpack");
            return OfficialGames.Minecraft.Models.LoaderInstallResult.Succeeded(null, minecraftVersion);
        }

        return await _loaderInstaller.InstallLoaderAsync(
            server,
            loader,
            minecraftVersion,
            loaderVersion,
            progress,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Cleans up partial installation on failure.
    /// </summary>
    private async Task CleanupOnFailureAsync(string? serverPath)
    {
        if (string.IsNullOrEmpty(serverPath) || !Directory.Exists(serverPath))
            return;

        try
        {
            _logger.LogWarning("Cleaning up failed installation at {Path}", serverPath);

            // Give files time to be released
            await Task.Delay(500).ConfigureAwait(false);

            // Try to clean up, but don't fail if cleanup fails
            foreach (var file in Directory.GetFiles(serverPath))
            {
                try { File.Delete(file); }
                catch { /* ignore individual file cleanup errors */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up partial installation");
        }
    }
}
