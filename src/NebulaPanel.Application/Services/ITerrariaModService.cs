using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for managing mods on Terraria/tModLoader servers.
/// </summary>
public interface ITerrariaModService
{
    /// <summary>
    /// Gets all installed mods for a server.
    /// </summary>
    /// <param name="server">The server to get mods for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of installed mods.</returns>
    Task<IReadOnlyList<TerrariaInstalledMod>> GetInstalledModsAsync(
        GameServer server,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a mod from Steam Workshop.
    /// </summary>
    /// <param name="server">The server to install the mod on.</param>
    /// <param name="workshopId">The Steam Workshop item ID.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Installation result.</returns>
    Task<TerrariaModInstallResult> InstallModAsync(
        GameServer server,
        string workshopId,
        IProgress<TerrariaModInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs multiple mods from Steam Workshop.
    /// </summary>
    /// <param name="server">The server to install mods on.</param>
    /// <param name="workshopIds">The Steam Workshop item IDs.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of mods successfully installed.</returns>
    Task<TerrarriaModBulkInstallResult> InstallModsAsync(
        GameServer server,
        IEnumerable<string> workshopIds,
        IProgress<TerrariaModInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a mod from a server.
    /// </summary>
    /// <param name="server">The server to uninstall from.</param>
    /// <param name="modId">ID of the installed mod.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully uninstalled.</returns>
    Task<bool> UninstallModAsync(
        GameServer server,
        Guid modId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables an installed mod.
    /// </summary>
    /// <param name="server">The server containing the mod.</param>
    /// <param name="modId">ID of the installed mod.</param>
    /// <param name="enabled">True to enable, false to disable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully toggled.</returns>
    Task<bool> SetModEnabledAsync(
        GameServer server,
        Guid modId,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a modpack from JSON format.
    /// </summary>
    /// <param name="server">The server to import mods to.</param>
    /// <param name="json">JSON string containing mod list.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with number of mods imported.</returns>
    Task<TerrarriaModBulkInstallResult> ImportModpackAsync(
        GameServer server,
        string json,
        IProgress<TerrariaModInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports installed mods to JSON format.
    /// </summary>
    /// <param name="server">The server to export mods from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON string of installed mods.</returns>
    Task<string> ExportModpackAsync(
        GameServer server,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports mods from a Steam Workshop collection.
    /// </summary>
    /// <param name="server">The server to import mods to.</param>
    /// <param name="collectionUrl">Workshop collection URL or ID.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with number of mods imported.</returns>
    Task<TerrarriaModBulkInstallResult> ImportCollectionAsync(
        GameServer server,
        string collectionUrl,
        IProgress<TerrariaModInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches Steam Workshop for Terraria mods.
    /// </summary>
    /// <param name="query">Search query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results.</returns>
    Task<ModSearchResult> SearchAsync(
        ModSearchQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets details about a specific Workshop mod.
    /// </summary>
    /// <param name="workshopId">The Steam Workshop item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mod details if found.</returns>
    Task<ModDetails?> GetModDetailsAsync(
        string workshopId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews a modpack import without installing.
    /// </summary>
    /// <param name="json">JSON string containing mod list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of mods that would be installed.</returns>
    Task<IReadOnlyList<TerrariaModPreview>> PreviewModpackAsync(
        string json,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews a collection import without installing.
    /// </summary>
    /// <param name="collectionUrl">Workshop collection URL or ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of mods that would be installed.</returns>
    Task<TerrariaCollectionPreview?> PreviewCollectionAsync(
        string collectionUrl,
        CancellationToken cancellationToken = default);
}
