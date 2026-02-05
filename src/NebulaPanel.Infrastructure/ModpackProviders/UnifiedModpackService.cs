using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.ModpackProviders;

/// <summary>
/// Unified service for modpack management that aggregates results from multiple providers.
/// </summary>
public class UnifiedModpackService(
    IModpackProviderRegistry providerRegistry,
    IModpackInstallationService installationService,
    ILogger<UnifiedModpackService> logger) : IUnifiedModpackService
{
    private readonly IModpackProviderRegistry _providerRegistry = providerRegistry;
    private readonly IModpackInstallationService _installationService = installationService;
    private readonly ILogger<UnifiedModpackService> _logger = logger;

    /// <inheritdoc />
    public async Task<UnifiedModpackSearchResult> SearchAsync(
        ModpackSearchQuery query,
        CancellationToken ct = default)
    {
        var providers = _providerRegistry.GetEnabledProviders();
        var allResults = new List<ModpackSearchItem>();
        var resultCountByProvider = new Dictionary<ModpackProviderType, int>();

        var searchTasks = providers.Select(async provider =>
        {
            try
            {
                var result = await provider.SearchAsync(query, ct).ConfigureAwait(false);
                return (provider.ProviderType, result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search provider {Provider}", provider.ProviderType);
                return (provider.ProviderType, (ModpackSearchResult?)null);
            }
        });

        var results = await Task.WhenAll(searchTasks).ConfigureAwait(false);

        foreach (var (providerType, result) in results)
        {
            if (result is not null)
            {
                allResults.AddRange(result.Modpacks);
                resultCountByProvider[providerType] = result.TotalCount;
            }
            else
            {
                resultCountByProvider[providerType] = 0;
            }
        }

        // Sort by downloads (popularity) by default
        var sortedResults = query.Sort switch
        {
            ModpackSearchSort.RecentlyUpdated => allResults.OrderByDescending(x => x.UpdatedAt),
            ModpackSearchSort.TotalDownloads => allResults.OrderByDescending(x => x.Downloads),
            ModpackSearchSort.Name => allResults.OrderBy(x => x.Name),
            _ => allResults.OrderByDescending(x => x.Downloads) // Popularity
        };

        return new UnifiedModpackSearchResult
        {
            Modpacks = sortedResults.ToList(),
            ResultCountByProvider = resultCountByProvider,
            TotalCount = allResults.Count,
            HasMoreResults = resultCountByProvider.Values.Any(c => c > query.PageSize)
        };
    }

    /// <inheritdoc />
    public async Task<ModpackSearchResult> SearchProviderAsync(
        ModpackProviderType provider,
        ModpackSearchQuery query,
        CancellationToken ct = default)
    {
        var modpackProvider = _providerRegistry.GetProvider(provider);
        if (modpackProvider is null)
        {
            _logger.LogWarning("Provider {Provider} not found", provider);
            return new ModpackSearchResult
            {
                Modpacks = [],
                TotalCount = 0,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        return await modpackProvider.SearchAsync(query, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModpackSearchItem>> GetFeaturedAsync(
        int countPerProvider = 10,
        CancellationToken ct = default)
    {
        var providers = _providerRegistry.GetEnabledProviders();
        var allFeatured = new List<ModpackSearchItem>();

        var featuredTasks = providers.Select(async provider =>
        {
            try
            {
                var featured = await provider.GetFeaturedAsync(countPerProvider, ct).ConfigureAwait(false);
                return featured;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get featured from provider {Provider}", provider.ProviderType);
                return (IReadOnlyList<ModpackSearchItem>)[];
            }
        });

        var results = await Task.WhenAll(featuredTasks).ConfigureAwait(false);

        foreach (var featured in results)
        {
            allFeatured.AddRange(featured);
        }

        // Sort by downloads
        return allFeatured.OrderByDescending(x => x.Downloads).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModpackSearchItem>> GetFeaturedFromProviderAsync(
        ModpackProviderType provider,
        int count = 20,
        CancellationToken ct = default)
    {
        var modpackProvider = _providerRegistry.GetProvider(provider);
        if (modpackProvider is null)
        {
            _logger.LogWarning("Provider {Provider} not found", provider);
            return [];
        }

        return await modpackProvider.GetFeaturedAsync(count, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ModpackDetails> GetDetailsAsync(
        ModpackProviderType provider,
        string modpackId,
        CancellationToken ct = default)
    {
        var modpackProvider = _providerRegistry.GetProvider(provider);
        if (modpackProvider is null)
        {
            throw new InvalidOperationException($"Provider {provider} not found");
        }

        return await modpackProvider.GetDetailsAsync(modpackId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModpackVersion>> GetVersionsAsync(
        ModpackProviderType provider,
        string modpackId,
        CancellationToken ct = default)
    {
        var modpackProvider = _providerRegistry.GetProvider(provider);
        if (modpackProvider is null)
        {
            _logger.LogWarning("Provider {Provider} not found", provider);
            return [];
        }

        return await modpackProvider.GetVersionsAsync(modpackId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ModpackServerFiles?> GetServerFilesAsync(
        ModpackProviderType provider,
        string modpackId,
        string versionId,
        CancellationToken ct = default)
    {
        var modpackProvider = _providerRegistry.GetProvider(provider);
        if (modpackProvider is null)
        {
            _logger.LogWarning("Provider {Provider} not found", provider);
            return null;
        }

        return await modpackProvider.GetServerFilesAsync(modpackId, versionId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ModpackInstallResult> InstallModpackAsync(
        GameServer server,
        ModpackProviderType provider,
        string modpackId,
        string versionId,
        IProgress<ModpackInstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var modpackProvider = _providerRegistry.GetProvider(provider);
        if (modpackProvider is null)
        {
            return ModpackInstallResult.Failed($"Provider {provider} not found");
        }

        // Get modpack details and version info for the install request
        var details = await modpackProvider.GetDetailsAsync(modpackId, ct).ConfigureAwait(false);
        var versions = await modpackProvider.GetVersionsAsync(modpackId, ct).ConfigureAwait(false);
        var version = versions.FirstOrDefault(v => v.Id == versionId);

        if (version is null)
        {
            return ModpackInstallResult.Failed($"Version {versionId} not found for modpack {modpackId}");
        }

        var serverFiles = await modpackProvider.GetServerFilesAsync(modpackId, versionId, ct).ConfigureAwait(false);

        var installRequest = new ModpackInstallRequest
        {
            Provider = provider,
            ModpackId = modpackId,
            ModpackName = details.Name,
            VersionId = versionId,
            VersionName = version.Name,
            MinecraftVersion = version.MinecraftVersion,
            Loader = version.Loader,
            LoaderVersion = version.LoaderVersion,
            RecommendedRamMb = serverFiles?.RecommendedRamMb ?? 4096,
            ModCount = details.ModCount
        };

        return await _installationService.InstallAsync(server, installRequest, progress, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IReadOnlyList<ModpackProviderInfo> GetAvailableProviders()
    {
        return _providerRegistry.GetAllProviders()
            .Select(p => new ModpackProviderInfo
            {
                Type = p.ProviderType,
                Name = p.DisplayName,
                IconUrl = p.IconUrl,
                Enabled = true
            })
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ModpackProviderInfo> GetEnabledProviders()
    {
        return _providerRegistry.GetEnabledProviders()
            .Select(p => new ModpackProviderInfo
            {
                Type = p.ProviderType,
                Name = p.DisplayName,
                IconUrl = p.IconUrl,
                Enabled = true
            })
            .ToList();
    }

    /// <inheritdoc />
    public bool IsProviderEnabled(ModpackProviderType provider)
    {
        return _providerRegistry.IsProviderAvailable(provider);
    }
}
