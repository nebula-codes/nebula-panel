using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for reading from the mod cache.
/// Provides search, details, and cache statistics.
/// </summary>
public interface IModCacheService
{
    /// <summary>
    /// Search cached mods with filters.
    /// </summary>
    Task<CachedModSearchResult> SearchAsync(CachedModSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed information about a cached mod.
    /// Falls back to live API if description/versions not cached.
    /// </summary>
    Task<CachedModDetailsDto?> GetDetailsAsync(ModProviderType provider, string providerModId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get versions for a cached mod.
    /// Falls back to live API if not cached.
    /// </summary>
    Task<IReadOnlyList<ModVersion>> GetVersionsAsync(ModProviderType provider, string providerModId, string? gameVersion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    Task<ModCacheStatsDto> GetCacheStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get sync status overview for all providers.
    /// </summary>
    Task<ModCacheSyncOverviewDto> GetSyncStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get mod cache settings.
    /// </summary>
    Task<ModCacheSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update mod cache settings.
    /// </summary>
    Task<Result<ModCacheSettingsDto>> UpdateSettingsAsync(UpdateModCacheSettingsRequest request, Guid userId, CancellationToken cancellationToken = default);
}
