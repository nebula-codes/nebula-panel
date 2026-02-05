using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Unified service for mod management that aggregates results from multiple mod providers.
/// Handles searching, installing, updating, and uninstalling mods for game servers.
/// </summary>
public interface IUnifiedModService
{
    /// <summary>
    /// Searches for mods across all enabled providers for a server.
    /// Results are deduplicated and merged by priority.
    /// </summary>
    /// <param name="server">The game server to search mods for.</param>
    /// <param name="query">Search parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unified search results from all providers.</returns>
    Task<UnifiedModSearchResult> SearchAsync(
        GameServer server,
        ModSearchQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for mods from a specific provider only.
    /// </summary>
    /// <param name="server">The game server context.</param>
    /// <param name="provider">The specific provider to search.</param>
    /// <param name="query">Search parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results from the specified provider.</returns>
    Task<ModSearchResult> SearchProviderAsync(
        GameServer server,
        ModProviderType provider,
        ModSearchQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a mod from a specific provider.
    /// </summary>
    /// <param name="provider">The mod provider.</param>
    /// <param name="modId">Provider-specific mod identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed mod information or null if not found.</returns>
    Task<ModDetails?> GetDetailsAsync(
        ModProviderType provider,
        string modId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available versions for a mod from a specific provider.
    /// </summary>
    /// <param name="provider">The mod provider.</param>
    /// <param name="modId">Provider-specific mod identifier.</param>
    /// <param name="gameVersion">Optional game version to filter compatible versions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available versions.</returns>
    Task<IReadOnlyList<ModVersion>> GetVersionsAsync(
        ModProviderType provider,
        string modId,
        string? gameVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a mod on a server. Downloads the mod and creates the database record.
    /// </summary>
    /// <param name="server">The target game server.</param>
    /// <param name="provider">The mod provider.</param>
    /// <param name="modId">Provider-specific mod identifier.</param>
    /// <param name="versionId">Optional specific version (latest if null).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Installation result.</returns>
    Task<InstallResult> InstallAsync(
        GameServer server,
        ModProviderType provider,
        string modId,
        string? versionId,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an installed mod to the latest compatible version.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="installedModId">The installed mod's database ID.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update result.</returns>
    Task<InstallResult> UpdateAsync(
        GameServer server,
        Guid installedModId,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a mod from a server. Removes files and database record.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="installedModId">The installed mod's database ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> UninstallAsync(
        GameServer server,
        Guid installedModId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all mods installed on a server.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of installed mods.</returns>
    Task<IReadOnlyList<ServerModDto>> GetInstalledAsync(
        GameServer server,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a mod's enabled state. When disabled, the mod file is renamed with a .disabled suffix.
    /// When enabled, the .disabled suffix is removed.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="installedModId">The installed mod's database ID.</param>
    /// <param name="enabled">Optional explicit state. If null, toggles the current state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated mod information.</returns>
    Task<Result<ServerModDto>> ToggleAsync(
        GameServer server,
        Guid installedModId,
        bool? enabled = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for available updates for all installed mods on a server.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available updates.</returns>
    Task<IReadOnlyList<ModUpdateInfo>> CheckAllUpdatesAsync(
        GameServer server,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of mod providers configured for a game.
    /// </summary>
    /// <param name="game">The game to get providers for.</param>
    /// <returns>List of provider information.</returns>
    IReadOnlyList<ModProviderInfo> GetProvidersForGame(Game game);
}
