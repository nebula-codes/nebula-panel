namespace NebulaPanel.Domain.Repositories;

using NebulaPanel.Domain.Entities;

/// <summary>
/// Repository for managing Hytale user credentials persistence.
/// </summary>
public interface IHytaleCredentialsRepository
{
    /// <summary>
    /// Gets credentials for a specific user.
    /// </summary>
    /// <param name="userId">The user ID to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The credentials if found, null otherwise.</returns>
    Task<HytaleUserCredentials?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates credentials for a user.
    /// If credentials already exist for the user, they are updated.
    /// </summary>
    /// <param name="credentials">The credentials to upsert.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpsertAsync(HytaleUserCredentials credentials, CancellationToken ct = default);

    /// <summary>
    /// Updates existing credentials.
    /// </summary>
    /// <param name="credentials">The credentials to update.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(HytaleUserCredentials credentials, CancellationToken ct = default);

    /// <summary>
    /// Deletes credentials for a specific user.
    /// </summary>
    /// <param name="userId">The user ID whose credentials to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets all credentials that expire within the specified time window.
    /// </summary>
    /// <param name="window">The time window from now to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of credentials expiring within the window.</returns>
    Task<IReadOnlyList<HytaleUserCredentials>> GetExpiringWithinAsync(TimeSpan window, CancellationToken ct = default);

    /// <summary>
    /// Gets all stored credentials.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all credentials.</returns>
    Task<IReadOnlyList<HytaleUserCredentials>> GetAllAsync(CancellationToken ct = default);
}
