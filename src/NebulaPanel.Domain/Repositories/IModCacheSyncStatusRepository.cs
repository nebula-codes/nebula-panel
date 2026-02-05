using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Domain.Repositories;

public interface IModCacheSyncStatusRepository
{
    Task<ModCacheSyncStatus?> GetAsync(ModProviderType provider, ModContentType contentType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModCacheSyncStatus>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(ModCacheSyncStatus status, CancellationToken cancellationToken = default);

    Task UpdateStateAsync(ModProviderType provider, ModContentType contentType, ModCacheSyncState state, CancellationToken cancellationToken = default);

    Task UpdateProgressAsync(ModProviderType provider, ModContentType contentType, int currentPage, int totalPages, int itemsSynced, int totalItems, CancellationToken cancellationToken = default);

    Task SetErrorAsync(ModProviderType provider, ModContentType contentType, string error, CancellationToken cancellationToken = default);

    Task ClearErrorAsync(ModProviderType provider, ModContentType contentType, CancellationToken cancellationToken = default);
}
