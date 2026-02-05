namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of the Hytale credentials repository.
/// </summary>
public class HytaleCredentialsRepository(NebulaPanelDbContext context) : IHytaleCredentialsRepository
{
    /// <inheritdoc />
    public async Task<HytaleUserCredentials?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.HytaleUserCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(HytaleUserCredentials credentials, CancellationToken ct = default)
    {
        var existing = await context.HytaleUserCredentials
            .FirstOrDefaultAsync(c => c.UserId == credentials.UserId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Update existing record - entity is already tracked, no need to call Update()
            existing.AccessToken = credentials.AccessToken;
            existing.RefreshToken = credentials.RefreshToken;
            existing.ExpiresAt = credentials.ExpiresAt;
            existing.LastRefreshedAt = credentials.LastRefreshedAt;
            existing.LastUsedAt = credentials.LastUsedAt;
            existing.HytaleUsername = credentials.HytaleUsername;
            // Entity is already tracked by EF Core from the query above
        }
        else
        {
            // Insert new record
            await context.HytaleUserCredentials.AddAsync(credentials, ct).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(HytaleUserCredentials credentials, CancellationToken ct = default)
    {
        // Only call Update() if entity is detached; if already tracked, just save
        var entry = context.Entry(credentials);
        if (entry.State == EntityState.Detached)
        {
            context.HytaleUserCredentials.Update(credentials);
        }
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (credentials is not null)
        {
            context.HytaleUserCredentials.Remove(credentials);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HytaleUserCredentials>> GetExpiringWithinAsync(TimeSpan window, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.Add(window);

        return await context.HytaleUserCredentials
            .Where(c => c.ExpiresAt <= cutoff && c.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HytaleUserCredentials>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.HytaleUserCredentials
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
