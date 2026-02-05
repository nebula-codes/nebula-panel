using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Settings for the mod cache system.
/// </summary>
public record ModCacheSettingsDto(
    bool Enabled,
    bool ModrinthEnabled,
    bool CurseForgeEnabled,
    bool CacheModpacks,
    string IncrementalSyncCron,
    string FullSyncCron,
    int RequestDelayMs,
    int SearchResultCacheMinutes,
    int ModDetailsCacheMinutes,
    IReadOnlyList<SyncIntervalOption> AvailableIncrementalIntervals,
    IReadOnlyList<SyncIntervalOption> AvailableFullSyncIntervals
);

/// <summary>
/// Available sync interval options for dropdowns.
/// </summary>
public record SyncIntervalOption(string Label, string CronExpression);

/// <summary>
/// Request to update mod cache settings.
/// </summary>
public record UpdateModCacheSettingsRequest(
    bool Enabled,
    bool ModrinthEnabled,
    bool CurseForgeEnabled,
    bool CacheModpacks,
    string IncrementalSyncCron,
    string FullSyncCron,
    int RequestDelayMs
);

/// <summary>
/// Overall mod cache statistics.
/// </summary>
public record ModCacheStatsDto(
    int TotalModsCached,
    int TotalModpacksCached,
    int ModrinthModCount,
    int ModrinthModpackCount,
    int CurseForgeModCount,
    int CurseForgeModpackCount,
    DateTime? OldestCacheEntry,
    DateTime? LastSyncTime
);

/// <summary>
/// Sync status for a specific provider + content type combination.
/// </summary>
public record ModCacheSyncStatusDto(
    ModProviderType Provider,
    ModContentType ContentType,
    ModCacheSyncState State,
    int CurrentPage,
    int TotalPages,
    int ItemsSynced,
    int TotalItems,
    double ProgressPercentage,
    DateTime? LastFullSyncStartedAt,
    DateTime? LastFullSyncCompletedAt,
    DateTime? LastIncrementalSyncAt,
    string? LastError
);

/// <summary>
/// Combined sync status for all providers.
/// </summary>
public record ModCacheSyncOverviewDto(
    IReadOnlyList<ModCacheSyncStatusDto> SyncStatuses,
    bool IsAnySyncRunning,
    bool IsInitialSyncComplete
);

/// <summary>
/// Query for searching cached mods.
/// </summary>
public record CachedModSearchQuery(
    string? Query,
    ModProviderType? Provider,
    ModContentType? ContentType,
    string? GameVersion,
    string? Loader,
    string? Category,
    int Page = 1,
    int PageSize = 20
);

/// <summary>
/// Result of searching cached mods.
/// </summary>
public record CachedModSearchResult(
    IReadOnlyList<CachedModDto> Mods,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>
/// DTO for a cached mod.
/// </summary>
public record CachedModDto(
    Guid Id,
    ModProviderType Provider,
    string ProviderModId,
    string Slug,
    string Name,
    string? Summary,
    string? IconUrl,
    string? Author,
    long Downloads,
    DateTime UpdatedAt,
    ModContentType ContentType,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<string> Loaders,
    DateTime CachedAt
);

/// <summary>
/// Detailed cached mod information with description and versions.
/// </summary>
public record CachedModDetailsDto(
    Guid Id,
    ModProviderType Provider,
    string ProviderModId,
    string Slug,
    string Name,
    string? Summary,
    string? IconUrl,
    string? Author,
    long Downloads,
    DateTime UpdatedAt,
    ModContentType ContentType,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<string> Loaders,
    string? DescriptionHtml,
    IReadOnlyList<ModVersion>? Versions,
    DateTime CachedAt,
    bool IsCached
);
