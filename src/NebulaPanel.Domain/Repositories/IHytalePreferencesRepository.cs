namespace NebulaPanel.Domain.Repositories;

using NebulaPanel.Domain.Entities;

/// <summary>
/// Repository for managing Hytale user preferences persistence.
/// </summary>
public interface IHytalePreferencesRepository
{
    /// <summary>
    /// Gets preferences for a specific user.
    /// </summary>
    /// <param name="userId">The user ID to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The preferences if found, null otherwise.</returns>
    Task<HytaleUserPreferences?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates preferences for a user.
    /// If preferences already exist for the user, they are updated.
    /// </summary>
    /// <param name="preferences">The preferences to upsert.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpsertAsync(HytaleUserPreferences preferences, CancellationToken ct = default);

    /// <summary>
    /// Deletes preferences for a specific user.
    /// </summary>
    /// <param name="userId">The user ID whose preferences to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(Guid userId, CancellationToken ct = default);
}
