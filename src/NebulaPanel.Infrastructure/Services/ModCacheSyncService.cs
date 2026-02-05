using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Services;

public class ModCacheSyncService(
    ICachedModRepository cachedModRepository,
    IModCacheSyncStatusRepository syncStatusRepository,
    ISystemSettingsRepository settingsRepository,
    IEnumerable<IModProvider> modProviders,
    ILogger<ModCacheSyncService> logger) : IModCacheSyncService
{
    private const int BatchSize = 100;
    private const int ModrinthPageSize = 100;  // Modrinth supports up to 100
    private const int CurseForgePageSize = 50; // CurseForge max is 50
    private const int CurseForgeMaxIndex = 5000; // CurseForge API limit (returns 400 at higher offsets)
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static volatile bool _syncPaused;

    public async Task FullSyncAsync(ModProviderType provider, ModContentType contentType, CancellationToken cancellationToken = default)
    {
        var settings = await GetModCacheSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled) return;

        if (provider == ModProviderType.Modrinth && !settings.ModrinthEnabled) return;
        if (provider == ModProviderType.CurseForge && !settings.CurseForgeEnabled) return;
        if (contentType == ModContentType.Modpack && !settings.CacheModpacks) return;

        var modProvider = modProviders.FirstOrDefault(p => p.ProviderType == provider);
        if (modProvider is null)
        {
            logger.LogWarning("No provider found for {Provider}", provider);
            return;
        }

        var status = await syncStatusRepository.GetAsync(provider, contentType, cancellationToken).ConfigureAwait(false);

        // Resume from last page if sync was interrupted
        var startPage = status?.State == ModCacheSyncState.Running ? status.CurrentPage : 1;

        await syncStatusRepository.UpsertAsync(new ModCacheSyncStatus
        {
            Provider = provider,
            ContentType = contentType,
            State = ModCacheSyncState.Running,
            CurrentPage = startPage,
            TotalPages = status?.TotalPages ?? 0,
            ItemsSynced = status?.ItemsSynced ?? 0,
            TotalItems = status?.TotalItems ?? 0,
            LastFullSyncStartedAt = startPage == 1 ? DateTime.UtcNow : status?.LastFullSyncStartedAt,
            LastFullSyncCompletedAt = status?.LastFullSyncCompletedAt,
            LastIncrementalSyncAt = status?.LastIncrementalSyncAt,
            LastError = null
        }, cancellationToken).ConfigureAwait(false);

        // Use appropriate page size for each provider
        var pageSize = provider == ModProviderType.CurseForge ? CurseForgePageSize : ModrinthPageSize;

        logger.LogInformation("Starting full sync for {Provider} {ContentType} from page {StartPage} with pageSize {PageSize}",
            provider, contentType, startPage, pageSize);

        try
        {
            var currentPage = startPage;
            var totalItems = 0;
            var itemsSynced = status?.ItemsSynced ?? 0;
            var hasMore = true;

            while (hasMore && !cancellationToken.IsCancellationRequested)
            {
                if (_syncPaused)
                {
                    await syncStatusRepository.UpdateStateAsync(provider, contentType, ModCacheSyncState.Paused, cancellationToken).ConfigureAwait(false);
                    logger.LogInformation("Sync paused for {Provider} {ContentType} at page {Page}", provider, contentType, currentPage);
                    return;
                }

                // Check CurseForge index limit before making request
                var currentIndex = (currentPage - 1) * pageSize;
                if (provider == ModProviderType.CurseForge && currentIndex >= CurseForgeMaxIndex)
                {
                    logger.LogWarning(
                        "CurseForge index limit reached ({MaxIndex}). Synced {ItemsSynced} items. " +
                        "CurseForge API does not allow pagination beyond this point.",
                        CurseForgeMaxIndex, itemsSynced);
                    hasMore = false;
                    break;
                }

                var query = new ModSearchQuery(
                    Query: null,
                    GameSlug: "minecraft",
                    GameVersion: null,
                    ModLoader: null,
                    Sort: ModSearchSort.Updated,
                    Page: currentPage,
                    PageSize: pageSize,
                    ContentType: contentType);

                var result = await modProvider.SearchAsync(query, cancellationToken).ConfigureAwait(false);

                if (currentPage == startPage || startPage == 1)
                {
                    totalItems = result.TotalCount;

                    // For CurseForge, cap the displayed total at the max we can actually fetch
                    if (provider == ModProviderType.CurseForge && totalItems > CurseForgeMaxIndex)
                    {
                        logger.LogInformation(
                            "CurseForge reports {TotalCount} total items, but API limits us to {MaxIndex}",
                            totalItems, CurseForgeMaxIndex);
                    }
                }

                var batch = result.Mods.Select(m => MapToCachedMod(m, provider, contentType)).ToList();

                if (batch.Count > 0)
                {
                    await cachedModRepository.UpsertBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                    itemsSynced += batch.Count;
                }

                // Calculate effective total pages considering CurseForge limit
                var effectiveTotalPages = result.TotalPages;
                if (provider == ModProviderType.CurseForge)
                {
                    effectiveTotalPages = Math.Min(result.TotalPages, CurseForgeMaxIndex / pageSize);
                }

                await syncStatusRepository.UpdateProgressAsync(
                    provider,
                    contentType,
                    currentPage,
                    effectiveTotalPages,
                    itemsSynced,
                    Math.Min(totalItems, provider == ModProviderType.CurseForge ? CurseForgeMaxIndex : totalItems),
                    cancellationToken).ConfigureAwait(false);

                logger.LogDebug(
                    "Synced page {Page}/{TotalPages} for {Provider} {ContentType}: {Count} mods ({ItemsSynced}/{TotalItems})",
                    currentPage, effectiveTotalPages, provider, contentType, batch.Count, itemsSynced, totalItems);

                hasMore = currentPage < effectiveTotalPages && result.Mods.Count > 0;
                currentPage++;

                // Rate limiting
                if (hasMore && settings.RequestDelayMs > 0)
                {
                    await Task.Delay(settings.RequestDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }

            await syncStatusRepository.UpsertAsync(new ModCacheSyncStatus
            {
                Provider = provider,
                ContentType = contentType,
                State = ModCacheSyncState.Idle,
                CurrentPage = 0,
                TotalPages = 0,
                ItemsSynced = itemsSynced,
                TotalItems = totalItems,
                LastFullSyncStartedAt = status?.LastFullSyncStartedAt,
                LastFullSyncCompletedAt = DateTime.UtcNow,
                LastIncrementalSyncAt = status?.LastIncrementalSyncAt,
                LastError = null
            }, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Full sync completed for {Provider} {ContentType}: {ItemsSynced} items", provider, contentType, itemsSynced);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Full sync failed for {Provider} {ContentType}", provider, contentType);
            await syncStatusRepository.SetErrorAsync(provider, contentType, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task IncrementalSyncAsync(ModProviderType provider, ModContentType contentType, CancellationToken cancellationToken = default)
    {
        var settings = await GetModCacheSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled) return;

        if (provider == ModProviderType.Modrinth && !settings.ModrinthEnabled) return;
        if (provider == ModProviderType.CurseForge && !settings.CurseForgeEnabled) return;
        if (contentType == ModContentType.Modpack && !settings.CacheModpacks) return;

        var modProvider = modProviders.FirstOrDefault(p => p.ProviderType == provider);
        if (modProvider is null)
        {
            logger.LogWarning("No provider found for {Provider}", provider);
            return;
        }

        var status = await syncStatusRepository.GetAsync(provider, contentType, cancellationToken).ConfigureAwait(false);

        // Skip if no full sync completed yet
        if (status?.LastFullSyncCompletedAt is null)
        {
            logger.LogInformation("Skipping incremental sync for {Provider} {ContentType}: no full sync completed", provider, contentType);
            return;
        }

        await syncStatusRepository.UpdateStateAsync(provider, contentType, ModCacheSyncState.Running, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Starting incremental sync for {Provider} {ContentType}", provider, contentType);

        // Use appropriate page size for each provider
        var pageSize = provider == ModProviderType.CurseForge ? CurseForgePageSize : ModrinthPageSize;

        try
        {
            var itemsSynced = 0;
            var currentPage = 1;
            var hasMore = true;
            var lastSyncTime = status.LastIncrementalSyncAt ?? status.LastFullSyncCompletedAt.Value;

            while (hasMore && !cancellationToken.IsCancellationRequested)
            {
                if (_syncPaused)
                {
                    await syncStatusRepository.UpdateStateAsync(provider, contentType, ModCacheSyncState.Paused, cancellationToken).ConfigureAwait(false);
                    return;
                }

                // Check CurseForge index limit
                var currentIndex = (currentPage - 1) * pageSize;
                if (provider == ModProviderType.CurseForge && currentIndex >= CurseForgeMaxIndex)
                {
                    logger.LogWarning("CurseForge index limit reached during incremental sync");
                    hasMore = false;
                    break;
                }

                var query = new ModSearchQuery(
                    Query: null,
                    GameSlug: "minecraft",
                    GameVersion: null,
                    ModLoader: null,
                    Sort: ModSearchSort.Updated,
                    Page: currentPage,
                    PageSize: pageSize,
                    ContentType: contentType);

                var result = await modProvider.SearchAsync(query, cancellationToken).ConfigureAwait(false);

                // Stop when we hit mods that haven't been updated since our last sync
                var newMods = result.Mods
                    .Where(m => m.UpdatedAt.HasValue && m.UpdatedAt.Value > lastSyncTime)
                    .ToList();

                if (newMods.Count == 0)
                {
                    hasMore = false;
                    break;
                }

                var batch = newMods.Select(m => MapToCachedMod(m, provider, contentType)).ToList();
                await cachedModRepository.UpsertBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                itemsSynced += batch.Count;

                // If we found fewer new mods than the page size, we've reached the end of updates
                hasMore = newMods.Count == pageSize && currentPage < result.TotalPages;
                currentPage++;

                if (hasMore && settings.RequestDelayMs > 0)
                {
                    await Task.Delay(settings.RequestDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }

            await syncStatusRepository.UpsertAsync(new ModCacheSyncStatus
            {
                Provider = provider,
                ContentType = contentType,
                State = ModCacheSyncState.Idle,
                CurrentPage = 0,
                TotalPages = 0,
                LastFullSyncStartedAt = status.LastFullSyncStartedAt,
                LastFullSyncCompletedAt = status.LastFullSyncCompletedAt,
                LastIncrementalSyncAt = DateTime.UtcNow,
                ItemsSynced = status.ItemsSynced + itemsSynced,
                TotalItems = status.TotalItems,
                LastError = null
            }, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Incremental sync completed for {Provider} {ContentType}: {ItemsSynced} new/updated items", provider, contentType, itemsSynced);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Incremental sync failed for {Provider} {ContentType}", provider, contentType);
            await syncStatusRepository.SetErrorAsync(provider, contentType, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<Result> TriggerManualSyncAsync(bool fullSync, CancellationToken cancellationToken = default)
    {
        var settings = await GetModCacheSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            return Result.Failure("Mod caching is disabled.");
        }

        _syncPaused = false;

        logger.LogInformation("Manual {SyncType} sync triggered", fullSync ? "full" : "incremental");

        var providers = new List<(ModProviderType, ModContentType)>();

        if (settings.ModrinthEnabled)
        {
            providers.Add((ModProviderType.Modrinth, ModContentType.Mod));
            if (settings.CacheModpacks)
                providers.Add((ModProviderType.Modrinth, ModContentType.Modpack));
        }

        if (settings.CurseForgeEnabled)
        {
            providers.Add((ModProviderType.CurseForge, ModContentType.Mod));
            if (settings.CacheModpacks)
                providers.Add((ModProviderType.CurseForge, ModContentType.Modpack));
        }

        foreach (var (provider, contentType) in providers)
        {
            if (fullSync)
            {
                await FullSyncAsync(provider, contentType, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await IncrementalSyncAsync(provider, contentType, cancellationToken).ConfigureAwait(false);
            }
        }

        return Result.Success();
    }

    public Task<Result> PauseSyncAsync(CancellationToken cancellationToken = default)
    {
        _syncPaused = true;
        logger.LogInformation("Sync pause requested");
        return Task.FromResult(Result.Success());
    }

    public Task<Result> ResumeSyncAsync(CancellationToken cancellationToken = default)
    {
        _syncPaused = false;
        logger.LogInformation("Sync resume requested");
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> ClearCacheAsync(ModProviderType provider, CancellationToken cancellationToken = default)
    {
        await cachedModRepository.DeleteByProviderAsync(provider, cancellationToken).ConfigureAwait(false);

        // Reset sync status
        foreach (var contentType in new[] { ModContentType.Mod, ModContentType.Modpack })
        {
            await syncStatusRepository.UpsertAsync(new ModCacheSyncStatus
            {
                Provider = provider,
                ContentType = contentType,
                State = ModCacheSyncState.Idle,
                CurrentPage = 0,
                TotalPages = 0,
                ItemsSynced = 0,
                TotalItems = 0,
                LastFullSyncStartedAt = null,
                LastFullSyncCompletedAt = null,
                LastIncrementalSyncAt = null
            }, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Cache cleared for {Provider}", provider);
        return Result.Success();
    }

    public async Task<Result> ClearAllCacheAsync(CancellationToken cancellationToken = default)
    {
        await ClearCacheAsync(ModProviderType.Modrinth, cancellationToken).ConfigureAwait(false);
        await ClearCacheAsync(ModProviderType.CurseForge, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

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

    private static CachedMod MapToCachedMod(ModSearchItem item, ModProviderType provider, ModContentType contentType)
    {
        return new CachedMod
        {
            Provider = provider,
            ProviderModId = item.Id,
            Slug = item.Slug,
            Name = item.Name,
            Summary = item.Summary,
            IconUrl = item.IconUrl,
            Author = item.Author,
            Downloads = item.Downloads,
            UpdatedAt = item.UpdatedAt ?? DateTime.UtcNow,
            ContentType = contentType,
            CategoriesJson = JsonSerializer.Serialize(item.Categories, JsonOptions),
            GameVersionsJson = JsonSerializer.Serialize(item.GameVersions, JsonOptions),
            LoadersJson = "[]" // Not provided in search results
        };
    }
}
