using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

public class ModCacheService(
    ICachedModRepository cachedModRepository,
    IModCacheSyncStatusRepository syncStatusRepository,
    ISystemSettingsRepository settingsRepository,
    IMemoryCache memoryCache,
    IEnumerable<IModProvider> modProviders,
    ILogger<ModCacheService> logger) : IModCacheService
{
    private const string CacheKeyPrefix = "mod_cache:";
    private const string SearchCachePrefix = "search:";
    private const string DetailsCachePrefix = "details:";
    private const string VersionsCachePrefix = "versions:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly IReadOnlyList<SyncIntervalOption> IncrementalIntervals =
    [
        new("Every 15 minutes", "*/15 * * * *"),
        new("Every 30 minutes", "*/30 * * * *"),
        new("Every hour", "0 * * * *"),
        new("Every 2 hours", "0 */2 * * *"),
        new("Every 6 hours", "0 */6 * * *"),
        new("Every 12 hours", "0 */12 * * *"),
        new("Daily", "0 0 * * *")
    ];

    private static readonly IReadOnlyList<SyncIntervalOption> FullSyncIntervals =
    [
        new("Daily", "0 0 * * *"),
        new("Every 3 days", "0 0 */3 * *"),
        new("Weekly", "0 0 * * 0"),
        new("Every 2 weeks", "0 0 1,15 * *"),
        new("Monthly", "0 0 1 * *")
    ];

    public async Task<CachedModSearchResult> SearchAsync(CachedModSearchQuery query, CancellationToken cancellationToken = default)
    {
        var settings = await GetModCacheSettingsAsync(cancellationToken).ConfigureAwait(false);
        var cacheKey = $"{CacheKeyPrefix}{SearchCachePrefix}{ComputeQueryHash(query)}";

        if (memoryCache.TryGetValue<CachedModSearchResult>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var skip = (query.Page - 1) * query.PageSize;

        var mods = await cachedModRepository.SearchAsync(
            query.Query,
            query.Provider,
            query.ContentType,
            query.GameVersion,
            query.Loader,
            query.Category,
            skip,
            query.PageSize,
            cancellationToken).ConfigureAwait(false);

        var totalCount = await cachedModRepository.CountAsync(
            query.Query,
            query.Provider,
            query.ContentType,
            query.GameVersion,
            query.Loader,
            query.Category,
            cancellationToken).ConfigureAwait(false);

        var result = new CachedModSearchResult(
            mods.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize,
            (int)Math.Ceiling(totalCount / (double)query.PageSize));

        memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(settings.SearchResultCacheMinutes));

        return result;
    }

    public async Task<CachedModDetailsDto?> GetDetailsAsync(ModProviderType provider, string providerModId, CancellationToken cancellationToken = default)
    {
        var settings = await GetModCacheSettingsAsync(cancellationToken).ConfigureAwait(false);
        var cacheKey = $"{CacheKeyPrefix}{DetailsCachePrefix}{provider}:{providerModId}";

        if (memoryCache.TryGetValue<CachedModDetailsDto>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var cachedMod = await cachedModRepository.GetByProviderIdAsync(provider, providerModId, cancellationToken).ConfigureAwait(false);

        if (cachedMod is null)
        {
            // Not in DB cache, try to fetch from API
            var modProvider = modProviders.FirstOrDefault(p => p.ProviderType == provider);
            if (modProvider is null)
            {
                return null;
            }

            try
            {
                var details = await modProvider.GetDetailsAsync(providerModId, cancellationToken).ConfigureAwait(false);
                if (details is null) return null;

                return new CachedModDetailsDto(
                    Guid.Empty,
                    provider,
                    providerModId,
                    details.Slug,
                    details.Name,
                    details.Summary,
                    details.IconUrl,
                    details.Authors.FirstOrDefault()?.Name,
                    details.Downloads,
                    details.UpdatedAt,
                    ModContentType.Mod,
                    details.Categories,
                    details.GameVersions,
                    details.Loaders,
                    details.Description,
                    null,
                    DateTime.UtcNow,
                    IsCached: false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch mod details from API for {Provider}:{ModId}", provider, providerModId);
                return null;
            }
        }

        // We have the mod in cache, check if we have description
        string? descriptionHtml = cachedMod.DescriptionHtml;
        IReadOnlyList<ModVersion>? versions = null;

        if (string.IsNullOrEmpty(cachedMod.DescriptionHtml))
        {
            // Fetch description lazily
            var modProvider = modProviders.FirstOrDefault(p => p.ProviderType == provider);
            if (modProvider is not null)
            {
                try
                {
                    var details = await modProvider.GetDetailsAsync(providerModId, cancellationToken).ConfigureAwait(false);
                    if (details is not null)
                    {
                        descriptionHtml = details.Description;
                        // Update cache with description
                        await cachedModRepository.UpdateDetailsAsync(cachedMod.Id, descriptionHtml, null, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to fetch mod description from API for {Provider}:{ModId}", provider, providerModId);
                }
            }
        }

        if (!string.IsNullOrEmpty(cachedMod.VersionsJson))
        {
            versions = JsonSerializer.Deserialize<List<ModVersion>>(cachedMod.VersionsJson, JsonOptions);
        }

        var result = new CachedModDetailsDto(
            cachedMod.Id,
            cachedMod.Provider,
            cachedMod.ProviderModId,
            cachedMod.Slug,
            cachedMod.Name,
            cachedMod.Summary,
            cachedMod.IconUrl,
            cachedMod.Author,
            cachedMod.Downloads,
            cachedMod.UpdatedAt,
            cachedMod.ContentType,
            ParseJsonArray(cachedMod.CategoriesJson),
            ParseJsonArray(cachedMod.GameVersionsJson),
            ParseJsonArray(cachedMod.LoadersJson),
            descriptionHtml,
            versions,
            cachedMod.CachedAt,
            IsCached: true);

        memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(settings.ModDetailsCacheMinutes));

        return result;
    }

    public async Task<IReadOnlyList<ModVersion>> GetVersionsAsync(ModProviderType provider, string providerModId, string? gameVersion = null, CancellationToken cancellationToken = default)
    {
        var settings = await GetModCacheSettingsAsync(cancellationToken).ConfigureAwait(false);
        var cacheKey = $"{CacheKeyPrefix}{VersionsCachePrefix}{provider}:{providerModId}:{gameVersion ?? "all"}";

        if (memoryCache.TryGetValue<IReadOnlyList<ModVersion>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var cachedMod = await cachedModRepository.GetByProviderIdAsync(provider, providerModId, cancellationToken).ConfigureAwait(false);

        if (cachedMod is not null && !string.IsNullOrEmpty(cachedMod.VersionsJson))
        {
            var cachedVersions = JsonSerializer.Deserialize<List<ModVersion>>(cachedMod.VersionsJson, JsonOptions) ?? [];
            if (cachedVersions.Count > 0)
            {
                var filtered = string.IsNullOrEmpty(gameVersion)
                    ? cachedVersions
                    : cachedVersions.Where(v => v.GameVersions.Contains(gameVersion)).ToList();

                memoryCache.Set(cacheKey, (IReadOnlyList<ModVersion>)filtered, TimeSpan.FromMinutes(settings.ModDetailsCacheMinutes));
                return filtered;
            }
        }

        // Fallback to API
        var modProvider = modProviders.FirstOrDefault(p => p.ProviderType == provider);
        if (modProvider is null)
        {
            return [];
        }

        try
        {
            var versions = await modProvider.GetVersionsAsync(providerModId, gameVersion, cancellationToken).ConfigureAwait(false);

            // Update cache if we have the mod
            if (cachedMod is not null && versions.Count > 0)
            {
                var versionsJson = JsonSerializer.Serialize(versions, JsonOptions);
                await cachedModRepository.UpdateDetailsAsync(cachedMod.Id, null, versionsJson, cancellationToken).ConfigureAwait(false);
            }

            memoryCache.Set(cacheKey, versions, TimeSpan.FromMinutes(settings.ModDetailsCacheMinutes));
            return versions;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch mod versions from API for {Provider}:{ModId}", provider, providerModId);
            return [];
        }
    }

    public async Task<ModCacheStatsDto> GetCacheStatsAsync(CancellationToken cancellationToken = default)
    {
        var modrinthModCount = await cachedModRepository.GetTotalCountAsync(ModProviderType.Modrinth, ModContentType.Mod, cancellationToken).ConfigureAwait(false);
        var modrinthModpackCount = await cachedModRepository.GetTotalCountAsync(ModProviderType.Modrinth, ModContentType.Modpack, cancellationToken).ConfigureAwait(false);
        var curseForgeModCount = await cachedModRepository.GetTotalCountAsync(ModProviderType.CurseForge, ModContentType.Mod, cancellationToken).ConfigureAwait(false);
        var curseForgeModpackCount = await cachedModRepository.GetTotalCountAsync(ModProviderType.CurseForge, ModContentType.Modpack, cancellationToken).ConfigureAwait(false);

        var oldestEntry = await cachedModRepository.GetOldestCacheEntryAsync(cancellationToken).ConfigureAwait(false);

        var syncStatuses = await syncStatusRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var syncDates = syncStatuses
            .Select(s => s.LastFullSyncCompletedAt ?? s.LastIncrementalSyncAt)
            .Where(d => d.HasValue)
            .ToList();
        var lastSync = syncDates.Count > 0 ? syncDates.Max() : null;

        return new ModCacheStatsDto(
            modrinthModCount + curseForgeModCount,
            modrinthModpackCount + curseForgeModpackCount,
            modrinthModCount,
            modrinthModpackCount,
            curseForgeModCount,
            curseForgeModpackCount,
            oldestEntry,
            lastSync);
    }

    public async Task<ModCacheSyncOverviewDto> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await syncStatusRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var dtos = statuses.Select(s => new ModCacheSyncStatusDto(
            s.Provider,
            s.ContentType,
            s.State,
            s.CurrentPage,
            s.TotalPages,
            s.ItemsSynced,
            s.TotalItems,
            s.TotalItems > 0 ? (double)s.ItemsSynced / s.TotalItems * 100 : 0,
            s.LastFullSyncStartedAt,
            s.LastFullSyncCompletedAt,
            s.LastIncrementalSyncAt,
            s.LastError)).ToList();

        var isAnySyncRunning = dtos.Any(s => s.State == ModCacheSyncState.Running);
        var isInitialSyncComplete = dtos.Any(s => s.LastFullSyncCompletedAt.HasValue);

        return new ModCacheSyncOverviewDto(dtos, isAnySyncRunning, isInitialSyncComplete);
    }

    public async Task<ModCacheSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetModCacheSettingsAsync(cancellationToken).ConfigureAwait(false);

        return new ModCacheSettingsDto(
            settings.Enabled,
            settings.ModrinthEnabled,
            settings.CurseForgeEnabled,
            settings.CacheModpacks,
            settings.IncrementalSyncCron,
            settings.FullSyncCron,
            settings.RequestDelayMs,
            settings.SearchResultCacheMinutes,
            settings.ModDetailsCacheMinutes,
            IncrementalIntervals,
            FullSyncIntervals);
    }

    public async Task<Result<ModCacheSettingsDto>> UpdateSettingsAsync(UpdateModCacheSettingsRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (request.RequestDelayMs < 0 || request.RequestDelayMs > 10000)
        {
            return Result.Failure<ModCacheSettingsDto>(Error.Validation("Request delay must be between 0 and 10000 milliseconds."));
        }

        if (!IncrementalIntervals.Any(i => i.CronExpression == request.IncrementalSyncCron))
        {
            return Result.Failure<ModCacheSettingsDto>(Error.Validation("Invalid incremental sync interval."));
        }

        if (!FullSyncIntervals.Any(i => i.CronExpression == request.FullSyncCron))
        {
            return Result.Failure<ModCacheSettingsDto>(Error.Validation("Invalid full sync interval."));
        }

        var systemSettings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);

        var newSettings = new ModCacheSettings
        {
            Enabled = request.Enabled,
            ModrinthEnabled = request.ModrinthEnabled,
            CurseForgeEnabled = request.CurseForgeEnabled,
            CacheModpacks = request.CacheModpacks,
            IncrementalSyncCron = request.IncrementalSyncCron,
            FullSyncCron = request.FullSyncCron,
            RequestDelayMs = request.RequestDelayMs
        };

        systemSettings.ModCacheSettingsJson = JsonSerializer.Serialize(newSettings, JsonOptions);
        systemSettings.LastModifiedByUserId = userId;

        await settingsRepository.UpdateAsync(systemSettings, cancellationToken).ConfigureAwait(false);

        // Clear memory cache when settings change
        ClearMemoryCache();

        logger.LogInformation("Mod cache settings updated by user {UserId}", userId);

        return new ModCacheSettingsDto(
            newSettings.Enabled,
            newSettings.ModrinthEnabled,
            newSettings.CurseForgeEnabled,
            newSettings.CacheModpacks,
            newSettings.IncrementalSyncCron,
            newSettings.FullSyncCron,
            newSettings.RequestDelayMs,
            newSettings.SearchResultCacheMinutes,
            newSettings.ModDetailsCacheMinutes,
            IncrementalIntervals,
            FullSyncIntervals);
    }

    #region Helpers

    private async Task<ModCacheSettings> GetModCacheSettingsAsync(CancellationToken cancellationToken)
    {
        var systemSettings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(systemSettings.ModCacheSettingsJson) || systemSettings.ModCacheSettingsJson == "{}")
        {
            return new ModCacheSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<ModCacheSettings>(systemSettings.ModCacheSettingsJson, JsonOptions) ?? new ModCacheSettings();
        }
        catch
        {
            return new ModCacheSettings();
        }
    }

    private static CachedModDto MapToDto(CachedMod mod) => new(
        mod.Id,
        mod.Provider,
        mod.ProviderModId,
        mod.Slug,
        mod.Name,
        mod.Summary,
        mod.IconUrl,
        mod.Author,
        mod.Downloads,
        mod.UpdatedAt,
        mod.ContentType,
        ParseJsonArray(mod.CategoriesJson),
        ParseJsonArray(mod.GameVersionsJson),
        ParseJsonArray(mod.LoadersJson),
        mod.CachedAt);

    private static IReadOnlyList<string> ParseJsonArray(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string ComputeQueryHash(CachedModSearchQuery query)
    {
        var key = $"{query.Query}|{query.Provider}|{query.ContentType}|{query.GameVersion}|{query.Loader}|{query.Category}|{query.Page}|{query.PageSize}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key));
    }

    private void ClearMemoryCache()
    {
        // IMemoryCache doesn't support clearing all keys with a prefix,
        // but we can let entries expire naturally or implement a more sophisticated cache invalidation
        logger.LogInformation("Mod cache settings changed, memory cache entries will expire naturally");
    }

    #endregion
}
