using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Repositories;

/// <summary>
/// Repository interface for managing server mod entities.
/// </summary>
public interface IServerModRepository
{
    /// <summary>
    /// Gets all mods installed on a specific server.
    /// </summary>
    /// <param name="serverId">The server's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of installed mods.</returns>
    Task<IReadOnlyList<ServerMod>> GetByServerIdAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific installed mod by its ID.
    /// </summary>
    /// <param name="id">The mod installation's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The installed mod or null if not found.</returns>
    Task<ServerMod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific mod by server, provider, and mod ID combination.
    /// </summary>
    /// <param name="serverId">The server's unique identifier.</param>
    /// <param name="provider">The mod provider type.</param>
    /// <param name="modId">The provider-specific mod identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The installed mod or null if not found.</returns>
    Task<ServerMod?> GetByServerAndModAsync(
        Guid serverId,
        ModProviderType provider,
        string modId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new mod installation record.
    /// </summary>
    /// <param name="mod">The mod to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The added mod with generated ID.</returns>
    Task<ServerMod> AddAsync(ServerMod mod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing mod installation record.
    /// </summary>
    /// <param name="mod">The mod to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(ServerMod mod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a mod installation record.
    /// </summary>
    /// <param name="id">The mod installation's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a mod is already installed on a server.
    /// </summary>
    /// <param name="serverId">The server's unique identifier.</param>
    /// <param name="provider">The mod provider type.</param>
    /// <param name="modId">The provider-specific mod identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the mod is already installed.</returns>
    Task<bool> ExistsAsync(
        Guid serverId,
        ModProviderType provider,
        string modId,
        CancellationToken cancellationToken = default);
}
