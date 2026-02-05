using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class ModCacheSyncStatusRepository(NebulaPanelDbContext context) : IModCacheSyncStatusRepository
{
    public async Task<ModCacheSyncStatus?> GetAsync(ModProviderType provider, ModContentType contentType, CancellationToken cancellationToken = default)
    {
        return await context.ModCacheSyncStatuses
            .FirstOrDefaultAsync(s => s.Provider == provider && s.ContentType == contentType, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ModCacheSyncStatus>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.ModCacheSyncStatuses
            .OrderBy(s => s.Provider)
            .ThenBy(s => s.ContentType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpsertAsync(ModCacheSyncStatus status, CancellationToken cancellationToken = default)
    {
        var existing = await context.ModCacheSyncStatuses
            .FirstOrDefaultAsync(s => s.Provider == status.Provider && s.ContentType == status.ContentType, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.State = status.State;
            existing.CurrentPage = status.CurrentPage;
            existing.TotalPages = status.TotalPages;
            existing.ItemsSynced = status.ItemsSynced;
            existing.TotalItems = status.TotalItems;
            existing.LastFullSyncStartedAt = status.LastFullSyncStartedAt;
            existing.LastFullSyncCompletedAt = status.LastFullSyncCompletedAt;
            existing.LastIncrementalSyncAt = status.LastIncrementalSyncAt;
            existing.LastError = status.LastError;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            status.Id = Guid.NewGuid();
            status.UpdatedAt = DateTime.UtcNow;
            await context.ModCacheSyncStatuses.AddAsync(status, cancellationToken).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStateAsync(ModProviderType provider, ModContentType contentType, ModCacheSyncState state, CancellationToken cancellationToken = default)
    {
        var existing = await GetOrCreateAsync(provider, contentType, cancellationToken).ConfigureAwait(false);
        existing.State = state;
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateProgressAsync(ModProviderType provider, ModContentType contentType, int currentPage, int totalPages, int itemsSynced, int totalItems, CancellationToken cancellationToken = default)
    {
        var existing = await GetOrCreateAsync(provider, contentType, cancellationToken).ConfigureAwait(false);
        existing.CurrentPage = currentPage;
        existing.TotalPages = totalPages;
        existing.ItemsSynced = itemsSynced;
        existing.TotalItems = totalItems;
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetErrorAsync(ModProviderType provider, ModContentType contentType, string error, CancellationToken cancellationToken = default)
    {
        var existing = await GetOrCreateAsync(provider, contentType, cancellationToken).ConfigureAwait(false);
        existing.LastError = error;
        existing.State = ModCacheSyncState.Failed;
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearErrorAsync(ModProviderType provider, ModContentType contentType, CancellationToken cancellationToken = default)
    {
        var existing = await GetOrCreateAsync(provider, contentType, cancellationToken).ConfigureAwait(false);
        existing.LastError = null;
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ModCacheSyncStatus> GetOrCreateAsync(ModProviderType provider, ModContentType contentType, CancellationToken cancellationToken)
    {
        var existing = await context.ModCacheSyncStatuses
            .FirstOrDefaultAsync(s => s.Provider == provider && s.ContentType == contentType, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var status = new ModCacheSyncStatus
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            ContentType = contentType,
            State = ModCacheSyncState.Idle,
            UpdatedAt = DateTime.UtcNow
        };

        await context.ModCacheSyncStatuses.AddAsync(status, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return status;
    }
}
