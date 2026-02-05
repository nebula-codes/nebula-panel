using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.ModProviders.Modtale;

namespace NebulaPanel.Infrastructure.OfficialGames.Hytale;

/// <summary>
/// Service for orchestrating mod operations across providers for Hytale servers.
/// </summary>
public sealed class HytaleModService : IHytaleModService
{
    private const string ModsDirectory = "Server/mods";
    private const string DisabledSuffix = ".disabled";
    private const string HytaleGameSlug = "hytale";

    private readonly IEnumerable<IModProvider> _allProviders;
    private readonly ModtaleApiClient _modtaleApiClient;
    private readonly IServerModRepository _modRepository;
    private readonly ILogger<HytaleModService> _logger;

    // Cached list of Hytale-supporting providers (lazy initialized)
    private IReadOnlyList<IModProvider>? _hytaleProviders;

    public HytaleModService(
        IEnumerable<IModProvider> allProviders,
        ModtaleApiClient modtaleApiClient,
        IServerModRepository modRepository,
        ILogger<HytaleModService> logger)
    {
        _allProviders = allProviders;
        _modtaleApiClient = modtaleApiClient;
        _modRepository = modRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HytaleUnifiedModSearchResult> SearchAsync(
        ModSearchQuery query,
        CancellationToken ct = default)
    {
        var providers = await GetHytaleProvidersAsync(ct).ConfigureAwait(false);
        var providerInfo = new Dictionary<ModProviderType, HytaleProviderSearchInfo>();
        var allMods = new List<ModSearchItem>();
        var totalCount = 0;

        // Search all providers in parallel
        var searchTasks = providers.Select(async provider =>
        {
            try
            {
                var result = await provider.SearchAsync(query, ct).ConfigureAwait(false);
                return (provider.ProviderType, Result: result, Success: true, Error: (string?)null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search provider {Provider}", provider.ProviderType);
                return (provider.ProviderType, Result: (ModSearchResult?)null, Success: false, Error: ex.Message);
            }
        });

        var results = await Task.WhenAll(searchTasks).ConfigureAwait(false);

        foreach (var (providerType, result, success, error) in results)
        {
            if (success && result != null)
            {
                providerInfo[providerType] = new HytaleProviderSearchInfo
                {
                    ResultCount = result.TotalCount,
                    Available = true
                };

                allMods.AddRange(result.Mods);
                totalCount += result.TotalCount;
            }
            else
            {
                providerInfo[providerType] = new HytaleProviderSearchInfo
                {
                    ResultCount = 0,
                    Available = false,
                    Message = error ?? "Search failed"
                };
            }
        }

        // Sort combined results
        var sortedMods = SortMods(allMods, query.Sort);

        // Apply pagination to combined results
        var pagedMods = sortedMods
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new HytaleUnifiedModSearchResult
        {
            Mods = pagedMods,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            ProviderInfo = providerInfo
        };
    }

    /// <inheritdoc />
    public async Task<ModSearchResult> SearchProviderAsync(
        ModProviderType provider,
        ModSearchQuery query,
        CancellationToken ct = default)
    {
        var modProvider = await GetProviderAsync(provider, ct).ConfigureAwait(false);

        if (modProvider is null)
        {
            _logger.LogWarning("Provider {Provider} not found or doesn't support Hytale", provider);
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        return await modProvider.SearchAsync(query, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ModDetails?> GetDetailsAsync(
        ModProviderType provider,
        string modId,
        CancellationToken ct = default)
    {
        var modProvider = await GetProviderAsync(provider, ct).ConfigureAwait(false);

        if (modProvider is null)
        {
            _logger.LogWarning("Provider {Provider} not found or doesn't support Hytale", provider);
            return null;
        }

        return await modProvider.GetDetailsAsync(modId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModVersion>> GetVersionsAsync(
        ModProviderType provider,
        string modId,
        string? gameVersion = null,
        CancellationToken ct = default)
    {
        var modProvider = await GetProviderAsync(provider, ct).ConfigureAwait(false);

        if (modProvider is null)
        {
            _logger.LogWarning("Provider {Provider} not found or doesn't support Hytale", provider);
            return [];
        }

        return await modProvider.GetVersionsAsync(modId, gameVersion, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModSearchItem>> GetFeaturedAsync(
        int count = 20,
        CancellationToken ct = default)
    {
        var query = new ModSearchQuery(
            Query: null,
            GameSlug: HytaleGameSlug,
            GameVersion: null,
            ModLoader: null,
            Sort: ModSearchSort.Downloads,
            Page: 1,
            PageSize: count
        );

        var result = await SearchAsync(query, ct).ConfigureAwait(false);
        return result.Mods;
    }

    /// <inheritdoc />
    public async Task<HytaleModInstallResult> InstallModAsync(
        GameServer server,
        ModProviderType provider,
        string modId,
        string versionId,
        IProgress<HytaleModInstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            // Check if already installed
            if (await _modRepository.ExistsAsync(server.Id, provider, modId, ct).ConfigureAwait(false))
            {
                return HytaleModInstallResult.Failed("This mod is already installed on this server");
            }

            var modProvider = await GetProviderAsync(provider, ct).ConfigureAwait(false);
            if (modProvider is null)
            {
                return HytaleModInstallResult.Failed($"Provider {provider} not found or doesn't support Hytale");
            }

            // Get mod details
            var details = await modProvider.GetDetailsAsync(modId, ct).ConfigureAwait(false);
            if (details is null)
            {
                return HytaleModInstallResult.Failed($"Mod {modId} not found");
            }

            // Get version info
            var versions = await modProvider.GetVersionsAsync(modId, null, ct).ConfigureAwait(false);
            var version = versions.FirstOrDefault(v => v.Id == versionId);
            if (version is null)
            {
                return HytaleModInstallResult.Failed($"Version {versionId} not found");
            }

            progress?.Report(HytaleModInstallProgress.Downloading(details.Name, 10));

            // Prepare mods directory
            var modsPath = Path.Combine(server.InstallPath, ModsDirectory);
            Directory.CreateDirectory(modsPath);

            // Download the mod
            var downloadProgress = new Progress<DownloadProgress>(p =>
            {
                var percentage = 10 + (int)(p.Percentage * 0.8);
                progress?.Report(new HytaleModInstallProgress
                {
                    Phase = "Downloading",
                    Message = $"Downloading {details.Name}... {p.Percentage:F0}%",
                    Percentage = percentage
                });
            });

            var downloadResult = await modProvider.DownloadAsync(
                modId, versionId, modsPath, downloadProgress, ct).ConfigureAwait(false);

            if (!downloadResult.Success)
            {
                return HytaleModInstallResult.Failed(downloadResult.Error ?? "Download failed");
            }

            progress?.Report(HytaleModInstallProgress.Registering());

            // Get file info
            var filePath = downloadResult.FilePath!;
            var fileInfo = new FileInfo(filePath);

            // Create database record
            var serverMod = new ServerMod
            {
                Id = Guid.NewGuid(),
                ServerId = server.Id,
                ModId = modId,
                Name = details.Name,
                Version = version.Version,
                VersionId = versionId,
                Provider = provider,
                InstalledAt = DateTime.UtcNow,
                IsEnabled = true,
                LocalPath = Path.GetRelativePath(server.InstallPath, filePath),
                Author = details.Authors.FirstOrDefault()?.Name,
                IconUrl = details.IconUrl,
                FileSizeBytes = fileInfo.Length,
                FileHash = downloadResult.FileHash
            };

            await _modRepository.AddAsync(serverMod, ct).ConfigureAwait(false);

            progress?.Report(HytaleModInstallProgress.Complete(details.Name));

            _logger.LogInformation(
                "Successfully installed mod {ModName} ({ModId}) version {Version} to server {ServerId}",
                details.Name, modId, version.Version, server.Id);

            return HytaleModInstallResult.Successful(MapToInstalledModInfo(serverMod, null));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install mod {ModId} to server {ServerId}", modId, server.Id);
            return HytaleModInstallResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<bool> UninstallModAsync(
        GameServer server,
        Guid installedModId,
        CancellationToken ct = default)
    {
        var mod = await _modRepository.GetByIdAsync(installedModId, ct).ConfigureAwait(false);

        if (mod is null || mod.ServerId != server.Id)
        {
            _logger.LogWarning("Mod {ModId} not found or doesn't belong to server {ServerId}", installedModId, server.Id);
            return false;
        }

        // Delete the file
        if (!string.IsNullOrEmpty(mod.LocalPath))
        {
            var fullPath = Path.Combine(server.InstallPath, mod.LocalPath);
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Deleted mod file: {FilePath}", fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete mod file: {FilePath}", fullPath);
                }
            }
        }

        // Remove from database
        await _modRepository.DeleteAsync(installedModId, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Uninstalled mod {ModName} ({ModId}) from server {ServerId}",
            mod.Name, mod.ModId, server.Id);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SetModEnabledAsync(
        GameServer server,
        Guid installedModId,
        bool enabled,
        CancellationToken ct = default)
    {
        var mod = await _modRepository.GetByIdAsync(installedModId, ct).ConfigureAwait(false);

        if (mod is null || mod.ServerId != server.Id)
        {
            _logger.LogWarning("Mod {ModId} not found or doesn't belong to server {ServerId}", installedModId, server.Id);
            return false;
        }

        if (string.IsNullOrEmpty(mod.LocalPath))
        {
            _logger.LogWarning("Mod {ModId} has no local path", installedModId);
            return false;
        }

        var currentPath = Path.Combine(server.InstallPath, mod.LocalPath);

        string newPath;
        if (enabled)
        {
            // Remove .disabled suffix
            newPath = currentPath.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase)
                ? currentPath[..^DisabledSuffix.Length]
                : currentPath;
        }
        else
        {
            // Add .disabled suffix
            newPath = currentPath.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase)
                ? currentPath
                : currentPath + DisabledSuffix;
        }

        // Rename file if needed
        if (!currentPath.Equals(newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(currentPath))
        {
            try
            {
                File.Move(currentPath, newPath);
                mod.LocalPath = Path.GetRelativePath(server.InstallPath, newPath);
                _logger.LogInformation("Renamed mod file from {OldPath} to {NewPath}", currentPath, newPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rename mod file from {OldPath} to {NewPath}", currentPath, newPath);
                return false;
            }
        }

        mod.IsEnabled = enabled;
        mod.UpdatedAt = DateTime.UtcNow;
        await _modRepository.UpdateAsync(mod, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Set mod {ModName} ({ModId}) enabled={Enabled} on server {ServerId}",
            mod.Name, mod.ModId, enabled, server.Id);

        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HytaleModUpdateInfo>> CheckUpdatesAsync(
        GameServer server,
        CancellationToken ct = default)
    {
        var installedMods = await _modRepository.GetByServerIdAsync(server.Id, ct).ConfigureAwait(false);
        var updates = new List<HytaleModUpdateInfo>();

        // Group mods by provider for efficient batch checking
        var modsByProvider = installedMods.GroupBy(m => m.Provider);

        foreach (var providerGroup in modsByProvider)
        {
            var provider = await GetProviderAsync(providerGroup.Key, ct).ConfigureAwait(false);
            if (provider is null) continue;

            foreach (var mod in providerGroup)
            {
                try
                {
                    var versions = await provider.GetVersionsAsync(mod.ModId, null, ct).ConfigureAwait(false);
                    if (versions.Count == 0) continue;

                    // Get latest release version
                    var latestVersion = versions
                        .Where(v => v.VersionType == ModVersionType.Release)
                        .OrderByDescending(v => v.ReleasedAt)
                        .FirstOrDefault() ?? versions[0];

                    // Check if there's a newer version
                    if (latestVersion.Id != mod.VersionId && IsNewerVersion(latestVersion.Version, mod.Version ?? string.Empty))
                    {
                        updates.Add(new HytaleModUpdateInfo
                        {
                            InstalledModId = mod.Id,
                            ModName = mod.Name,
                            CurrentVersion = mod.Version ?? mod.VersionId ?? "Unknown",
                            LatestVersion = latestVersion.Version,
                            LatestVersionId = latestVersion.Id,
                            Changelog = latestVersion.Changelog,
                            ReleasedAt = latestVersion.ReleasedAt
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check updates for mod {ModId}", mod.ModId);
                }
            }
        }

        return updates;
    }

    /// <inheritdoc />
    public async Task<HytaleModInstallResult> UpdateModAsync(
        GameServer server,
        Guid installedModId,
        string newVersionId,
        IProgress<HytaleModInstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var mod = await _modRepository.GetByIdAsync(installedModId, ct).ConfigureAwait(false);

        if (mod is null || mod.ServerId != server.Id)
        {
            return HytaleModInstallResult.Failed("Mod not found or doesn't belong to this server");
        }

        var provider = await GetProviderAsync(mod.Provider, ct).ConfigureAwait(false);
        if (provider is null)
        {
            return HytaleModInstallResult.Failed($"Provider {mod.Provider} not available");
        }

        // Get the new version info
        var versions = await provider.GetVersionsAsync(mod.ModId, null, ct).ConfigureAwait(false);
        var newVersion = versions.FirstOrDefault(v => v.Id == newVersionId);
        if (newVersion is null)
        {
            return HytaleModInstallResult.Failed($"Version {newVersionId} not found");
        }

        progress?.Report(new HytaleModInstallProgress
        {
            Phase = "Preparing",
            Message = $"Updating {mod.Name}...",
            Percentage = 5
        });

        // Delete old file
        if (!string.IsNullOrEmpty(mod.LocalPath))
        {
            var oldPath = Path.Combine(server.InstallPath, mod.LocalPath);
            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old mod file: {Path}", oldPath);
                }
            }
        }

        // Download new version
        var modsPath = Path.Combine(server.InstallPath, ModsDirectory);
        Directory.CreateDirectory(modsPath);

        var downloadProgress = new Progress<DownloadProgress>(p =>
        {
            var percentage = 10 + (int)(p.Percentage * 0.8);
            progress?.Report(new HytaleModInstallProgress
            {
                Phase = "Downloading",
                Message = $"Downloading {mod.Name} v{newVersion.Version}... {p.Percentage:F0}%",
                Percentage = percentage
            });
        });

        var downloadResult = await provider.DownloadAsync(
            mod.ModId, newVersionId, modsPath, downloadProgress, ct).ConfigureAwait(false);

        if (!downloadResult.Success)
        {
            return HytaleModInstallResult.Failed(downloadResult.Error ?? "Download failed");
        }

        progress?.Report(HytaleModInstallProgress.Registering());

        // Update database record
        var filePath = downloadResult.FilePath!;
        var fileInfo = new FileInfo(filePath);

        mod.Version = newVersion.Version;
        mod.VersionId = newVersionId;
        mod.LocalPath = Path.GetRelativePath(server.InstallPath, filePath);
        mod.FileSizeBytes = fileInfo.Length;
        mod.FileHash = downloadResult.FileHash;
        mod.UpdatedAt = DateTime.UtcNow;
        // Re-enable if it was disabled
        if (!mod.IsEnabled && !mod.LocalPath.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
        {
            mod.IsEnabled = true;
        }

        await _modRepository.UpdateAsync(mod, ct).ConfigureAwait(false);

        progress?.Report(HytaleModInstallProgress.Complete(mod.Name));

        _logger.LogInformation(
            "Updated mod {ModName} ({ModId}) to version {Version} on server {ServerId}",
            mod.Name, mod.ModId, newVersion.Version, server.Id);

        return HytaleModInstallResult.Successful(MapToInstalledModInfo(mod, null));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HytaleInstalledModInfo>> GetInstalledModsAsync(
        GameServer server,
        CancellationToken ct = default)
    {
        var installedMods = await _modRepository.GetByServerIdAsync(server.Id, ct).ConfigureAwait(false);

        // Check for updates
        var updates = await CheckUpdatesAsync(server, ct).ConfigureAwait(false);
        var updateLookup = updates.ToDictionary(u => u.InstalledModId);

        return installedMods
            .Select(m => MapToInstalledModInfo(m, updateLookup.GetValueOrDefault(m.Id)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetTagsAsync(CancellationToken ct = default)
    {
        try
        {
            var tags = await _modtaleApiClient.GetTagsAsync(ct).ConfigureAwait(false);
            return tags;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch tags from Modtale");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetGameVersionsAsync(CancellationToken ct = default)
    {
        try
        {
            var versions = await _modtaleApiClient.GetGameVersionsAsync(ct).ConfigureAwait(false);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch game versions from Modtale");
            return [];
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetClassificationsAsync(CancellationToken ct = default)
    {
        // Hytale mod classifications are fixed
        IReadOnlyList<string> classifications = new[]
        {
            ModtaleClassification.Plugin,
            ModtaleClassification.Data,
            ModtaleClassification.Art,
            ModtaleClassification.Save,
            ModtaleClassification.Modpack
        };

        return Task.FromResult(classifications);
    }

    #region Private Helper Methods

    private async Task<IReadOnlyList<IModProvider>> GetHytaleProvidersAsync(CancellationToken ct)
    {
        if (_hytaleProviders is not null)
        {
            return _hytaleProviders;
        }

        var providers = new List<IModProvider>();
        foreach (var provider in _allProviders)
        {
            if (await provider.SupportsGameAsync(HytaleGameSlug, ct).ConfigureAwait(false))
            {
                providers.Add(provider);
            }
        }

        _hytaleProviders = providers;
        return providers;
    }

    private async Task<IModProvider?> GetProviderAsync(ModProviderType providerType, CancellationToken ct)
    {
        var providers = await GetHytaleProvidersAsync(ct).ConfigureAwait(false);
        return providers.FirstOrDefault(p => p.ProviderType == providerType);
    }

    private static IReadOnlyList<ModSearchItem> SortMods(List<ModSearchItem> mods, ModSearchSort sort)
    {
        return sort switch
        {
            ModSearchSort.Downloads => mods.OrderByDescending(m => m.Downloads).ToList(),
            ModSearchSort.Updated => mods.OrderByDescending(m => m.UpdatedAt ?? DateTime.MinValue).ToList(),
            ModSearchSort.Created => mods.OrderBy(m => m.UpdatedAt ?? DateTime.MaxValue).ToList(),
            ModSearchSort.Name => mods.OrderBy(m => m.Name).ToList(),
            _ => mods // Relevance - keep original order from provider
        };
    }

    private HytaleInstalledModInfo MapToInstalledModInfo(ServerMod mod, HytaleModUpdateInfo? update)
    {
        var filePath = mod.LocalPath ?? string.Empty;
        var fileName = Path.GetFileName(filePath);

        return new HytaleInstalledModInfo
        {
            Id = mod.Id,
            ModId = mod.ModId,
            Name = mod.Name,
            Author = mod.Author,
            InstalledVersion = mod.Version ?? "Unknown",
            InstalledVersionId = mod.VersionId,
            Provider = mod.Provider,
            IconUrl = mod.IconUrl,
            FilePath = filePath,
            FileName = fileName,
            FileSizeBytes = mod.FileSizeBytes ?? 0,
            InstalledAt = mod.InstalledAt,
            IsEnabled = mod.IsEnabled,
            UpdateAvailable = update is not null,
            LatestVersion = update?.LatestVersion,
            LatestVersionId = update?.LatestVersionId,
            LatestVersionDate = update?.ReleasedAt
        };
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        // Handle null/empty cases
        if (string.IsNullOrWhiteSpace(latest)) return false;
        if (string.IsNullOrWhiteSpace(current)) return true;

        // Try semantic version comparison
        if (Version.TryParse(NormalizeVersion(latest), out var v1) &&
            Version.TryParse(NormalizeVersion(current), out var v2))
        {
            return v1 > v2;
        }

        // Fallback to string comparison
        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static string NormalizeVersion(string version)
    {
        // Remove common prefixes like 'v'
        if (version.StartsWith('v') || version.StartsWith('V'))
        {
            version = version[1..];
        }

        // Take only the numeric parts (e.g., "1.2.3-beta" -> "1.2.3")
        var dashIndex = version.IndexOf('-');
        if (dashIndex > 0)
        {
            version = version[..dashIndex];
        }

        return version;
    }

    #endregion
}
