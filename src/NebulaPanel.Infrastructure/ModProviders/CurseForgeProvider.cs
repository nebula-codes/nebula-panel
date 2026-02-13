using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.Configuration;
using NebulaPanel.Infrastructure.ModProviders.CurseForge;

namespace NebulaPanel.Infrastructure.ModProviders;

/// <summary>
/// CurseForge mod provider implementation.
/// </summary>
public sealed class CurseForgeProvider : IModProvider
{
    private readonly CurseForgeApiClient _apiClient;
    private readonly CurseForgeSettings _settings;
    private readonly ILogger<CurseForgeProvider> _logger;

    public CurseForgeProvider(
        CurseForgeApiClient apiClient,
        IOptions<CurseForgeSettings> settings,
        ILogger<CurseForgeProvider> logger)
    {
        _apiClient = apiClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public ModProviderType ProviderType => ModProviderType.CurseForge;

    /// <inheritdoc />
    public string DisplayName => "CurseForge";

    /// <inheritdoc />
    public string? IconUrl => "https://www.curseforge.com/favicon.ico";

    /// <inheritdoc />
    public Task<bool> SupportsGameAsync(string gameSlug, CancellationToken cancellationToken = default)
    {
        var supported = _apiClient.IsConfigured && _settings.IsGameSupported(gameSlug);
        return Task.FromResult(supported);
    }

    /// <inheritdoc />
    public async Task<ModSearchResult> SearchAsync(
        ModSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_apiClient.IsConfigured)
        {
            _logger.LogWarning("CurseForge API key is not configured, skipping search");
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        var gameConfig = _settings.GetGameConfig(query.GameSlug ?? "");

        if (gameConfig is null)
        {
            _logger.LogWarning("Unsupported game slug for CurseForge: {GameSlug}", query.GameSlug);
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        if (!gameConfig.Enabled)
        {
            _logger.LogInformation(
                "CurseForge support for game '{GameSlug}' is disabled in configuration",
                query.GameSlug);
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        if (gameConfig.GameId == 0)
        {
            _logger.LogInformation(
                "Game '{GameSlug}' is not yet available on CurseForge (GameId = 0)",
                query.GameSlug);
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        var gameId = gameConfig.GameId;

        var sortField = query.EarlyAccessOnly == true
            ? CurseForgeSortField.EarlyAccess
            : MapSortToField(query.Sort);
        var sortOrder = query.Sort == ModSearchSort.Name ? "asc" : "desc";
        var index = (query.Page - 1) * query.PageSize;
        var limit = Math.Min(query.PageSize, 50); // CurseForge max is 50

        // Determine class ID for the content type from configuration
        int? classId = null;
        var contentTypeKey = query.ContentType.ToString();
        if (gameConfig.ClassIds.TryGetValue(contentTypeKey, out var configuredClassId))
        {
            classId = configuredClassId;
        }

        // Map mod loader to CurseForge type
        int? modLoaderType = MapModLoader(query.ModLoader);

        _logger.LogInformation(
            "Searching CurseForge: query='{Query}', gameId={GameId}, classId={ClassId}, " +
            "contentType={ContentType}, gameVersion={GameVersion}, modLoader={ModLoader}, " +
            "sortField={SortField}, index={Index}, limit={Limit}",
            query.Query, gameId, classId, query.ContentType, query.GameVersion, modLoaderType, sortField, index, limit);

        var response = await _apiClient.SearchAsync(
            gameId,
            query.Query,
            classId,
            categoryId: null,
            query.GameVersion,
            modLoaderType,
            sortField,
            sortOrder,
            index,
            limit,
            cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            _logger.LogWarning(
                "CurseForge search returned null response - this may indicate an API key issue. " +
                "Check the logs for 403 Forbidden errors.");
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        _logger.LogInformation(
            "CurseForge search returned {ResultCount} results, total={TotalCount}",
            response.Data.Length, response.Pagination.TotalCount);

        var mods = response.Data.Select(mod => new ModSearchItem(
            Id: mod.Id.ToString(),
            Slug: mod.Slug,
            Name: mod.Name,
            Summary: mod.Summary,
            IconUrl: mod.Logo?.Url,
            Author: mod.Authors.FirstOrDefault()?.Name,
            Downloads: mod.DownloadCount,
            UpdatedAt: mod.DateModified,
            Categories: mod.Categories.Select(c => c.Name).ToList(),
            GameVersions: GetGameVersions(mod),
            Provider: ModProviderType.CurseForge,
            IsEarlyAccess: mod.LatestEarlyAccessFilesIndexes?.Length > 0
        )).ToList();

        // If filtering to early access only, remove non-early-access mods
        if (query.EarlyAccessOnly == true)
        {
            mods = mods.Where(m => m.IsEarlyAccess).ToList();
        }

        var totalPages = (int)Math.Ceiling((double)response.Pagination.TotalCount / query.PageSize);

        return new ModSearchResult(
            Mods: mods,
            TotalCount: response.Pagination.TotalCount,
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
        if (!_apiClient.IsConfigured)
        {
            _logger.LogWarning("CurseForge API key is not configured");
            return null;
        }

        if (!int.TryParse(modId, out var id))
        {
            _logger.LogWarning("Invalid CurseForge mod ID: {ModId}", modId);
            return null;
        }

        var mod = await _apiClient.GetModAsync(id, cancellationToken).ConfigureAwait(false);

        if (mod is null)
        {
            return null;
        }

        var authors = mod.Authors
            .Select(a => new ModAuthor(a.Name, a.Url))
            .ToList();

        var screenshots = mod.Screenshots?
            .Select(s => new ModScreenshot(s.Url, s.Title))
            .ToList() ?? [];

        var gameVersions = GetGameVersions(mod);
        var loaders = GetModLoaders(mod);

        return new ModDetails(
            Id: mod.Id.ToString(),
            Slug: mod.Slug,
            Name: mod.Name,
            Summary: mod.Summary,
            Description: null, // CurseForge doesn't return full description in API
            IconUrl: mod.Logo?.Url,
            BannerUrl: mod.Screenshots?.FirstOrDefault()?.Url,
            SourceUrl: mod.Links?.SourceUrl,
            WikiUrl: mod.Links?.WikiUrl,
            DiscordUrl: null, // CurseForge doesn't provide Discord URL
            Authors: authors,
            Downloads: mod.DownloadCount,
            CreatedAt: mod.DateCreated,
            UpdatedAt: mod.DateModified,
            Categories: mod.Categories.Select(c => c.Name).ToList(),
            GameVersions: gameVersions,
            Loaders: loaders,
            Screenshots: screenshots,
            Provider: ModProviderType.CurseForge
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModVersion>> GetVersionsAsync(
        string modId,
        string? gameVersion,
        CancellationToken cancellationToken = default)
    {
        if (!_apiClient.IsConfigured)
        {
            _logger.LogWarning("CurseForge API key is not configured");
            return [];
        }

        if (!int.TryParse(modId, out var id))
        {
            _logger.LogWarning("Invalid CurseForge mod ID: {ModId}", modId);
            return [];
        }

        var files = await _apiClient.GetFilesAsync(
            id,
            gameVersion,
            modLoaderType: null,
            index: 0,
            pageSize: 50,
            cancellationToken).ConfigureAwait(false);

        return files
            .Where(f => f.FileStatus == CurseForgeFileStatus.Approved || f.IsAvailable)
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
            if (!_apiClient.IsConfigured)
            {
                return new ModDownloadResult(false, null, "CurseForge API key is not configured", null);
            }

            if (!int.TryParse(modId, out var modIdInt) || !int.TryParse(versionId, out var fileId))
            {
                return new ModDownloadResult(false, null, "Invalid mod ID or version ID format", null);
            }

            // Get file details
            var file = await _apiClient.GetFileAsync(modIdInt, fileId, cancellationToken).ConfigureAwait(false);

            if (file is null)
            {
                return new ModDownloadResult(false, null, $"File {versionId} not found", null);
            }

            // Get download URL - some mods have restricted downloads
            var downloadUrl = file.DownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = await _apiClient.GetDownloadUrlAsync(modIdInt, fileId, cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                return new ModDownloadResult(false, null, "Download URL not available - mod author may have disabled third-party downloads", null);
            }

            // Ensure destination directory exists
            Directory.CreateDirectory(destinationPath);

            var filePath = Path.Combine(destinationPath, file.FileName);

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
            .Where(m => m.Provider == ModProviderType.CurseForge)
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
                    // Get mod details for the mod name
                    var modDetails = await _apiClient.GetModAsync(
                        int.Parse(mod.ModId),
                        cancellationToken).ConfigureAwait(false);

                    // Find the installed version to get its version string
                    var installedVersion = versions.FirstOrDefault(v => v.Id == mod.VersionId);
                    var currentVersionString = installedVersion?.Version ?? mod.VersionId;

                    updates.Add(new ModUpdateInfo(
                        InstalledModId: Guid.Empty, // Will be set by the service layer
                        ModName: modDetails?.Name ?? mod.ModId,
                        CurrentVersion: currentVersionString,
                        LatestVersion: latestVersion.Version,
                        LatestVersionId: latestVersion.Id,
                        ReleasedAt: latestVersion.ReleasedAt,
                        Changelog: latestVersion.Changelog,
                        Provider: ModProviderType.CurseForge
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

    private static int MapSortToField(ModSearchSort sort) => sort switch
    {
        ModSearchSort.Relevance => CurseForgeSortField.Featured,
        ModSearchSort.Downloads => CurseForgeSortField.Popularity,
        ModSearchSort.Updated => CurseForgeSortField.LastUpdated,
        ModSearchSort.Created => CurseForgeSortField.ReleasedDate,
        ModSearchSort.Name => CurseForgeSortField.Name,
        _ => CurseForgeSortField.Featured
    };

    private static int? MapModLoader(string? modLoader)
    {
        if (string.IsNullOrWhiteSpace(modLoader))
        {
            return null;
        }

        return modLoader.ToLowerInvariant() switch
        {
            "forge" => CurseForgeModLoaderType.Forge,
            "fabric" => CurseForgeModLoaderType.Fabric,
            "quilt" => CurseForgeModLoaderType.Quilt,
            "neoforge" => CurseForgeModLoaderType.NeoForge,
            "liteloader" => CurseForgeModLoaderType.LiteLoader,
            "cauldron" => CurseForgeModLoaderType.Cauldron,
            _ => null
        };
    }

    private static List<string> GetGameVersions(CurseForgeMod mod)
    {
        // Extract unique game versions from latest files
        return mod.LatestFiles
            .SelectMany(f => f.GameVersions)
            .Where(v => !IsModLoaderVersion(v))
            .Distinct()
            .OrderByDescending(v => v)
            .ToList();
    }

    private static List<string> GetModLoaders(CurseForgeMod mod)
    {
        // Extract mod loaders from latest files
        return mod.LatestFiles
            .SelectMany(f => f.GameVersions)
            .Where(IsModLoaderVersion)
            .Distinct()
            .ToList();
    }

    private static bool IsModLoaderVersion(string version)
    {
        var loaders = new[] { "Forge", "Fabric", "Quilt", "NeoForge", "LiteLoader", "Cauldron" };
        return loaders.Any(l => version.Equals(l, StringComparison.OrdinalIgnoreCase));
    }

    private static ModVersion MapVersion(CurseForgeFile file)
    {
        var gameVersions = file.GameVersions
            .Where(v => !IsModLoaderVersion(v))
            .ToList();

        var loaders = file.GameVersions
            .Where(IsModLoaderVersion)
            .ToList();

        var dependencies = file.Dependencies?
            .Select(MapDependency)
            .ToList() ?? [];

        return new ModVersion(
            Id: file.Id.ToString(),
            Version: file.DisplayName,
            Name: file.FileName,
            Changelog: null, // CurseForge doesn't include changelog in file response
            GameVersions: gameVersions,
            Loaders: loaders,
            ReleasedAt: file.FileDate,
            Downloads: file.DownloadCount,
            VersionType: MapReleaseType(file.ReleaseType),
            Dependencies: dependencies,
            Files: new List<ModFile>
            {
                new ModFile(
                    FileName: file.FileName,
                    Url: file.DownloadUrl ?? "",
                    SizeBytes: file.FileLength,
                    Sha512: null, // CurseForge doesn't provide SHA512
                    Sha1: GetHashValue(file.Hashes, CurseForgeHashAlgo.Sha1),
                    IsPrimary: true
                )
            }
        );
    }

    private static ModVersionType MapReleaseType(int releaseType) => releaseType switch
    {
        CurseForgeReleaseType.Release => ModVersionType.Release,
        CurseForgeReleaseType.Beta => ModVersionType.Beta,
        CurseForgeReleaseType.Alpha => ModVersionType.Alpha,
        _ => ModVersionType.Release
    };

    private static ModDependency MapDependency(CurseForgeDependency dep)
    {
        return new ModDependency(
            ModId: dep.ModId.ToString(),
            ModName: null, // Would need additional API call
            VersionId: null, // CurseForge dependencies don't specify version
            Type: MapDependencyType(dep.RelationType)
        );
    }

    private static ModDependencyType MapDependencyType(int relationType) => relationType switch
    {
        CurseForgeDependencyType.RequiredDependency => ModDependencyType.Required,
        CurseForgeDependencyType.OptionalDependency => ModDependencyType.Optional,
        CurseForgeDependencyType.Incompatible => ModDependencyType.Incompatible,
        CurseForgeDependencyType.EmbeddedLibrary => ModDependencyType.Embedded,
        _ => ModDependencyType.Optional
    };

    private static string? GetHashValue(CurseForgeHash[] hashes, int algo)
    {
        return hashes.FirstOrDefault(h => h.Algo == algo)?.Value;
    }

    private static async Task<(bool Valid, string? Hash)> VerifyChecksumAsync(
        string filePath,
        CurseForgeHash[] hashes,
        CancellationToken cancellationToken)
    {
        // CurseForge provides MD5 (algo=2) and SHA1 (algo=1)
        // Prefer SHA1 over MD5
        var sha1Hash = GetHashValue(hashes, CurseForgeHashAlgo.Sha1);
        if (!string.IsNullOrWhiteSpace(sha1Hash))
        {
            var computedHash = await ComputeHashAsync<SHA1>(filePath, cancellationToken).ConfigureAwait(false);
            var expectedHash = sha1Hash.ToLowerInvariant();
            return (computedHash == expectedHash, computedHash);
        }

        var md5Hash = GetHashValue(hashes, CurseForgeHashAlgo.Md5);
        if (!string.IsNullOrWhiteSpace(md5Hash))
        {
            var computedHash = await ComputeHashAsync<MD5>(filePath, cancellationToken).ConfigureAwait(false);
            var expectedHash = md5Hash.ToLowerInvariant();
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
            nameof(SHA1) => (HashAlgorithm)SHA1.Create(),
            nameof(MD5) => MD5.Create(),
            _ => throw new ArgumentException($"Unsupported hash algorithm: {typeof(T).Name}")
        };

        var hashBytes = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
