using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Repositories;

public interface ICachedModRepository
{
    Task<CachedMod?> GetByProviderIdAsync(ModProviderType provider, string providerModId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CachedMod>> SearchAsync(
        string? query,
        ModProviderType? provider,
        ModContentType? contentType,
        string? gameVersion,
        string? loader,
        string? category,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        string? query,
        ModProviderType? provider,
        ModContentType? contentType,
        string? gameVersion,
        string? loader,
        string? category,
        CancellationToken cancellationToken = default);

    Task<int> GetTotalCountAsync(ModProviderType? provider = null, ModContentType? contentType = null, CancellationToken cancellationToken = default);

    Task<DateTime?> GetOldestCacheEntryAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(CachedMod mod, CancellationToken cancellationToken = default);

    Task UpsertBatchAsync(IEnumerable<CachedMod> mods, CancellationToken cancellationToken = default);

    Task DeleteByProviderAsync(ModProviderType provider, CancellationToken cancellationToken = default);

    Task UpdateDetailsAsync(Guid id, string? descriptionHtml, string? versionsJson, CancellationToken cancellationToken = default);
}
