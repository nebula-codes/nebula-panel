using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for managing official games in the admin UI.
/// Coordinates between the registry, repository, and providers.
/// </summary>
public interface IOfficialGameService
{
    /// <summary>
    /// Gets all official games with their current status.
    /// </summary>
    Task<IReadOnlyList<OfficialGameStatusDto>> GetAllOfficialGamesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about an official game.
    /// </summary>
    Task<Result<OfficialGameDetailDto>> GetOfficialGameAsync(
        string gameSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables an official game.
    /// </summary>
    Task<Result> SetOfficialGameEnabledAsync(
        string gameSlug,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a refresh of available versions for an official game.
    /// </summary>
    Task<Result<RefreshVersionsResultDto>> RefreshVersionsAsync(
        string gameSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available versions for an official game.
    /// </summary>
    Task<Result<IReadOnlyList<GameVersionDto>>> GetAvailableVersionsAsync(
        string gameSlug,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes mod provider configuration from the game definition to the database.
    /// </summary>
    Task<Result> SyncModProvidersAsync(
        string gameSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes all official games' mod providers from their definitions to the database.
    /// </summary>
    Task SyncAllModProvidersAsync(CancellationToken cancellationToken = default);
}
