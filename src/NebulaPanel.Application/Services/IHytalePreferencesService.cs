namespace NebulaPanel.Application.Services;

using NebulaPanel.Application.DTOs;

/// <summary>
/// Service for managing Hytale user preferences.
/// </summary>
public interface IHytalePreferencesService
{
    /// <summary>
    /// Gets preferences for a specific user.
    /// Returns default preferences if none exist.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user's Hytale preferences.</returns>
    Task<HytaleUserPreferencesDto> GetPreferencesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Updates preferences for a specific user.
    /// Creates preferences if they don't exist.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdatePreferencesAsync(Guid userId, UpdateHytalePreferencesRequest request, CancellationToken ct = default);
}
