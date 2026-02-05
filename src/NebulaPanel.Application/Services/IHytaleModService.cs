using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for orchestrating mod operations across providers for Hytale servers.
/// </summary>
public interface IHytaleModService
{
    /// <summary>
    /// Search mods across all enabled providers for Hytale.
    /// </summary>
    /// <param name="query">Search query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unified search results from all providers.</returns>
    Task<HytaleUnifiedModSearchResult> SearchAsync(
        ModSearchQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Search a specific provider only.
    /// </summary>
    /// <param name="provider">The provider to search.</param>
    /// <param name="query">Search query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Search results from the specified provider.</returns>
    Task<ModSearchResult> SearchProviderAsync(
        ModProviderType provider,
        ModSearchQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Get mod details from a specific provider.
    /// </summary>
    /// <param name="provider">The provider to query.</param>
    /// <param name="modId">Provider-specific mod identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detailed mod information or null if not found.</returns>
    Task<ModDetails?> GetDetailsAsync(
        ModProviderType provider,
        string modId,
        CancellationToken ct = default);

    /// <summary>
    /// Get available versions for a mod.
    /// </summary>
    /// <param name="provider">The provider to query.</param>
    /// <param name="modId">Provider-specific mod identifier.</param>
    /// <param name="gameVersion">Optional game version filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available versions.</returns>
    Task<IReadOnlyList<ModVersion>> GetVersionsAsync(
        ModProviderType provider,
        string modId,
        string? gameVersion = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get featured/popular mods.
    /// </summary>
    /// <param name="count">Number of mods to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of popular mods.</returns>
    Task<IReadOnlyList<ModSearchItem>> GetFeaturedAsync(
        int count = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Install a mod to a server's /Server/mods/ directory.
    /// </summary>
    /// <param name="server">The server to install the mod on.</param>
    /// <param name="provider">The mod provider.</param>
    /// <param name="modId">Provider-specific mod identifier.</param>
    /// <param name="versionId">Provider-specific version identifier.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Installation result.</returns>
    Task<HytaleModInstallResult> InstallModAsync(
        GameServer server,
        ModProviderType provider,
        string modId,
        string versionId,
        IProgress<HytaleModInstallProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Uninstall a mod from a server.
    /// </summary>
    /// <param name="server">The server to uninstall from.</param>
    /// <param name="installedModId">ID of the installed mod record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if successfully uninstalled.</returns>
    Task<bool> UninstallModAsync(
        GameServer server,
        Guid installedModId,
        CancellationToken ct = default);

    /// <summary>
    /// Enable or disable an installed mod.
    /// </summary>
    /// <param name="server">The server containing the mod.</param>
    /// <param name="installedModId">ID of the installed mod record.</param>
    /// <param name="enabled">True to enable, false to disable.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if successfully toggled.</returns>
    Task<bool> SetModEnabledAsync(
        GameServer server,
        Guid installedModId,
        bool enabled,
        CancellationToken ct = default);

    /// <summary>
    /// Check for updates on all installed mods.
    /// </summary>
    /// <param name="server">The server to check updates for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available updates.</returns>
    Task<IReadOnlyList<HytaleModUpdateInfo>> CheckUpdatesAsync(
        GameServer server,
        CancellationToken ct = default);

    /// <summary>
    /// Update a mod to a specific version.
    /// </summary>
    /// <param name="server">The server containing the mod.</param>
    /// <param name="installedModId">ID of the installed mod record.</param>
    /// <param name="newVersionId">Provider-specific version ID to update to.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Installation result for the update.</returns>
    Task<HytaleModInstallResult> UpdateModAsync(
        GameServer server,
        Guid installedModId,
        string newVersionId,
        IProgress<HytaleModInstallProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get installed mods for a server.
    /// </summary>
    /// <param name="server">The server to get mods for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of installed mods with update information.</returns>
    Task<IReadOnlyList<HytaleInstalledModInfo>> GetInstalledModsAsync(
        GameServer server,
        CancellationToken ct = default);

    /// <summary>
    /// Get available tags for filtering.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available tags.</returns>
    Task<IReadOnlyList<string>> GetTagsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get available game versions for filtering.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available game versions.</returns>
    Task<IReadOnlyList<string>> GetGameVersionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get available classifications (PLUGIN, DATA, etc.).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available classifications.</returns>
    Task<IReadOnlyList<string>> GetClassificationsAsync(CancellationToken ct = default);
}
