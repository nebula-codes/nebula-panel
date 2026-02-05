namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of the Hytale preferences repository.
/// </summary>
public class HytalePreferencesRepository(NebulaPanelDbContext context) : IHytalePreferencesRepository
{
    /// <inheritdoc />
    public async Task<HytaleUserPreferences?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.HytaleUserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(HytaleUserPreferences preferences, CancellationToken ct = default)
    {
        var existing = await context.HytaleUserPreferences
            .FirstOrDefaultAsync(p => p.UserId == preferences.UserId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Update existing record
            existing.AutoRefreshToken = preferences.AutoRefreshToken;
            existing.NotifyAt24Hours = preferences.NotifyAt24Hours;
            existing.NotifyAt1Hour = preferences.NotifyAt1Hour;
            existing.EmailOnExpiry = preferences.EmailOnExpiry;

            context.HytaleUserPreferences.Update(existing);
        }
        else
        {
            // Insert new record
            await context.HytaleUserPreferences.AddAsync(preferences, ct).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        var preferences = await GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (preferences is not null)
        {
            context.HytaleUserPreferences.Remove(preferences);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
