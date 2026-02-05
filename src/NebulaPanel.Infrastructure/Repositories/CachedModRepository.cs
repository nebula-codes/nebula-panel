using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class CachedModRepository(NebulaPanelDbContext context) : ICachedModRepository
{
    public async Task<CachedMod?> GetByProviderIdAsync(ModProviderType provider, string providerModId, CancellationToken cancellationToken = default)
    {
        return await context.CachedMods
            .FirstOrDefaultAsync(m => m.Provider == provider && m.ProviderModId == providerModId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CachedMod>> SearchAsync(
        string? query,
        ModProviderType? provider,
        ModContentType? contentType,
        string? gameVersion,
        string? loader,
        string? category,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var queryable = BuildSearchQuery(query, provider, contentType, gameVersion, loader, category);

        return await queryable
            .OrderByDescending(m => m.Downloads)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountAsync(
        string? query,
        ModProviderType? provider,
        ModContentType? contentType,
        string? gameVersion,
        string? loader,
        string? category,
        CancellationToken cancellationToken = default)
    {
        var queryable = BuildSearchQuery(query, provider, contentType, gameVersion, loader, category);

        return await queryable
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetTotalCountAsync(ModProviderType? provider = null, ModContentType? contentType = null, CancellationToken cancellationToken = default)
    {
        var query = context.CachedMods.AsQueryable();

        if (provider.HasValue)
        {
            query = query.Where(m => m.Provider == provider.Value);
        }

        if (contentType.HasValue)
        {
            query = query.Where(m => m.ContentType == contentType.Value);
        }

        return await query.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DateTime?> GetOldestCacheEntryAsync(CancellationToken cancellationToken = default)
    {
        return await context.CachedMods
            .MinAsync(m => (DateTime?)m.CachedAt, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpsertAsync(CachedMod mod, CancellationToken cancellationToken = default)
    {
        var existing = await context.CachedMods
            .FirstOrDefaultAsync(m => m.Provider == mod.Provider && m.ProviderModId == mod.ProviderModId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.Slug = mod.Slug;
            existing.Name = mod.Name;
            existing.Summary = mod.Summary;
            existing.IconUrl = mod.IconUrl;
            existing.Author = mod.Author;
            existing.Downloads = mod.Downloads;
            existing.UpdatedAt = mod.UpdatedAt;
            existing.ContentType = mod.ContentType;
            existing.CategoriesJson = mod.CategoriesJson;
            existing.GameVersionsJson = mod.GameVersionsJson;
            existing.LoadersJson = mod.LoadersJson;
            existing.CachedAt = DateTime.UtcNow;
        }
        else
        {
            mod.Id = Guid.NewGuid();
            mod.CachedAt = DateTime.UtcNow;
            await context.CachedMods.AddAsync(mod, cancellationToken).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertBatchAsync(IEnumerable<CachedMod> mods, CancellationToken cancellationToken = default)
    {
        var modsList = mods.ToList();
        if (modsList.Count == 0) return;

        foreach (var mod in modsList)
        {
            var existing = await context.CachedMods
                .FirstOrDefaultAsync(m => m.Provider == mod.Provider && m.ProviderModId == mod.ProviderModId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.Slug = mod.Slug;
                existing.Name = mod.Name;
                existing.Summary = mod.Summary;
                existing.IconUrl = mod.IconUrl;
                existing.Author = mod.Author;
                existing.Downloads = mod.Downloads;
                existing.UpdatedAt = mod.UpdatedAt;
                existing.ContentType = mod.ContentType;
                existing.CategoriesJson = mod.CategoriesJson;
                existing.GameVersionsJson = mod.GameVersionsJson;
                existing.LoadersJson = mod.LoadersJson;
                existing.CachedAt = DateTime.UtcNow;
            }
            else
            {
                mod.Id = Guid.NewGuid();
                mod.CachedAt = DateTime.UtcNow;
                await context.CachedMods.AddAsync(mod, cancellationToken).ConfigureAwait(false);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteByProviderAsync(ModProviderType provider, CancellationToken cancellationToken = default)
    {
        await context.CachedMods
            .Where(m => m.Provider == provider)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateDetailsAsync(Guid id, string? descriptionHtml, string? versionsJson, CancellationToken cancellationToken = default)
    {
        var mod = await context.CachedMods.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (mod is null) return;

        if (descriptionHtml is not null)
        {
            mod.DescriptionHtml = descriptionHtml;
        }

        if (versionsJson is not null)
        {
            mod.VersionsJson = versionsJson;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private IQueryable<CachedMod> BuildSearchQuery(
        string? query,
        ModProviderType? provider,
        ModContentType? contentType,
        string? gameVersion,
        string? loader,
        string? category)
    {
        var queryable = context.CachedMods.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchTerm = query.ToLowerInvariant();
            queryable = queryable.Where(m =>
                m.Name.ToLower().Contains(searchTerm) ||
                (m.Summary != null && m.Summary.ToLower().Contains(searchTerm)) ||
                m.Slug.ToLower().Contains(searchTerm));
        }

        if (provider.HasValue)
        {
            queryable = queryable.Where(m => m.Provider == provider.Value);
        }

        if (contentType.HasValue)
        {
            queryable = queryable.Where(m => m.ContentType == contentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            queryable = queryable.Where(m => m.GameVersionsJson.Contains(gameVersion));
        }

        if (!string.IsNullOrWhiteSpace(loader))
        {
            queryable = queryable.Where(m => m.LoadersJson.Contains(loader));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            queryable = queryable.Where(m => m.CategoriesJson.Contains(category));
        }

        return queryable;
    }
}
