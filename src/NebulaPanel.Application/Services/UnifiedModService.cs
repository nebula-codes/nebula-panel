using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Unified service for mod management that aggregates results from multiple mod providers.
/// </summary>
public class UnifiedModService : IUnifiedModService
{
    private const string DisabledSuffix = ".disabled";

    private readonly IEnumerable<IModProvider> _providers;
    private readonly IServerModRepository _serverModRepository;
    private readonly ILogger<UnifiedModService> _logger;

    public UnifiedModService(
        IEnumerable<IModProvider> providers,
        IServerModRepository serverModRepository,
        ILogger<UnifiedModService> logger)
    {
        _providers = providers;
        _serverModRepository = serverModRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UnifiedModSearchResult> SearchAsync(
        GameServer server,
        ModSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "UnifiedModService.SearchAsync: server={ServerId}, game={GameSlug}, query='{Query}'",
            server.Id, server.Game?.Slug, query.Query);

        var enabledProviders = GetEnabledProvidersForServer(server);

        _logger.LogInformation(
            "Found {ProviderCount} enabled providers for server",
            enabledProviders.Count);

        if (enabledProviders.Count == 0)
        {
            _logger.LogWarning("No enabled providers found for server {ServerId}", server.Id);
            return new UnifiedModSearchResult([], new Dictionary<ModProviderType, int>(), 0, false);
        }

        var allMods = new List<ModSearchItem>();
        var resultCountByProvider = new Dictionary<ModProviderType, int>();

        // Search all enabled providers in parallel
        var searchTasks = enabledProviders.Select(async provider =>
        {
            try
            {
                var result = await provider.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                return (provider.ProviderType, result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to search provider {Provider} for query '{Query}'",
                    provider.ProviderType, query.Query);
                return (provider.ProviderType, new ModSearchResult([], 0, query.Page, query.PageSize, 0));
            }
        });

        var results = await Task.WhenAll(searchTasks).ConfigureAwait(false);

        foreach (var (providerType, result) in results)
        {
            resultCountByProvider[providerType] = result.TotalCount;
            allMods.AddRange(result.Mods);
        }

        // Deduplicate by mod ID (prefer higher priority provider)
        var seenModIds = new HashSet<string>();
        var deduplicatedMods = new List<ModSearchItem>();

        // Sort by provider priority then by downloads
        var sortedMods = allMods
            .OrderBy(m => GetProviderPriority(server, m.Provider))
            .ThenByDescending(m => m.Downloads);

        foreach (var mod in sortedMods)
        {
            var key = $"{mod.Provider}:{mod.Id}";
            if (seenModIds.Add(key))
            {
                deduplicatedMods.Add(mod);
            }
        }

        // Re-sort by the original sort criteria
        var finalMods = query.Sort switch
        {
            ModSearchSort.Downloads => deduplicatedMods.OrderByDescending(m => m.Downloads).ToList(),
            ModSearchSort.Updated => deduplicatedMods.OrderByDescending(m => m.UpdatedAt).ToList(),
            ModSearchSort.Created => deduplicatedMods.OrderByDescending(m => m.UpdatedAt).ToList(), // Use UpdatedAt as proxy
            ModSearchSort.Name => deduplicatedMods.OrderBy(m => m.Name).ToList(),
            _ => deduplicatedMods // Keep relevance ordering
        };

        var totalCount = resultCountByProvider.Values.Sum();
        var hasMoreResults = results.Any(r => r.Item2.Page < r.Item2.TotalPages);

        return new UnifiedModSearchResult(
            Mods: finalMods,
            ResultCountByProvider: resultCountByProvider,
            TotalCount: totalCount,
            HasMoreResults: hasMoreResults
        );
    }

    /// <inheritdoc />
    public async Task<ModSearchResult> SearchProviderAsync(
        GameServer server,
        ModProviderType provider,
        ModSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SearchProviderAsync: provider={Provider}, query='{Query}'",
            provider, query.Query);

        var modProvider = _providers.FirstOrDefault(p => p.ProviderType == provider);

        if (modProvider is null)
        {
            _logger.LogWarning("Provider {Provider} not found in DI. Available: [{Available}]",
                provider, string.Join(", ", _providers.Select(p => p.ProviderType)));
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        try
        {
            _logger.LogInformation("Calling {Provider}.SearchAsync", provider);
            var result = await modProvider.SearchAsync(query, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Provider {Provider} returned {Count} results",
                provider, result.Mods.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search provider {Provider}", provider);
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }
    }

    /// <inheritdoc />
    public async Task<ModDetails?> GetDetailsAsync(
        ModProviderType provider,
        string modId,
        CancellationToken cancellationToken = default)
    {
        var modProvider = _providers.FirstOrDefault(p => p.ProviderType == provider);

        if (modProvider is null)
        {
            _logger.LogWarning("Provider {Provider} not found", provider);
            return null;
        }

        try
        {
            return await modProvider.GetDetailsAsync(modId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get details for mod {ModId} from {Provider}", modId, provider);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModVersion>> GetVersionsAsync(
        ModProviderType provider,
        string modId,
        string? gameVersion,
        CancellationToken cancellationToken = default)
    {
        var modProvider = _providers.FirstOrDefault(p => p.ProviderType == provider);

        if (modProvider is null)
        {
            _logger.LogWarning("Provider {Provider} not found", provider);
            return [];
        }

        try
        {
            return await modProvider.GetVersionsAsync(modId, gameVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get versions for mod {ModId} from {Provider}", modId, provider);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<InstallResult> InstallAsync(
        GameServer server,
        ModProviderType provider,
        string modId,
        string? versionId,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var modProvider = _providers.FirstOrDefault(p => p.ProviderType == provider);

        if (modProvider is null)
        {
            return new InstallResult(false, null, $"Provider {provider} not found", 0);
        }

        // Check if already installed
        if (await _serverModRepository.ExistsAsync(server.Id, provider, modId, cancellationToken).ConfigureAwait(false))
        {
            return new InstallResult(false, null, "Mod is already installed on this server", 0);
        }

        try
        {
            // Get mod details
            var details = await modProvider.GetDetailsAsync(modId, cancellationToken).ConfigureAwait(false);
            if (details is null)
            {
                return new InstallResult(false, null, "Mod not found", 0);
            }

            // Get versions to find the target version
            var gameVersion = ParseGameVersion(server.InstalledVersion);
            _logger.LogInformation("Installing mod {ModId}, parsed game version: {GameVersion} (from '{InstalledVersion}')",
                modId, gameVersion, server.InstalledVersion);

            var versions = await modProvider.GetVersionsAsync(
                modId,
                gameVersion,
                cancellationToken).ConfigureAwait(false);

            if (versions.Count == 0)
            {
                return new InstallResult(false, null, $"No compatible versions found for game version {gameVersion}", 0);
            }

            // Use specified version or latest
            ModVersion? targetVersion;
            if (versionId is not null)
            {
                targetVersion = versions.FirstOrDefault(v => v.Id == versionId);
                if (targetVersion is null)
                {
                    return new InstallResult(false, null, $"Version {versionId} not found", 0);
                }
            }
            else
            {
                targetVersion = versions[0]; // First is latest
            }

            // Determine mod install path
            var modInstallPath = GetModInstallPath(server);
            if (string.IsNullOrEmpty(modInstallPath))
            {
                return new InstallResult(false, null, "Could not determine mod installation path", 0);
            }

            var fullInstallPath = Path.Combine(server.InstallPath, modInstallPath);

            // Download the mod
            var downloadResult = await modProvider.DownloadAsync(
                modId,
                targetVersion.Id,
                fullInstallPath,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (!downloadResult.Success)
            {
                return new InstallResult(false, null, downloadResult.Error ?? "Download failed", 0);
            }

            // Get file info
            var primaryFile = targetVersion.Files.FirstOrDefault(f => f.IsPrimary) ?? targetVersion.Files.FirstOrDefault();

            // Create database record
            var serverMod = new ServerMod
            {
                Id = Guid.NewGuid(),
                ServerId = server.Id,
                ModId = modId,
                Name = details.Name,
                Version = targetVersion.Version,
                VersionId = targetVersion.Id,
                Provider = provider,
                InstalledAt = DateTime.UtcNow,
                IsEnabled = true,
                LocalPath = downloadResult.FilePath is not null
                    ? Path.GetRelativePath(server.InstallPath, downloadResult.FilePath)
                    : null,
                IconUrl = details.IconUrl,
                FileSizeBytes = primaryFile?.SizeBytes,
                FileHash = downloadResult.FileHash
            };

            await _serverModRepository.AddAsync(serverMod, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Installed mod {ModName} ({ModId}) version {Version} on server {ServerId}",
                details.Name, modId, targetVersion.Version, server.Id);

            // Handle dependencies
            var installedDependencyCount = 0;
            var requiredDependencies = targetVersion.Dependencies
                .Where(d => d.Type == ModDependencyType.Required)
                .ToList();

            if (requiredDependencies.Count > 0)
            {
                var visitedMods = new HashSet<string> { $"{provider}:{modId}" };
                installedDependencyCount = await InstallDependenciesAsync(
                    server,
                    provider,
                    requiredDependencies,
                    visitedMods,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            return new InstallResult(
                true,
                MapToDto(serverMod),
                null,
                installedDependencyCount
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install mod {ModId} on server {ServerId}", modId, server.Id);
            return new InstallResult(false, null, $"Installation failed: {ex.Message}", 0);
        }
    }

    /// <summary>
    /// Installs required dependencies for a mod, handling circular dependency detection.
    /// </summary>
    private async Task<int> InstallDependenciesAsync(
        GameServer server,
        ModProviderType providerType,
        IReadOnlyList<ModDependency> dependencies,
        HashSet<string> visitedMods,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var installedCount = 0;

        foreach (var dependency in dependencies)
        {
            var depKey = $"{providerType}:{dependency.ModId}";

            // Skip if already visited (circular dependency protection)
            if (!visitedMods.Add(depKey))
            {
                _logger.LogDebug(
                    "Skipping dependency {ModId} - already in install chain (circular dependency protection)",
                    dependency.ModId);
                continue;
            }

            // Skip if already installed on this server
            if (await _serverModRepository.ExistsAsync(
                server.Id, providerType, dependency.ModId, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogDebug(
                    "Skipping dependency {ModId} - already installed on server {ServerId}",
                    dependency.ModId, server.Id);
                continue;
            }

            try
            {
                _logger.LogInformation(
                    "Installing required dependency {ModName} ({ModId}) for server {ServerId}",
                    dependency.ModName ?? dependency.ModId, dependency.ModId, server.Id);

                var provider = _providers.FirstOrDefault(p => p.ProviderType == providerType);
                if (provider is null)
                {
                    _logger.LogWarning(
                        "Cannot install dependency {ModId} - provider {Provider} not available",
                        dependency.ModId, providerType);
                    continue;
                }

                // Get mod details
                var depDetails = await provider.GetDetailsAsync(dependency.ModId, cancellationToken).ConfigureAwait(false);
                if (depDetails is null)
                {
                    _logger.LogWarning(
                        "Cannot install dependency {ModId} - mod not found on {Provider}",
                        dependency.ModId, providerType);
                    continue;
                }

                // Get versions
                var gameVersion = ParseGameVersion(server.InstalledVersion);
                var versions = await provider.GetVersionsAsync(
                    dependency.ModId,
                    gameVersion,
                    cancellationToken).ConfigureAwait(false);

                if (versions.Count == 0)
                {
                    _logger.LogWarning(
                        "Cannot install dependency {ModId} - no compatible versions for game version {GameVersion}",
                        dependency.ModId, gameVersion);
                    continue;
                }

                // Use specified version or latest
                ModVersion? targetVersion;
                if (dependency.VersionId is not null)
                {
                    targetVersion = versions.FirstOrDefault(v => v.Id == dependency.VersionId);
                    if (targetVersion is null)
                    {
                        _logger.LogWarning(
                            "Specified version {VersionId} not found for dependency {ModId}, using latest",
                            dependency.VersionId, dependency.ModId);
                        targetVersion = versions[0];
                    }
                }
                else
                {
                    targetVersion = versions[0];
                }

                // Download the dependency
                var modInstallPath = GetModInstallPath(server);
                var fullInstallPath = Path.Combine(server.InstallPath, modInstallPath ?? "mods");

                var downloadResult = await provider.DownloadAsync(
                    dependency.ModId,
                    targetVersion.Id,
                    fullInstallPath,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                if (!downloadResult.Success)
                {
                    _logger.LogWarning(
                        "Failed to download dependency {ModId}: {Error}",
                        dependency.ModId, downloadResult.Error);
                    continue;
                }

                // Create database record
                var primaryFile = targetVersion.Files.FirstOrDefault(f => f.IsPrimary) ?? targetVersion.Files.FirstOrDefault();
                var serverMod = new ServerMod
                {
                    Id = Guid.NewGuid(),
                    ServerId = server.Id,
                    ModId = dependency.ModId,
                    Name = depDetails.Name,
                    Version = targetVersion.Version,
                    VersionId = targetVersion.Id,
                    Provider = providerType,
                    InstalledAt = DateTime.UtcNow,
                    IsEnabled = true,
                    LocalPath = downloadResult.FilePath is not null
                        ? Path.GetRelativePath(server.InstallPath, downloadResult.FilePath)
                        : null,
                    IconUrl = depDetails.IconUrl,
                    FileSizeBytes = primaryFile?.SizeBytes,
                    FileHash = downloadResult.FileHash
                };

                await _serverModRepository.AddAsync(serverMod, cancellationToken).ConfigureAwait(false);
                installedCount++;

                _logger.LogInformation(
                    "Installed dependency {ModName} ({ModId}) version {Version} on server {ServerId}",
                    depDetails.Name, dependency.ModId, targetVersion.Version, server.Id);

                // Recursively install nested dependencies
                var nestedDependencies = targetVersion.Dependencies
                    .Where(d => d.Type == ModDependencyType.Required)
                    .ToList();

                if (nestedDependencies.Count > 0)
                {
                    var nestedCount = await InstallDependenciesAsync(
                        server,
                        providerType,
                        nestedDependencies,
                        visitedMods,
                        progress,
                        cancellationToken).ConfigureAwait(false);
                    installedCount += nestedCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to install dependency {ModId} on server {ServerId}",
                    dependency.ModId, server.Id);
                // Continue with other dependencies even if one fails
            }
        }

        return installedCount;
    }

    /// <inheritdoc />
    public async Task<InstallResult> UpdateAsync(
        GameServer server,
        Guid installedModId,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var installedMod = await _serverModRepository.GetByIdAsync(installedModId, cancellationToken).ConfigureAwait(false);

        if (installedMod is null)
        {
            return new InstallResult(false, null, "Installed mod not found", 0);
        }

        if (installedMod.ServerId != server.Id)
        {
            return new InstallResult(false, null, "Mod does not belong to this server", 0);
        }

        var modProvider = _providers.FirstOrDefault(p => p.ProviderType == installedMod.Provider);

        if (modProvider is null)
        {
            return new InstallResult(false, null, $"Provider {installedMod.Provider} not found", 0);
        }

        try
        {
            // Get available versions
            var gameVersion = ParseGameVersion(server.InstalledVersion);
            var versions = await modProvider.GetVersionsAsync(
                installedMod.ModId,
                gameVersion,
                cancellationToken).ConfigureAwait(false);

            if (versions.Count == 0)
            {
                return new InstallResult(false, null, $"No compatible versions found for game version {gameVersion}", 0);
            }

            var latestVersion = versions[0];

            // Check if already on latest
            if (latestVersion.Id == installedMod.VersionId)
            {
                return new InstallResult(false, null, "Already on the latest version", 0);
            }

            // Delete old file if it exists
            if (!string.IsNullOrEmpty(installedMod.LocalPath))
            {
                var oldFilePath = Path.Combine(server.InstallPath, installedMod.LocalPath);
                if (File.Exists(oldFilePath))
                {
                    File.Delete(oldFilePath);
                }
            }

            // Download new version
            var modInstallPath = GetModInstallPath(server);
            var fullInstallPath = Path.Combine(server.InstallPath, modInstallPath ?? "mods");

            var downloadResult = await modProvider.DownloadAsync(
                installedMod.ModId,
                latestVersion.Id,
                fullInstallPath,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (!downloadResult.Success)
            {
                return new InstallResult(false, null, downloadResult.Error ?? "Download failed", 0);
            }

            // Update database record
            var primaryFile = latestVersion.Files.FirstOrDefault(f => f.IsPrimary) ?? latestVersion.Files.FirstOrDefault();

            installedMod.Version = latestVersion.Version;
            installedMod.VersionId = latestVersion.Id;
            installedMod.UpdatedAt = DateTime.UtcNow;
            installedMod.LocalPath = downloadResult.FilePath is not null
                ? Path.GetRelativePath(server.InstallPath, downloadResult.FilePath)
                : null;
            installedMod.FileSizeBytes = primaryFile?.SizeBytes;
            installedMod.FileHash = downloadResult.FileHash;

            await _serverModRepository.UpdateAsync(installedMod, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Updated mod {ModName} to version {Version} on server {ServerId}",
                installedMod.Name, latestVersion.Version, server.Id);

            return new InstallResult(true, MapToDto(installedMod), null, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update mod {ModId} on server {ServerId}", installedModId, server.Id);
            return new InstallResult(false, null, $"Update failed: {ex.Message}", 0);
        }
    }

    /// <inheritdoc />
    public async Task<Result> UninstallAsync(
        GameServer server,
        Guid installedModId,
        CancellationToken cancellationToken = default)
    {
        var installedMod = await _serverModRepository.GetByIdAsync(installedModId, cancellationToken).ConfigureAwait(false);

        if (installedMod is null)
        {
            return Result.Failure("Installed mod not found");
        }

        if (installedMod.ServerId != server.Id)
        {
            return Result.Failure("Mod does not belong to this server");
        }

        try
        {
            // Delete the file if it exists
            if (!string.IsNullOrEmpty(installedMod.LocalPath))
            {
                var filePath = Path.Combine(server.InstallPath, installedMod.LocalPath);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            // Delete database record
            await _serverModRepository.DeleteAsync(installedModId, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Uninstalled mod {ModName} from server {ServerId}",
                installedMod.Name, server.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall mod {ModId} from server {ServerId}", installedModId, server.Id);
            return Result.Failure($"Uninstall failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServerModDto>> GetInstalledAsync(
        GameServer server,
        CancellationToken cancellationToken = default)
    {
        var installedMods = await _serverModRepository.GetByServerIdAsync(server.Id, cancellationToken).ConfigureAwait(false);

        return installedMods.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<ServerModDto>> ToggleAsync(
        GameServer server,
        Guid installedModId,
        bool? enabled = null,
        CancellationToken cancellationToken = default)
    {
        var installedMod = await _serverModRepository.GetByIdAsync(installedModId, cancellationToken).ConfigureAwait(false);

        if (installedMod is null)
        {
            return Result.Failure<ServerModDto>("Installed mod not found");
        }

        if (installedMod.ServerId != server.Id)
        {
            return Result.Failure<ServerModDto>("Mod does not belong to this server");
        }

        // Determine new state: explicit value or toggle current state
        var newEnabledState = enabled ?? !installedMod.IsEnabled;

        // If already in the desired state, just return current data
        if (installedMod.IsEnabled == newEnabledState)
        {
            return Result.Success(MapToDto(installedMod));
        }

        try
        {
            // Handle file renaming if we have a local path
            if (!string.IsNullOrEmpty(installedMod.LocalPath))
            {
                var currentPath = Path.Combine(server.InstallPath, installedMod.LocalPath);
                string newPath;
                string newLocalPath;

                if (newEnabledState)
                {
                    // Enabling: remove .disabled suffix
                    if (installedMod.LocalPath.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        newLocalPath = installedMod.LocalPath[..^DisabledSuffix.Length];
                        newPath = Path.Combine(server.InstallPath, newLocalPath);
                    }
                    else
                    {
                        // File doesn't have disabled suffix, just update state
                        newLocalPath = installedMod.LocalPath;
                        newPath = currentPath;
                    }
                }
                else
                {
                    // Disabling: add .disabled suffix
                    if (!installedMod.LocalPath.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        newLocalPath = installedMod.LocalPath + DisabledSuffix;
                        newPath = Path.Combine(server.InstallPath, newLocalPath);
                    }
                    else
                    {
                        // File already has disabled suffix, just update state
                        newLocalPath = installedMod.LocalPath;
                        newPath = currentPath;
                    }
                }

                // Rename file if paths are different and source exists
                if (currentPath != newPath && File.Exists(currentPath))
                {
                    // Ensure target doesn't exist
                    if (File.Exists(newPath))
                    {
                        return Result.Failure<ServerModDto>($"Target file already exists: {newLocalPath}");
                    }

                    File.Move(currentPath, newPath);
                    installedMod.LocalPath = newLocalPath;

                    _logger.LogInformation(
                        "Renamed mod file from {OldPath} to {NewPath}",
                        currentPath, newPath);
                }
            }

            // Update database record
            installedMod.IsEnabled = newEnabledState;
            installedMod.UpdatedAt = DateTime.UtcNow;

            await _serverModRepository.UpdateAsync(installedMod, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Toggled mod {ModName} to {State} on server {ServerId}",
                installedMod.Name, newEnabledState ? "enabled" : "disabled", server.Id);

            return Result.Success(MapToDto(installedMod));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle mod {ModId} on server {ServerId}", installedModId, server.Id);
            return Result.Failure<ServerModDto>($"Failed to toggle mod: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModUpdateInfo>> CheckAllUpdatesAsync(
        GameServer server,
        CancellationToken cancellationToken = default)
    {
        var installedMods = await _serverModRepository.GetByServerIdAsync(server.Id, cancellationToken).ConfigureAwait(false);

        if (installedMods.Count == 0)
        {
            return [];
        }

        var updates = new List<ModUpdateInfo>();

        // Group by provider for efficient checking
        var modsByProvider = installedMods
            .GroupBy(m => m.Provider)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (providerType, mods) in modsByProvider)
        {
            var provider = _providers.FirstOrDefault(p => p.ProviderType == providerType);
            if (provider is null)
            {
                continue;
            }

            try
            {
                var installedInfos = mods.Select(m => new InstalledModInfo(
                    m.ModId,
                    m.VersionId ?? string.Empty,
                    m.Provider
                ));

                var providerUpdates = await provider.CheckUpdatesAsync(
                    installedInfos,
                    server.InstalledVersion,
                    cancellationToken).ConfigureAwait(false);

                // Map to include the actual installed mod IDs
                foreach (var update in providerUpdates)
                {
                    var installedMod = mods.FirstOrDefault(m => m.ModId == update.ModName || m.Name == update.ModName);
                    if (installedMod is not null)
                    {
                        updates.Add(update with { InstalledModId = installedMod.Id });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to check updates from provider {Provider}",
                    providerType);
            }
        }

        return updates;
    }

    /// <inheritdoc />
    public IReadOnlyList<ModProviderInfo> GetProvidersForGame(Game game)
    {
        if (!game.SupportsMods)
        {
            return [];
        }

        var gameProviders = game.ModProviders
            .Where(p => p.Enabled)
            .OrderBy(p => p.Priority)
            .ToList();

        return gameProviders.Select(config =>
        {
            var provider = _providers.FirstOrDefault(p => p.ProviderType == config.Provider);
            return new ModProviderInfo(
                config.Provider,
                provider?.DisplayName ?? config.Provider.ToString(),
                provider?.IconUrl,
                config.Enabled,
                config.Priority
            );
        }).ToList();
    }

    private List<IModProvider> GetEnabledProvidersForServer(GameServer server)
    {
        var game = server.Game;
        if (game is null)
        {
            _logger.LogWarning("Server {ServerId} has no game loaded", server.Id);
            return [];
        }

        if (!game.SupportsMods)
        {
            _logger.LogWarning("Game {GameSlug} does not support mods", game.Slug);
            return [];
        }

        _logger.LogInformation(
            "Game {GameSlug} has {ModProviderCount} configured mod providers, SupportsMods={SupportsMods}",
            game.Slug, game.ModProviders.Count, game.SupportsMods);

        var enabledProviderTypes = game.ModProviders
            .Where(p => p.Enabled)
            .Select(p => p.Provider)
            .ToHashSet();

        _logger.LogInformation(
            "Enabled provider types: [{Types}]",
            string.Join(", ", enabledProviderTypes));

        _logger.LogInformation(
            "Available providers in DI: [{Providers}]",
            string.Join(", ", _providers.Select(p => p.ProviderType)));

        var matchedProviders = _providers
            .Where(p => enabledProviderTypes.Contains(p.ProviderType))
            .ToList();

        _logger.LogInformation("Matched {Count} providers", matchedProviders.Count);

        return matchedProviders;
    }

    private static int GetProviderPriority(GameServer server, ModProviderType providerType)
    {
        var config = server.Game?.ModProviders.FirstOrDefault(p => p.Provider == providerType);
        return config?.Priority ?? 100;
    }

    private static string? GetModInstallPath(GameServer server)
    {
        // Try to find the mod install path from game configuration
        var modConfig = server.Game?.ModProviders.FirstOrDefault();
        if (modConfig is not null && !string.IsNullOrEmpty(modConfig.ModInstallPath))
        {
            return modConfig.ModInstallPath;
        }

        // Default paths based on game
        if (server.Game?.Slug?.Equals("minecraft", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "mods";
        }

        return "mods";
    }

    /// <summary>
    /// Parses the installed version string to extract just the game version number.
    /// E.g., "forge-1.20.1-recommended" -> "1.20.1"
    /// </summary>
    private static string? ParseGameVersion(string? installedVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion))
            return null;

        // Extract version number (patterns like 1.20.1, 1.7.10, 1.20)
        var versionMatch = Regex.Match(installedVersion, @"\b(\d+\.\d+(?:\.\d+)?)\b");
        return versionMatch.Success ? versionMatch.Groups[1].Value : null;
    }

    private static ServerModDto MapToDto(ServerMod mod)
    {
        return new ServerModDto(
            mod.Id,
            mod.ServerId,
            mod.ModId,
            mod.Name,
            mod.Version,
            mod.VersionId,
            mod.Provider,
            mod.InstalledAt,
            mod.UpdatedAt,
            mod.IsEnabled,
            mod.LocalPath,
            mod.IconUrl,
            mod.FileSizeBytes
        );
    }
}
