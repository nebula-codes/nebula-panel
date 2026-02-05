using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Provider interface for modpack search, download, and installation.
/// Each implementation handles a specific modpack repository (CurseForge, Modrinth, FTB, etc.).
/// </summary>
public interface IModpackProvider
{
    /// <summary>
    /// The type of modpack provider this implementation handles.
    /// </summary>
    ModpackProviderType ProviderType { get; }

    /// <summary>
    /// Human-readable name for display (e.g., "CurseForge", "Modrinth").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// URL to the provider's logo/icon for UI display.
    /// </summary>
    string? IconUrl { get; }

    /// <summary>
    /// Search for modpacks.
    /// </summary>
    /// <param name="query">Search parameters including text, filters, and pagination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated search results.</returns>
    Task<ModpackSearchResult> SearchAsync(ModpackSearchQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get featured/popular modpacks for browsing.
    /// </summary>
    /// <param name="count">Number of featured modpacks to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of featured modpacks.</returns>
    Task<IReadOnlyList<ModpackSearchItem>> GetFeaturedAsync(int count = 20, CancellationToken ct = default);

    /// <summary>
    /// Get detailed modpack information.
    /// </summary>
    /// <param name="modpackId">Provider-specific modpack identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detailed modpack information.</returns>
    Task<ModpackDetails> GetDetailsAsync(string modpackId, CancellationToken ct = default);

    /// <summary>
    /// Get available versions/files for a modpack.
    /// </summary>
    /// <param name="modpackId">Provider-specific modpack identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available versions.</returns>
    Task<IReadOnlyList<ModpackVersion>> GetVersionsAsync(string modpackId, CancellationToken ct = default);

    /// <summary>
    /// Get server-specific files for a modpack version.
    /// </summary>
    /// <param name="modpackId">Provider-specific modpack identifier.</param>
    /// <param name="versionId">Provider-specific version identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Server files information, or null if no server pack is available.</returns>
    Task<ModpackServerFiles?> GetServerFilesAsync(string modpackId, string versionId, CancellationToken ct = default);

    /// <summary>
    /// Download and extract modpack server files.
    /// </summary>
    /// <param name="modpackId">Provider-specific modpack identifier.</param>
    /// <param name="versionId">Provider-specific version identifier.</param>
    /// <param name="destinationPath">Directory to extract server files to.</param>
    /// <param name="progress">Optional progress reporter for download status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Download result with extracted path or error.</returns>
    Task<ModpackDownloadResult> DownloadServerFilesAsync(
        string modpackId,
        string versionId,
        string destinationPath,
        IProgress<ModpackDownloadProgress>? progress = null,
        CancellationToken ct = default);
}

#region Modpack Query and Search DTOs

/// <summary>
/// Search query parameters for modpack search.
/// </summary>
public record ModpackSearchQuery
{
    /// <summary>
    /// Search text query.
    /// </summary>
    public string Query { get; init; } = "";

    /// <summary>
    /// Filter by Minecraft version (e.g., "1.20.1").
    /// </summary>
    public string? MinecraftVersion { get; init; }

    /// <summary>
    /// Filter by mod loader type.
    /// </summary>
    public ModLoaderType? LoaderType { get; init; }

    /// <summary>
    /// Sort order for results.
    /// </summary>
    public ModpackSearchSort Sort { get; init; } = ModpackSearchSort.Popularity;

    /// <summary>
    /// Page number (1-based).
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Number of results per page.
    /// </summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Sort options for modpack search results.
/// </summary>
public enum ModpackSearchSort
{
    /// <summary>
    /// Sort by popularity/trending.
    /// </summary>
    Popularity,

    /// <summary>
    /// Sort by most recently updated.
    /// </summary>
    RecentlyUpdated,

    /// <summary>
    /// Sort by total download count.
    /// </summary>
    TotalDownloads,

    /// <summary>
    /// Sort alphabetically by name.
    /// </summary>
    Name
}

/// <summary>
/// Paginated search results from a modpack provider.
/// </summary>
public record ModpackSearchResult
{
    /// <summary>
    /// List of modpacks matching the search query.
    /// </summary>
    public IReadOnlyList<ModpackSearchItem> Modpacks { get; init; } = [];

    /// <summary>
    /// Total number of results available.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Current page number.
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Number of results per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Total number of pages available.
    /// </summary>
    public int TotalPages { get; init; }
}

/// <summary>
/// Summary information for a modpack in search results.
/// </summary>
public record ModpackSearchItem
{
    /// <summary>
    /// Provider-specific unique identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// URL-friendly identifier.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Display name of the modpack.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Brief description of the modpack.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// URL to the modpack's icon/logo.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Primary author or team name.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Total download count.
    /// </summary>
    public long Downloads { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// Supported Minecraft versions.
    /// </summary>
    public List<string> MinecraftVersions { get; init; } = [];

    /// <summary>
    /// Supported mod loaders.
    /// </summary>
    public List<ModLoaderType> Loaders { get; init; } = [];

    /// <summary>
    /// Which provider this modpack is from.
    /// </summary>
    public ModpackProviderType Provider { get; init; }

    /// <summary>
    /// Whether this modpack has dedicated server files available.
    /// Important: not all modpacks have server packs.
    /// </summary>
    public bool HasServerPack { get; init; }
}

#endregion

#region Modpack Details DTOs

/// <summary>
/// Detailed information about a modpack.
/// </summary>
public record ModpackDetails
{
    /// <summary>
    /// Provider-specific unique identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// URL-friendly identifier.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Display name of the modpack.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Brief description of the modpack.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Full description (markdown or HTML).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// URL to the modpack's icon/logo.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// URL to the modpack's banner image.
    /// </summary>
    public string? BannerUrl { get; init; }

    /// <summary>
    /// Authors/team members of the modpack.
    /// </summary>
    public List<ModpackAuthor> Authors { get; init; } = [];

    /// <summary>
    /// Total download count.
    /// </summary>
    public long Downloads { get; init; }

    /// <summary>
    /// When the modpack was first created/published.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the modpack was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Categories/tags for the modpack.
    /// </summary>
    public List<string> Categories { get; init; } = [];

    /// <summary>
    /// Supported Minecraft versions.
    /// </summary>
    public List<string> MinecraftVersions { get; init; } = [];

    /// <summary>
    /// Supported mod loaders.
    /// </summary>
    public List<ModLoaderType> Loaders { get; init; } = [];

    /// <summary>
    /// Gallery screenshots.
    /// </summary>
    public List<ModpackScreenshot> Screenshots { get; init; } = [];

    /// <summary>
    /// External links (Wiki, Discord, GitHub, etc.).
    /// </summary>
    public List<ModpackLink> Links { get; init; } = [];

    /// <summary>
    /// Which provider this modpack is from.
    /// </summary>
    public ModpackProviderType Provider { get; init; }

    /// <summary>
    /// Number of mods included in the modpack.
    /// </summary>
    public int ModCount { get; init; }

    /// <summary>
    /// Whether this modpack has dedicated server files available.
    /// </summary>
    public bool HasServerPack { get; init; }
}

/// <summary>
/// Modpack author information.
/// </summary>
public record ModpackAuthor
{
    /// <summary>
    /// Author's display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// URL to the author's profile.
    /// </summary>
    public string? Url { get; init; }
}

/// <summary>
/// Modpack screenshot/gallery image.
/// </summary>
public record ModpackScreenshot
{
    /// <summary>
    /// URL to the image.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Optional title/caption for the screenshot.
    /// </summary>
    public string? Title { get; init; }
}

/// <summary>
/// External link for a modpack.
/// </summary>
public record ModpackLink
{
    /// <summary>
    /// Type of link (e.g., "wiki", "discord", "github", "website").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// URL of the link.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Display name for the link.
    /// </summary>
    public string? Name { get; init; }
}

#endregion

#region Modpack Version DTOs

/// <summary>
/// Information about a specific modpack version.
/// </summary>
public record ModpackVersion
{
    /// <summary>
    /// Provider-specific version identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Version name/number (e.g., "1.5.0", "2.0-beta").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Minecraft version this pack version targets (e.g., "1.20.1").
    /// </summary>
    public required string MinecraftVersion { get; init; }

    /// <summary>
    /// Mod loader type for this version.
    /// </summary>
    public ModLoaderType Loader { get; init; }

    /// <summary>
    /// Specific loader version (e.g., "0.15.3" for Fabric).
    /// </summary>
    public string? LoaderVersion { get; init; }

    /// <summary>
    /// When this version was released.
    /// </summary>
    public DateTime ReleasedAt { get; init; }

    /// <summary>
    /// Number of downloads for this specific version.
    /// </summary>
    public long Downloads { get; init; }

    /// <summary>
    /// Release type (release, beta, alpha).
    /// </summary>
    public ModpackVersionType Type { get; init; }

    /// <summary>
    /// Changelog/release notes for this version.
    /// </summary>
    public string? Changelog { get; init; }

    /// <summary>
    /// Whether this version has a dedicated server pack available.
    /// </summary>
    public bool HasServerPack { get; init; }

    /// <summary>
    /// Size of the server pack in bytes, if available.
    /// </summary>
    public long? ServerPackSize { get; init; }
}

/// <summary>
/// Type of modpack version release.
/// </summary>
public enum ModpackVersionType
{
    /// <summary>
    /// Stable release.
    /// </summary>
    Release,

    /// <summary>
    /// Beta/preview release.
    /// </summary>
    Beta,

    /// <summary>
    /// Alpha/experimental release.
    /// </summary>
    Alpha
}

#endregion

#region Server Files and Download DTOs

/// <summary>
/// Information about server-specific files for a modpack version.
/// </summary>
public record ModpackServerFiles
{
    /// <summary>
    /// Direct download URL for the server pack.
    /// </summary>
    public required string DownloadUrl { get; init; }

    /// <summary>
    /// Size of the server pack in bytes.
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// SHA-256 hash of the file for verification.
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Type of server file package.
    /// </summary>
    public ModpackServerFileType FileType { get; init; }

    /// <summary>
    /// Required Java version (e.g., "17", "21").
    /// </summary>
    public string? RequiredJavaVersion { get; init; }

    /// <summary>
    /// Recommended RAM allocation in megabytes.
    /// </summary>
    public long? RecommendedRamMb { get; init; }
}

/// <summary>
/// Type of server file package.
/// </summary>
public enum ModpackServerFileType
{
    /// <summary>
    /// Ready-to-run server files (extract and run).
    /// </summary>
    ServerPack,

    /// <summary>
    /// Full client pack (need to extract server files manually).
    /// </summary>
    FullPack,

    /// <summary>
    /// Forge-style installer JAR that needs to be executed.
    /// </summary>
    InstallerJar
}

/// <summary>
/// Result of a modpack server files download operation.
/// </summary>
public record ModpackDownloadResult
{
    /// <summary>
    /// Whether the download and extraction was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Path where the server files were extracted.
    /// </summary>
    public string? ExtractedPath { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Hash of the downloaded file for verification.
    /// </summary>
    public string? FileHash { get; init; }

    /// <summary>
    /// Type of server files that were downloaded.
    /// </summary>
    public ModpackServerFileType? FileType { get; init; }

    /// <summary>
    /// Minecraft version extracted from manifest (if available).
    /// Takes precedence over version from file metadata.
    /// </summary>
    public string? ManifestMinecraftVersion { get; init; }

    /// <summary>
    /// Mod loader type extracted from manifest (if available).
    /// Takes precedence over loader from file metadata.
    /// </summary>
    public ModLoaderType? ManifestLoaderType { get; init; }

    /// <summary>
    /// Mod loader version extracted from manifest (e.g., "21.1.216" from "neoforge-21.1.216").
    /// Takes precedence over loader version from file metadata.
    /// </summary>
    public string? ManifestLoaderVersion { get; init; }

    /// <summary>
    /// Creates a successful download result.
    /// </summary>
    public static ModpackDownloadResult Succeeded(string extractedPath, string? fileHash = null, ModpackServerFileType? fileType = null)
        => new()
        {
            Success = true,
            ExtractedPath = extractedPath,
            FileHash = fileHash,
            FileType = fileType
        };

    /// <summary>
    /// Creates a successful download result with manifest info.
    /// </summary>
    public static ModpackDownloadResult Succeeded(
        string extractedPath,
        string? fileHash,
        ModpackServerFileType? fileType,
        string? manifestMinecraftVersion,
        ModLoaderType? manifestLoaderType,
        string? manifestLoaderVersion)
        => new()
        {
            Success = true,
            ExtractedPath = extractedPath,
            FileHash = fileHash,
            FileType = fileType,
            ManifestMinecraftVersion = manifestMinecraftVersion,
            ManifestLoaderType = manifestLoaderType,
            ManifestLoaderVersion = manifestLoaderVersion
        };

    /// <summary>
    /// Creates a failed download result.
    /// </summary>
    public static ModpackDownloadResult Failed(string error)
        => new()
        {
            Success = false,
            Error = error
        };
}

/// <summary>
/// Progress information during modpack download and extraction.
/// </summary>
public record ModpackDownloadProgress
{
    /// <summary>
    /// Current phase of the download/install process.
    /// </summary>
    public ModpackDownloadPhase Phase { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Overall progress percentage (0-100).
    /// </summary>
    public double Percentage { get; init; }

    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; init; }

    /// <summary>
    /// Total bytes to download.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Current file being processed (for multi-file downloads).
    /// </summary>
    public string? CurrentFile { get; init; }

    /// <summary>
    /// Creates a downloading progress update.
    /// </summary>
    public static ModpackDownloadProgress Downloading(long bytesDownloaded, long totalBytes, string? currentFile = null)
        => new()
        {
            Phase = ModpackDownloadPhase.Downloading,
            Message = "Downloading server files...",
            Percentage = totalBytes > 0 ? (double)bytesDownloaded / totalBytes * 100 : 0,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes,
            CurrentFile = currentFile
        };

    /// <summary>
    /// Creates an extracting progress update.
    /// </summary>
    public static ModpackDownloadProgress Extracting(double percentage, string? currentFile = null)
        => new()
        {
            Phase = ModpackDownloadPhase.Extracting,
            Message = "Extracting files...",
            Percentage = percentage,
            CurrentFile = currentFile
        };

    /// <summary>
    /// Creates a verifying progress update.
    /// </summary>
    public static ModpackDownloadProgress Verifying()
        => new()
        {
            Phase = ModpackDownloadPhase.Verifying,
            Message = "Verifying files...",
            Percentage = 95
        };

    /// <summary>
    /// Creates a completed progress update.
    /// </summary>
    public static ModpackDownloadProgress Completed()
        => new()
        {
            Phase = ModpackDownloadPhase.Completed,
            Message = "Download complete",
            Percentage = 100
        };
}

/// <summary>
/// Phase of the modpack download/install process.
/// </summary>
public enum ModpackDownloadPhase
{
    /// <summary>
    /// Starting the download process.
    /// </summary>
    Starting,

    /// <summary>
    /// Downloading files.
    /// </summary>
    Downloading,

    /// <summary>
    /// Extracting archive contents.
    /// </summary>
    Extracting,

    /// <summary>
    /// Verifying file integrity.
    /// </summary>
    Verifying,

    /// <summary>
    /// Running post-install steps (e.g., Forge installer).
    /// </summary>
    PostInstall,

    /// <summary>
    /// Download and installation completed.
    /// </summary>
    Completed
}

#endregion
