using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class ApiKeyRepository(NebulaPanelDbContext context) : IApiKeyRepository
{
    public async Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.ApiKeys
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return await context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        await context.ApiKeys.AddAsync(apiKey, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        context.ApiKeys.Update(apiKey);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
