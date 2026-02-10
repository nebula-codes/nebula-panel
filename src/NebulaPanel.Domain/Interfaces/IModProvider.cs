using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Provider interface for mod search, download, and management.
/// Each implementation handles a specific mod repository (Modrinth, CurseForge, etc.).
/// </summary>
public interface IModProvider
{
    /// <summary>
    /// The type of mod provider this implementation handles.
    /// </summary>
    ModProviderType ProviderType { get; }

    /// <summary>
    /// Human-readable name for display (e.g., "Modrinth", "CurseForge").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// URL to the provider's logo/icon for UI display.
    /// </summary>
    string? IconUrl { get; }

    /// <summary>
    /// Checks if this provider supports mods for the specified game.
    /// </summary>
    /// <param name="gameSlug">The game's unique slug identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the provider supports the game.</returns>
    Task<bool> SupportsGameAsync(string gameSlug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for mods matching the query parameters.
    /// </summary>
    /// <param name="query">Search parameters including text, filters, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated search results.</returns>
    Task<ModSearchResult> SearchAsync(ModSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific mod.
    /// </summary>
    /// <param name="modId">Provider-specific mod identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed mod information or null if not found.</returns>
    Task<ModDetails?> GetDetailsAsync(string modId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available versions for a mod, optionally filtered by game version.
    /// </summary>
    /// <param name="modId">Provider-specific mod identifier.</param>
    /// <param name="gameVersion">Optional game version to filter compatible versions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available versions.</returns>
    Task<IReadOnlyList<ModVersion>> GetVersionsAsync(
        string modId,
        string? gameVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a specific version of a mod to the destination path.
    /// </summary>
    /// <param name="modId">Provider-specific mod identifier.</param>
    /// <param name="versionId">Provider-specific version identifier.</param>
    /// <param name="destinationPath">Directory to save the downloaded file.</param>
    /// <param name="progress">Optional progress reporter for download status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Download result with file path or error.</returns>
    Task<ModDownloadResult> DownloadAsync(
        string modId,
        string versionId,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for available updates for installed mods.
    /// </summary>
    /// <param name="installedMods">Information about currently installed mods.</param>
    /// <param name="gameVersion">Optional game version to filter compatible updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available updates.</returns>
    Task<IReadOnlyList<ModUpdateInfo>> CheckUpdatesAsync(
        IEnumerable<InstalledModInfo> installedMods,
        string? gameVersion,
        CancellationToken cancellationToken = default);
}

#region DTOs used by IModProvider

/// <summary>
/// Search query parameters for mod search.
/// </summary>
public record ModSearchQuery(
    string? Query,
    string? GameSlug,
    string? GameVersion,
    string? ModLoader,
    ModSearchSort Sort = ModSearchSort.Relevance,
    int Page = 1,
    int PageSize = 20,
    List<string>? Categories = null,
    ModContentType ContentType = ModContentType.Mod
);

/// <summary>
/// Sort options for mod search results.
/// </summary>
public enum ModSearchSort
{
    Relevance,
    Downloads,
    Updated,
    Created,
    Name
}

/// <summary>
/// Paginated search results from a single provider.
/// </summary>
public record ModSearchResult(
    IReadOnlyList<ModSearchItem> Mods,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>
/// Summary information for a mod in search results.
/// </summary>
public record ModSearchItem(
    string Id,
    string Slug,
    string Name,
    string? Summary,
    string? IconUrl,
    string? Author,
    long Downloads,
    DateTime? UpdatedAt,
    List<string> Categories,
    List<string> GameVersions,
    ModProviderType Provider
);

/// <summary>
/// Detailed information about a mod.
/// </summary>
public record ModDetails(
    string Id,
    string Slug,
    string Name,
    string? Summary,
    string? Description,
    string? IconUrl,
    string? BannerUrl,
    string? SourceUrl,
    string? WikiUrl,
    string? DiscordUrl,
    List<ModAuthor> Authors,
    long Downloads,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<string> Categories,
    List<string> GameVersions,
    List<string> Loaders,
    List<ModScreenshot> Screenshots,
    ModProviderType Provider
);

/// <summary>
/// Mod author information.
/// </summary>
public record ModAuthor(string Name, string? Url);

/// <summary>
/// Mod screenshot information.
/// </summary>
public record ModScreenshot(string Url, string? Title);

/// <summary>
/// Information about a specific mod version.
/// </summary>
public record ModVersion(
    string Id,
    string Version,
    string? Name,
    string? Changelog,
    List<string> GameVersions,
    List<string> Loaders,
    DateTime ReleasedAt,
    long Downloads,
    ModVersionType VersionType,
    List<ModDependency> Dependencies,
    List<ModFile> Files
);

/// <summary>
/// Type of mod version release.
/// </summary>
public enum ModVersionType
{
    Release,
    Beta,
    Alpha
}

/// <summary>
/// Mod dependency information.
/// </summary>
public record ModDependency(
    string ModId,
    string? ModName,
    string? VersionId,
    ModDependencyType Type
);

/// <summary>
/// Type of mod dependency relationship.
/// </summary>
public enum ModDependencyType
{
    Required,
    Optional,
    Incompatible,
    Embedded
}

/// <summary>
/// Downloadable file information.
/// </summary>
public record ModFile(
    string FileName,
    string Url,
    long SizeBytes,
    string? Sha512,
    string? Sha1,
    bool IsPrimary
);

/// <summary>
/// Result of a mod download operation.
/// </summary>
public record ModDownloadResult(
    bool Success,
    string? FilePath,
    string? Error,
    string? FileHash
);

/// <summary>
/// Progress information during download.
/// </summary>
public record DownloadProgress(
    long BytesDownloaded,
    long TotalBytes,
    double Percentage
);

/// <summary>
/// Information about an installed mod for update checking.
/// </summary>
public record InstalledModInfo(
    string ModId,
    string VersionId,
    ModProviderType Provider
);

/// <summary>
/// Information about an available mod update.
/// </summary>
public record ModUpdateInfo(
    Guid InstalledModId,
    string ModName,
    string CurrentVersion,
    string LatestVersion,
    string LatestVersionId,
    DateTime ReleasedAt,
    string? Changelog,
    ModProviderType Provider
);

#endregion
