using NebulaPanel.Application.Common;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for synchronizing mods from providers to the cache.
/// Used by background jobs.
/// </summary>
public interface IModCacheSyncService
{
    /// <summary>
    /// Perform a full sync of all mods for a provider and content type.
    /// Iterates through all pages, resumable from last saved progress.
    /// </summary>
    Task FullSyncAsync(ModProviderType provider, ModContentType contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform an incremental sync, fetching only recently updated mods.
    /// </summary>
    Task IncrementalSyncAsync(ModProviderType provider, ModContentType contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trigger a manual sync for all enabled providers.
    /// </summary>
    Task<Result> TriggerManualSyncAsync(bool fullSync, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause all running syncs.
    /// </summary>
    Task<Result> PauseSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resume paused syncs.
    /// </summary>
    Task<Result> ResumeSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all cached data for a provider.
    /// </summary>
    Task<Result> ClearCacheAsync(ModProviderType provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all cached data.
    /// </summary>
    Task<Result> ClearAllCacheAsync(CancellationToken cancellationToken = default);
}
