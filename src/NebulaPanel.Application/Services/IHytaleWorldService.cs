using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for managing Hytale server worlds
/// </summary>
public interface IHytaleWorldService
{
    /// <summary>
    /// Gets all worlds for a server
    /// </summary>
    Task<IEnumerable<HytaleWorldDto>> GetWorldsAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific world's configuration
    /// </summary>
    Task<HytaleWorldConfigDto?> GetWorldConfigAsync(Guid serverId, string worldName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a world's configuration (only modifiable gameplay settings)
    /// </summary>
    Task<bool> UpdateWorldConfigAsync(Guid serverId, UpdateHytaleWorldConfigRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new world with optional configuration
    /// </summary>
    Task<HytaleWorldDto?> CreateWorldAsync(Guid serverId, CreateHytaleWorldRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Duplicates an existing world
    /// </summary>
    Task<HytaleWorldDto?> DuplicateWorldAsync(Guid serverId, DuplicateHytaleWorldRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a world
    /// </summary>
    Task<bool> DeleteWorldAsync(Guid serverId, string worldName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raw config.json content for a world
    /// </summary>
    Task<string?> GetWorldConfigRawAsync(Guid serverId, string worldName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves raw config.json content for a world
    /// </summary>
    Task<bool> SaveWorldConfigRawAsync(Guid serverId, string worldName, string content, CancellationToken cancellationToken = default);
}
