using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.ModProviders.Modtale;

namespace NebulaPanel.Infrastructure.ModProviders;

/// <summary>
/// Modtale mod provider implementation for Hytale mods.
/// </summary>
public sealed class ModtaleProvider : IModProvider
{
    private readonly ModtaleApiClient _apiClient;
    private readonly ILogger<ModtaleProvider> _logger;

    public ModtaleProvider(ModtaleApiClient apiClient, ILogger<ModtaleProvider> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public ModProviderType ProviderType => ModProviderType.Modtale;

    /// <inheritdoc />
    public string DisplayName => "Modtale";

    /// <inheritdoc />
    public string? IconUrl => "/images/providers/modtale.svg";

    /// <inheritdoc />
    public Task<bool> SupportsGameAsync(string gameSlug, CancellationToken cancellationToken = default)
    {
        // Modtale only supports Hytale
        var supported = gameSlug.Equals("hytale", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(supported);
    }

    /// <inheritdoc />
    public async Task<ModSearchResult> SearchAsync(
        ModSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        // Map content type to Modtale classification
        var classification = MapContentTypeToClassification(query.ContentType);
        var sort = MapSortToModtale(query.Sort);
        var page = query.Page - 1; // Modtale uses 0-based pagination
        var pageSize = Math.Min(query.PageSize, 100);

        // Map categories to tags (comma-separated)
        var tags = query.Categories is { Count: > 0 }
            ? string.Join(",", query.Categories)
            : null;

        _logger.LogInformation(
            "Searching Modtale: query='{Query}', classification={Classification}, " +
            "gameVersion={GameVersion}, sort={Sort}, page={Page}, pageSize={PageSize}",
            query.Query, classification, query.GameVersion, sort, page, pageSize);

        var response = await _apiClient.SearchAsync(
            query.Query,
            classification,
            query.GameVersion,
            tags,
            sort,
            page,
            pageSize,
            cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            _logger.LogWarning("Modtale search returned null response");
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        _logger.LogInformation(
            "Modtale search returned {ResultCount} results, total={TotalCount}",
            response.Content.Length, response.TotalElements);

        var mods = response.Content.Select(project => new ModSearchItem(
            Id: project.Id,
            Slug: project.Id, // Modtale uses ID as slug
            Name: project.Title,
            Summary: project.Description,
            IconUrl: project.ImageUrl,
            Author: project.Author?.Username ?? "Unknown",
            Downloads: project.Downloads,
            UpdatedAt: project.UpdatedAt,
            Categories: BuildCategories(project.Classification, project.Tags),
            GameVersions: [], // Search results don't include game versions
            Provider: ModProviderType.Modtale
        )).ToList();

        return new ModSearchResult(
            Mods: mods,
            TotalCount: response.TotalElements,
            Page: query.Page,
            PageSize: query.PageSize,
            TotalPages: response.TotalPages
        );
    }

    /// <inheritdoc />
    public async Task<ModDetails?> GetDetailsAsync(
        string modId,
        CancellationToken cancellationToken = default)
    {
        var project = await _apiClient.GetProjectAsync(modId, cancellationToken).ConfigureAwait(false);

        if (project is null)
        {
            return null;
        }

        var authorName = project.Author?.Username ?? "Unknown";
        var authors = new List<ModAuthor>
        {
            new(
                Name: authorName,
                Url: project.Author?.Username is not null ? $"https://modtale.net/user/{project.Author.Username}" : null
            )
        };

        var screenshots = project.GalleryImages?
            .Select(img => new ModScreenshot(img.Url, img.Title))
            .ToList() ?? [];

        // Extract game versions from all versions
        var gameVersions = project.Versions
            .SelectMany(v => v.GameVersions)
            .Distinct()
            .OrderByDescending(v => v)
            .ToList();

        return new ModDetails(
            Id: project.Id,
            Slug: project.Id,
            Name: project.Title,
            Summary: project.Description,
            Description: project.About,
            IconUrl: project.ImageUrl ?? project.GalleryImages?.FirstOrDefault()?.Url,
            BannerUrl: project.GalleryImages?.FirstOrDefault(g => g.Featured)?.Url,
            PageUrl: null,
            SourceUrl: project.RepositoryUrl,
            WikiUrl: null,
            DiscordUrl: null,
            Authors: authors,
            Downloads: project.Downloads,
            CreatedAt: project.CreatedAt,
            UpdatedAt: project.UpdatedAt,
            Categories: BuildCategories(project.Classification, project.Tags),
            GameVersions: gameVersions,
            Loaders: [], // Hytale doesn't have mod loaders like Minecraft
            Screenshots: screenshots,
            Dependencies: [],
            Provider: ModProviderType.Modtale
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModVersion>> GetVersionsAsync(
        string modId,
        string? gameVersion,
        CancellationToken cancellationToken = default)
    {
        var project = await _apiClient.GetProjectAsync(modId, cancellationToken).ConfigureAwait(false);

        if (project is null)
        {
            return [];
        }

        var versions = project.Versions.AsEnumerable();

        // Filter by game version if specified
        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            versions = versions.Where(v =>
                v.GameVersions.Any(gv => gv.Equals(gameVersion, StringComparison.OrdinalIgnoreCase)));
        }

        return versions
            .OrderByDescending(v => v.ReleasedAt)
            .Select(v => MapVersion(modId, v))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ModDownloadResult> DownloadAsync(
        string modId,
        string versionId,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get project details to find the version info
            var project = await _apiClient.GetProjectAsync(modId, cancellationToken).ConfigureAwait(false);

            if (project is null)
            {
                return new ModDownloadResult(false, null, $"Project {modId} not found", null);
            }

            var version = project.Versions.FirstOrDefault(v => v.Id == versionId);

            if (version is null)
            {
                return new ModDownloadResult(false, null, $"Version {versionId} not found", null);
            }

            // Ensure destination directory exists
            Directory.CreateDirectory(destinationPath);

            var fileName = version.FileName ?? $"{project.Title}-{version.VersionNumber}.jar";
            var filePath = Path.Combine(destinationPath, SanitizeFileName(fileName));

            // Construct download URL using version number (FileUrl is just a storage path, not a full URL)
            var downloadUrl = $"https://api.modtale.net/api/v1/projects/{modId}/versions/{Uri.EscapeDataString(version.VersionNumber)}/download";

            // Download the file
            var (success, error) = await _apiClient.DownloadFileAsync(
                downloadUrl,
                filePath,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                return new ModDownloadResult(false, null, error, null);
            }

            // Verify checksum if available
            if (!string.IsNullOrWhiteSpace(version.Sha256))
            {
                var computedHash = await ComputeSha256Async(filePath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(computedHash, version.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "SHA256 mismatch for {ModId}/{VersionId}. Expected: {Expected}, Got: {Actual}",
                        modId, versionId, version.Sha256, computedHash);

                    // Delete corrupted file
                    try { File.Delete(filePath); } catch { /* ignore */ }

                    return new ModDownloadResult(false, null, "Checksum verification failed - file may be corrupted", null);
                }

                _logger.LogInformation(
                    "Successfully downloaded mod {ModId} version {VersionId} to {FilePath}",
                    modId, versionId, filePath);

                return new ModDownloadResult(true, filePath, null, computedHash);
            }

            _logger.LogInformation(
                "Successfully downloaded mod {ModId} version {VersionId} to {FilePath} (no checksum available)",
                modId, versionId, filePath);

            return new ModDownloadResult(true, filePath, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download mod {ModId} version {VersionId}", modId, versionId);
            return new ModDownloadResult(false, null, $"Download failed: {ex.Message}", null);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModUpdateInfo>> CheckUpdatesAsync(
        IEnumerable<InstalledModInfo> installedMods,
        string? gameVersion,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<ModUpdateInfo>();
        var modsToCheck = installedMods
            .Where(m => m.Provider == ModProviderType.Modtale)
            .ToList();

        foreach (var mod in modsToCheck)
        {
            try
            {
                var versions = await GetVersionsAsync(mod.ModId, gameVersion, cancellationToken).ConfigureAwait(false);

                if (versions.Count == 0)
                {
                    continue;
                }

                // Get the latest version (first one since they're sorted by date)
                var latestVersion = versions[0];

                // If the installed version is different from the latest, there's an update
                if (!mod.VersionId.Equals(latestVersion.Id, StringComparison.OrdinalIgnoreCase))
                {
                    // Get project details for the mod name
                    var project = await _apiClient.GetProjectAsync(mod.ModId, cancellationToken).ConfigureAwait(false);

                    // Find the installed version to get its version string
                    var installedVersion = versions.FirstOrDefault(v => v.Id == mod.VersionId);
                    var currentVersionString = installedVersion?.Version ?? mod.VersionId;

                    updates.Add(new ModUpdateInfo(
                        InstalledModId: Guid.Empty, // Will be set by the service layer
                        ModName: project?.Title ?? mod.ModId,
                        CurrentVersion: currentVersionString,
                        LatestVersion: latestVersion.Version,
                        LatestVersionId: latestVersion.Id,
                        ReleasedAt: latestVersion.ReleasedAt,
                        Changelog: latestVersion.Changelog,
                        Provider: ModProviderType.Modtale
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to check updates for mod {ModId}",
                    mod.ModId);
            }
        }

        return updates;
    }

    #region Private Helper Methods

    private static string? MapContentTypeToClassification(ModContentType contentType) => contentType switch
    {
        ModContentType.Mod => ModtaleClassification.Plugin,
        ModContentType.Modpack => ModtaleClassification.Modpack,
        ModContentType.ResourcePack => ModtaleClassification.Art,
        ModContentType.World => ModtaleClassification.Save,
        _ => null // No filter - return all types
    };

    private static string MapSortToModtale(ModSearchSort sort) => sort switch
    {
        ModSearchSort.Relevance => "relevance",
        ModSearchSort.Downloads => "downloads",
        ModSearchSort.Updated => "updated",
        ModSearchSort.Created => "newest",
        ModSearchSort.Name => "relevance", // Modtale doesn't support name sorting
        _ => "relevance"
    };

    private static List<string> BuildCategories(string classification, string[]? tags)
    {
        var categories = new List<string>();

        // Add classification as a category
        if (!string.IsNullOrWhiteSpace(classification))
        {
            categories.Add(classification);
        }

        // Add tags as categories
        if (tags is { Length: > 0 })
        {
            categories.AddRange(tags);
        }

        return categories;
    }

    private static ModVersion MapVersion(string modId, ModtaleVersion v)
    {
        // Construct download URL using version number (FileUrl is just a storage path, not a full URL)
        var downloadUrl = $"https://api.modtale.net/api/v1/projects/{modId}/versions/{Uri.EscapeDataString(v.VersionNumber)}/download";

        return new ModVersion(
            Id: v.Id,
            Version: v.VersionNumber,
            Name: v.VersionNumber,
            Changelog: v.Changelog,
            GameVersions: v.GameVersions.ToList(),
            Loaders: [], // Hytale doesn't have mod loaders
            ReleasedAt: v.ReleasedAt,
            Downloads: v.DownloadCount,
            VersionType: MapChannelToVersionType(v.Channel),
            Dependencies: [], // Modtale API doesn't expose dependencies in version info
            Files: new List<ModFile>
            {
                new(
                    FileName: v.FileName ?? $"mod-{v.VersionNumber}.jar",
                    Url: downloadUrl,
                    SizeBytes: v.FileSize ?? 0,
                    Sha512: null,
                    Sha1: null,
                    IsPrimary: true
                )
            }
        );
    }

    private static ModVersionType MapChannelToVersionType(string channel) => channel.ToUpperInvariant() switch
    {
        "RELEASE" => ModVersionType.Release,
        "BETA" => ModVersionType.Beta,
        "ALPHA" => ModVersionType.Alpha,
        _ => ModVersionType.Release
    };

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(fileName.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    #endregion
}
