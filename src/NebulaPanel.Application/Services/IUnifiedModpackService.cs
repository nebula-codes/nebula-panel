using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Unified service for modpack management that aggregates results from multiple modpack providers.
/// Handles searching, browsing, and installing modpacks for game servers.
/// </summary>
public interface IUnifiedModpackService
{
    /// <summary>
    /// Search across all enabled modpack providers.
    /// Results are merged and sorted by relevance.
    /// </summary>
    /// <param name="query">Search parameters including text, filters, and pagination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unified search results from all enabled providers.</returns>
    Task<UnifiedModpackSearchResult> SearchAsync(ModpackSearchQuery query, CancellationToken ct = default);

    /// <summary>
    /// Search for modpacks from a specific provider only.
    /// </summary>
    /// <param name="provider">The specific provider to search.</param>
    /// <param name="query">Search parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Search results from the specified provider.</returns>
    Task<ModpackSearchResult> SearchProviderAsync(
        ModpackProviderType provider,
        ModpackSearchQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Get featured modpacks from all enabled providers.
    /// Useful for browsing/discovery pages.
    /// </summary>
    /// <param name="countPerProvider">Number of featured modpacks to retrieve from each provider.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Combined list of featured modpacks from all providers.</returns>
    Task<IReadOnlyList<ModpackSearchItem>> GetFeaturedAsync(int countPerProvider = 10, CancellationToken ct = default);

    /// <summary>
    /// Get featured modpacks from a specific provider.
    /// </summary>
    /// <param name="provider">The provider to get featured modpacks from.</param>
    /// <param name="count">Number of featured modpacks to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of featured modpacks from the specified provider.</returns>
    Task<IReadOnlyList<ModpackSearchItem>> GetFeaturedFromProviderAsync(
        ModpackProviderType provider,
        int count = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Get modpack details from specific provider.
    /// </summary>
    /// <param name="provider">The modpack provider.</param>
    /// <param name="modpackId">Provider-specific modpack identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detailed modpack information.</returns>
    Task<ModpackDetails> GetDetailsAsync(
        ModpackProviderType provider,
        string modpackId,
        CancellationToken ct = default);

    /// <summary>
    /// Get available versions for a modpack.
    /// </summary>
    /// <param name="provider">The modpack provider.</param>
    /// <param name="modpackId">Provider-specific modpack identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available versions.</returns>
    Task<IReadOnlyList<ModpackVersion>> GetVersionsAsync(
        ModpackProviderType provider,
        string modpackId,
        CancellationToken ct = default);

    /// <summary>
    /// Get server-specific files for a modpack version.
    /// </summary>
    /// <param name="provider">The modpack provider.</param>
    /// <param name="modpackId">Provider-specific modpack identifier.</param>
    /// <param name="versionId">Provider-specific version identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Server files information, or null if no server pack is available.</returns>
    Task<ModpackServerFiles?> GetServerFilesAsync(
        ModpackProviderType provider,
        string modpackId,
        string versionId,
        CancellationToken ct = default);

    /// <summary>
    /// Install a modpack as a new server or on an existing server.
    /// Downloads, extracts, and configures the server with the modpack.
    /// </summary>
    /// <param name="server">The target game server to install the modpack on.</param>
    /// <param name="provider">The modpack provider.</param>
    /// <param name="modpackId">Provider-specific modpack identifier.</param>
    /// <param name="versionId">Provider-specific version identifier.</param>
    /// <param name="progress">Optional progress reporter for installation status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Installation result with details about the installed modpack.</returns>
    Task<ModpackInstallResult> InstallModpackAsync(
        GameServer server,
        ModpackProviderType provider,
        string modpackId,
        string versionId,
        IProgress<ModpackInstallProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets information about all available modpack providers.
    /// </summary>
    /// <returns>List of modpack provider information.</returns>
    IReadOnlyList<ModpackProviderInfo> GetAvailableProviders();

    /// <summary>
    /// Gets information about all enabled modpack providers.
    /// </summary>
    /// <returns>List of enabled modpack provider information.</returns>
    IReadOnlyList<ModpackProviderInfo> GetEnabledProviders();

    /// <summary>
    /// Checks if a specific provider is available and enabled.
    /// </summary>
    /// <param name="provider">The provider type to check.</param>
    /// <returns>True if the provider is available and enabled.</returns>
    bool IsProviderEnabled(ModpackProviderType provider);
}
