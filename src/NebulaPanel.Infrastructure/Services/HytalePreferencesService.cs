namespace NebulaPanel.Infrastructure.Services;

using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;

/// <summary>
/// Service for managing Hytale user preferences.
/// </summary>
public class HytalePreferencesService(IHytalePreferencesRepository repository) : IHytalePreferencesService
{
    /// <inheritdoc />
    public async Task<HytaleUserPreferencesDto> GetPreferencesAsync(Guid userId, CancellationToken ct = default)
    {
        var preferences = await repository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);

        if (preferences is null)
        {
            // Return default preferences if none exist
            return new HytaleUserPreferencesDto(
                AutoRefreshToken: true,
                NotifyAt24Hours: true,
                NotifyAt1Hour: true,
                EmailOnExpiry: false);
        }

        return new HytaleUserPreferencesDto(
            AutoRefreshToken: preferences.AutoRefreshToken,
            NotifyAt24Hours: preferences.NotifyAt24Hours,
            NotifyAt1Hour: preferences.NotifyAt1Hour,
            EmailOnExpiry: preferences.EmailOnExpiry);
    }

    /// <inheritdoc />
    public async Task UpdatePreferencesAsync(Guid userId, UpdateHytalePreferencesRequest request, CancellationToken ct = default)
    {
        var preferences = await repository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);

        if (preferences is null)
        {
            // Create new preferences
            preferences = new HytaleUserPreferences
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AutoRefreshToken = request.AutoRefreshToken,
                NotifyAt24Hours = request.NotifyAt24Hours,
                NotifyAt1Hour = request.NotifyAt1Hour,
                EmailOnExpiry = request.EmailOnExpiry
            };
        }
        else
        {
            // Update existing preferences
            preferences.AutoRefreshToken = request.AutoRefreshToken;
            preferences.NotifyAt24Hours = request.NotifyAt24Hours;
            preferences.NotifyAt1Hour = request.NotifyAt1Hour;
            preferences.EmailOnExpiry = request.EmailOnExpiry;
        }

        await repository.UpsertAsync(preferences, ct).ConfigureAwait(false);
    }
}
