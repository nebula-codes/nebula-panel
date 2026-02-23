using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.ModProviders.Modrinth;

namespace NebulaPanel.Infrastructure.ModProviders;

/// <summary>
/// Modrinth mod provider implementation for Minecraft mods.
/// </summary>
public sealed class ModrinthProvider : IModProvider
{
    private readonly ModrinthApiClient _apiClient;
    private readonly ILogger<ModrinthProvider> _logger;

    public ModrinthProvider(ModrinthApiClient apiClient, ILogger<ModrinthProvider> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public ModProviderType ProviderType => ModProviderType.Modrinth;

    /// <inheritdoc />
    public string DisplayName => "Modrinth";

    /// <inheritdoc />
    public string? IconUrl => "https://modrinth.com/favicon.ico";

    /// <inheritdoc />
    public Task<bool> SupportsGameAsync(string gameSlug, CancellationToken cancellationToken = default)
    {
        // Modrinth currently only supports Minecraft
        var supported = gameSlug.Equals("minecraft", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(supported);
    }

    /// <inheritdoc />
    public async Task<ModSearchResult> SearchAsync(
        ModSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var facets = BuildFacets(query);
        var index = MapSortToIndex(query.Sort);
        var offset = (query.Page - 1) * query.PageSize;
        var limit = Math.Min(query.PageSize, 100); // Modrinth max is 100

        // Log the actual facets being sent
        var facetStrings = facets.Select(f => $"[{string.Join(", ", f)}]");
        _logger.LogInformation(
            "Searching Modrinth: query='{Query}', facets=[{Facets}], index={Index}, offset={Offset}, limit={Limit}",
            query.Query, string.Join(", ", facetStrings), index, offset, limit);

        var response = await _apiClient.SearchAsync(
            query.Query,
            facets,
            index,
            offset,
            limit,
            cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            _logger.LogWarning("Modrinth search returned null response");
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        _logger.LogInformation(
            "Modrinth search returned {HitCount} hits, total={TotalHits}",
            response.Hits.Length, response.TotalHits);

        var mods = response.Hits.Select(hit => new ModSearchItem(
            Id: hit.ProjectId,
            Slug: hit.Slug,
            Name: hit.Title,
            Summary: hit.Description,
            IconUrl: hit.IconUrl,
            Author: hit.Author,
            Downloads: hit.Downloads,
            UpdatedAt: TryParseDateTime(hit.DateModified),
            Categories: hit.Categories?.ToList() ?? [],
            GameVersions: hit.Versions?.ToList() ?? [],
            Provider: ModProviderType.Modrinth
        )).ToList();

        var totalPages = (int)Math.Ceiling((double)response.TotalHits / query.PageSize);

        return new ModSearchResult(
            Mods: mods,
            TotalCount: response.TotalHits,
            Page: query.Page,
            PageSize: query.PageSize,
            TotalPages: totalPages
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

        // Fetch team members to get author information
        var teamMembers = await _apiClient.GetProjectTeamAsync(project.Id, cancellationToken).ConfigureAwait(false);

        var authors = teamMembers
            .Where(m => m.Accepted)
            .OrderBy(m => m.Ordering)
            .Select(m => new ModAuthor(
                Name: m.User.Name ?? m.User.Username,
                Url: $"https://modrinth.com/user/{m.User.Username}"
            ))
            .ToList();

        // If no team members found, use project info
        if (authors.Count == 0)
        {
            authors.Add(new ModAuthor("Unknown", null));
        }

        var screenshots = project.Gallery?
            .OrderBy(g => g.Ordering)
            .Select(g => new ModScreenshot(g.Url, g.Title))
            .ToList() ?? [];

        return new ModDetails(
            Id: project.Id,
            Slug: project.Slug,
            Name: project.Title,
            Summary: project.Description,
            Description: project.Body,
            IconUrl: project.IconUrl,
            BannerUrl: project.Gallery?.FirstOrDefault(g => g.Featured)?.Url,
            PageUrl: $"https://modrinth.com/mod/{project.Slug}",
            SourceUrl: project.SourceUrl,
            WikiUrl: project.WikiUrl,
            DiscordUrl: project.DiscordUrl,
            Authors: authors,
            Downloads: project.Downloads,
            CreatedAt: TryParseDateTime(project.Published) ?? DateTime.UtcNow,
            UpdatedAt: TryParseDateTime(project.Updated) ?? DateTime.UtcNow,
            Categories: project.Categories.ToList(),
            GameVersions: project.GameVersions.ToList(),
            Loaders: project.Loaders.ToList(),
            Screenshots: screenshots,
            Dependencies: [],
            Provider: ModProviderType.Modrinth
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModVersion>> GetVersionsAsync(
        string modId,
        string? gameVersion,
        CancellationToken cancellationToken = default)
    {
        var gameVersions = gameVersion is not null ? new[] { gameVersion } : null;

        var versions = await _apiClient.GetProjectVersionsAsync(
            modId,
            loaders: null,
            gameVersions: gameVersions,
            cancellationToken).ConfigureAwait(false);

        return versions
            .Select(MapVersion)
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
            // Get version details to find the download URL
            var version = await _apiClient.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);

            if (version is null)
            {
                return new ModDownloadResult(false, null, $"Version {versionId} not found", null);
            }

            // Find the primary file, or fall back to the first file
            var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();

            if (file is null)
            {
                return new ModDownloadResult(false, null, "No downloadable files found for this version", null);
            }

            // Ensure destination directory exists
            Directory.CreateDirectory(destinationPath);

            var filePath = Path.Combine(destinationPath, file.Filename);

            // Download the file
            var (success, error) = await _apiClient.DownloadFileAsync(
                file.Url,
                filePath,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                return new ModDownloadResult(false, null, error, null);
            }

            // Verify checksum
            var (checksumValid, computedHash) = await VerifyChecksumAsync(filePath, file.Hashes, cancellationToken).ConfigureAwait(false);

            if (!checksumValid)
            {
                // Delete the corrupted file
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // Ignore deletion errors
                }

                return new ModDownloadResult(false, null, "Checksum verification failed - file may be corrupted", null);
            }

            _logger.LogInformation(
                "Successfully downloaded mod {ModId} version {VersionId} to {FilePath}",
                modId, versionId, filePath);

            return new ModDownloadResult(true, filePath, null, computedHash);
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
            .Where(m => m.Provider == ModProviderType.Modrinth)
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
                        Provider: ModProviderType.Modrinth
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

    private static List<string[]> BuildFacets(ModSearchQuery query)
    {
        // Map content type to Modrinth project type
        var projectType = query.ContentType switch
        {
            ModContentType.Mod => "mod",
            ModContentType.Modpack => "modpack",
            ModContentType.ResourcePack => "resourcepack",
            ModContentType.Shader => "shader",
            ModContentType.World => "world",  // Not supported by Modrinth, but include for completeness
            _ => "mod"
        };

        var facets = new List<string[]>
        {
            new[] { $"project_type:{projectType}" }
        };

        if (!string.IsNullOrWhiteSpace(query.GameVersion))
        {
            facets.Add(new[] { $"versions:{query.GameVersion}" });
        }

        if (!string.IsNullOrWhiteSpace(query.ModLoader))
        {
            facets.Add(new[] { $"categories:{query.ModLoader.ToLowerInvariant()}" });
        }

        if (query.Categories is { Count: > 0 })
        {
            // Categories are OR'd together within the same array
            var categoryFacets = query.Categories
                .Select(c => $"categories:{c.ToLowerInvariant()}")
                .ToArray();
            facets.Add(categoryFacets);
        }

        return facets;
    }

    private static string MapSortToIndex(ModSearchSort sort) => sort switch
    {
        ModSearchSort.Relevance => "relevance",
        ModSearchSort.Downloads => "downloads",
        ModSearchSort.Updated => "updated",
        ModSearchSort.Created => "newest",
        ModSearchSort.Name => "relevance", // Modrinth doesn't support name sorting
        _ => "relevance"
    };

    private static ModVersion MapVersion(ModrinthVersion v)
    {
        return new ModVersion(
            Id: v.Id,
            Version: v.VersionNumber,
            Name: v.Name,
            Changelog: v.Changelog,
            GameVersions: v.GameVersions.ToList(),
            Loaders: v.Loaders.ToList(),
            ReleasedAt: TryParseDateTime(v.DatePublished) ?? DateTime.UtcNow,
            Downloads: v.Downloads,
            VersionType: MapVersionType(v.VersionType),
            Dependencies: v.Dependencies.Select(MapDependency).ToList(),
            Files: v.Files.Select(MapFile).ToList()
        );
    }

    private static ModVersionType MapVersionType(string versionType) => versionType.ToLowerInvariant() switch
    {
        "release" => ModVersionType.Release,
        "beta" => ModVersionType.Beta,
        "alpha" => ModVersionType.Alpha,
        _ => ModVersionType.Release
    };

    private static ModDependency MapDependency(ModrinthDependency d)
    {
        return new ModDependency(
            ModId: d.ProjectId ?? string.Empty,
            ModName: null, // Would need additional API call to get mod name
            VersionId: d.VersionId,
            Type: MapDependencyType(d.DependencyType)
        );
    }

    private static ModDependencyType MapDependencyType(string dependencyType) => dependencyType.ToLowerInvariant() switch
    {
        "required" => ModDependencyType.Required,
        "optional" => ModDependencyType.Optional,
        "incompatible" => ModDependencyType.Incompatible,
        "embedded" => ModDependencyType.Embedded,
        _ => ModDependencyType.Optional
    };

    private static ModFile MapFile(ModrinthFile f)
    {
        return new ModFile(
            FileName: f.Filename,
            Url: f.Url,
            SizeBytes: f.Size,
            Sha512: f.Hashes.Sha512,
            Sha1: f.Hashes.Sha1,
            IsPrimary: f.Primary
        );
    }

    private static DateTime? TryParseDateTime(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            return null;
        }

        if (DateTime.TryParse(dateString, out var result))
        {
            return result.ToUniversalTime();
        }

        return null;
    }

    private static async Task<(bool Valid, string? Hash)> VerifyChecksumAsync(
        string filePath,
        ModrinthHashes hashes,
        CancellationToken cancellationToken)
    {
        // Prefer SHA512 over SHA1
        if (!string.IsNullOrWhiteSpace(hashes.Sha512))
        {
            var computedHash = await ComputeHashAsync<SHA512>(filePath, cancellationToken).ConfigureAwait(false);
            var expectedHash = hashes.Sha512.ToLowerInvariant();
            return (computedHash == expectedHash, computedHash);
        }

        if (!string.IsNullOrWhiteSpace(hashes.Sha1))
        {
            var computedHash = await ComputeHashAsync<SHA1>(filePath, cancellationToken).ConfigureAwait(false);
            var expectedHash = hashes.Sha1.ToLowerInvariant();
            return (computedHash == expectedHash, computedHash);
        }

        // No checksum available, assume valid
        return (true, null);
    }

    private static async Task<string> ComputeHashAsync<T>(string filePath, CancellationToken cancellationToken)
        where T : HashAlgorithm
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        using var hashAlgorithm = typeof(T).Name switch
        {
            nameof(SHA512) => (HashAlgorithm)SHA512.Create(),
            nameof(SHA1) => SHA1.Create(),
            _ => throw new ArgumentException($"Unsupported hash algorithm: {typeof(T).Name}")
        };

        var hashBytes = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
