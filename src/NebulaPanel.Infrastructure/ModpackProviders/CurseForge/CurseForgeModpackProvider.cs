using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.Configuration;
using NebulaPanel.Infrastructure.ModProviders.CurseForge;

namespace NebulaPanel.Infrastructure.ModpackProviders.CurseForge;

/// <summary>
/// Modpack provider implementation for CurseForge - the largest Minecraft modpack repository.
/// </summary>
public sealed partial class CurseForgeModpackProvider : IModpackProvider
{
    private const int MinecraftGameId = 432;
    private const int ModpackClassId = 4471;
    private const int MaxRetries = 3;
    private const int BufferSize = 81920; // 80KB buffer for downloads
    private const int MaxConcurrentDownloads = 4; // Max parallel mod downloads
    private const int BatchSize = 50; // CurseForge API batch limit

    // Categories that indicate mod loaders
    private static readonly Dictionary<int, ModLoaderType> LoaderCategoryIds = new()
    {
        { 4780, ModLoaderType.Fabric }, // Fabric category
        { 4559, ModLoaderType.Fabric }, // Fabric category (alternate)
        { 6715, ModLoaderType.Quilt },  // Quilt category
        { 6756, ModLoaderType.NeoForge } // NeoForge category
    };

    // Patterns for identifying client-only mods
    private static readonly string[] ClientOnlyPatterns =
    [
        "optifine", "optifabric", "iris", "oculus", "sodium", "rubidium",
        "shader", "shaders", "dynamic-fps", "dynamicfps", "fps-reducer",
        "entityculling", "entity-culling", "cullleaves", "cull-leaves",
        "journeymap", "xaeros", "xaero", "voxelmap", "replaymod", "replay-mod",
        "better-fps", "betterfps", "smoothboot", "smooth-boot",
        "reese-sodium", "sodium-extra", "indium",
        "continuity", "lambdynamiclights", "lambda-dynamic-lights",
        "colormatic", "fabulously-optimized",
        "ears", "cit-resewn", "citresewn", "custom-entity-models",
        "emotecraft", "not-enough-animations", "notenoughanimations",
        "first-person-model", "firstpersonmodel"
    ];

    private readonly HttpClient _httpClient;
    private readonly CurseForgeSettings _settings;
    private readonly CurseForgeApiClient _apiClient;
    private readonly ILogger<CurseForgeModpackProvider> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private readonly SemaphoreSlim _downloadSemaphore = new(MaxConcurrentDownloads, MaxConcurrentDownloads);

    private int _rateLimitRemaining = 300;
    private DateTime _rateLimitResetTime = DateTime.UtcNow;

    public CurseForgeModpackProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<CurseForgeSettings> settings,
        CurseForgeApiClient apiClient,
        ILogger<CurseForgeModpackProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("CurseForge");
        _settings = settings.Value;
        _apiClient = apiClient;
        _logger = logger;

        // Set base address from settings
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
    }

    /// <inheritdoc />
    public ModpackProviderType ProviderType => ModpackProviderType.CurseForge;

    /// <inheritdoc />
    public string DisplayName => "CurseForge";

    /// <inheritdoc />
    public string? IconUrl => "https://www.curseforge.com/favicon.ico";

    /// <inheritdoc />
    public async Task<ModpackSearchResult> SearchAsync(ModpackSearchQuery query, CancellationToken ct = default)
    {
        var queryParams = new List<string>
        {
            $"gameId={MinecraftGameId}",
            $"classId={ModpackClassId}",
            $"pageSize={query.PageSize}",
            $"index={(query.Page - 1) * query.PageSize}",
            $"sortField={MapSortField(query.Sort)}",
            "sortOrder=desc"
        };

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            queryParams.Add($"searchFilter={Uri.EscapeDataString(query.Query)}");
        }

        if (!string.IsNullOrWhiteSpace(query.MinecraftVersion))
        {
            queryParams.Add($"gameVersion={Uri.EscapeDataString(query.MinecraftVersion)}");
        }

        if (query.LoaderType.HasValue && query.LoaderType.Value != ModLoaderType.None)
        {
            var loaderTypeId = MapModLoaderType(query.LoaderType.Value);
            if (loaderTypeId.HasValue)
            {
                queryParams.Add($"modLoaderType={loaderTypeId.Value}");
            }
        }

        var url = $"mods/search?{string.Join("&", queryParams)}";
        var response = await GetWithRetryAsync<CurseForgeSearchResponse>(url, ct).ConfigureAwait(false);

        if (response == null)
        {
            return new ModpackSearchResult
            {
                Modpacks = [],
                TotalCount = 0,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalPages = 0
            };
        }

        var modpacks = response.Data.Select(MapToSearchItem).ToList();

        return new ModpackSearchResult
        {
            Modpacks = modpacks,
            TotalCount = response.Pagination.TotalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)response.Pagination.TotalCount / query.PageSize)
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModpackSearchItem>> GetFeaturedAsync(int count = 20, CancellationToken ct = default)
    {
        // CurseForge doesn't have a dedicated featured endpoint, so we use popularity sort
        var queryParams = new List<string>
        {
            $"gameId={MinecraftGameId}",
            $"classId={ModpackClassId}",
            $"pageSize={count}",
            "index=0",
            $"sortField={CurseForgeSortField.Popularity}",
            "sortOrder=desc"
        };

        var url = $"mods/search?{string.Join("&", queryParams)}";
        var response = await GetWithRetryAsync<CurseForgeSearchResponse>(url, ct).ConfigureAwait(false);

        if (response == null)
        {
            return [];
        }

        return response.Data.Select(MapToSearchItem).ToList();
    }

    /// <inheritdoc />
    public async Task<ModpackDetails> GetDetailsAsync(string modpackId, CancellationToken ct = default)
    {
        var url = $"mods/{modpackId}";
        var modResponse = await GetWithRetryAsync<CurseForgeModResponse>(url, ct).ConfigureAwait(false);

        if (modResponse == null)
        {
            throw new InvalidOperationException($"Modpack with ID {modpackId} not found");
        }

        var mod = modResponse.Data;

        // Fetch the description (separate endpoint)
        var description = await GetDescriptionAsync(modpackId, ct).ConfigureAwait(false);

        // Determine if a server pack exists for any version
        var hasServerPack = mod.LatestFiles.Any(f =>
            f.ServerPackFileId.HasValue || IsServerPackFileName(f.FileName));

        // Get mod count from the latest file's modules (if available)
        var modCount = GetModCountFromLatestFile(mod);

        // Extract Minecraft versions from latest files
        var minecraftVersions = ExtractMinecraftVersions(mod.LatestFiles);

        // Detect loaders from categories and latest files
        var loaders = DetectLoaders(mod);

        // Build external links
        var links = BuildLinks(mod);

        // Build screenshots list
        var screenshots = mod.Screenshots?
            .Select(s => new ModpackScreenshot { Url = s.Url, Title = s.Title })
            .ToList() ?? [];

        return new ModpackDetails
        {
            Id = mod.Id.ToString(),
            Slug = mod.Slug,
            Name = mod.Name,
            Summary = mod.Summary,
            Description = description,
            IconUrl = mod.Logo?.Url,
            BannerUrl = mod.Screenshots?.FirstOrDefault()?.Url,
            Authors = mod.Authors.Select(a => new ModpackAuthor { Name = a.Name, Url = a.Url }).ToList(),
            Downloads = mod.DownloadCount,
            CreatedAt = mod.DateCreated,
            UpdatedAt = mod.DateModified,
            Categories = mod.Categories.Select(c => c.Name).ToList(),
            MinecraftVersions = minecraftVersions,
            Loaders = loaders,
            Screenshots = screenshots,
            Links = links,
            Provider = ModpackProviderType.CurseForge,
            ModCount = modCount,
            HasServerPack = hasServerPack
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModpackVersion>> GetVersionsAsync(string modpackId, CancellationToken ct = default)
    {
        var allFiles = new List<CurseForgeFile>();
        var index = 0;
        const int pageSize = 50;

        // Paginate through all files
        while (true)
        {
            var url = $"mods/{modpackId}/files?pageSize={pageSize}&index={index}";
            var response = await GetWithRetryAsync<CurseForgeFilesResponse>(url, ct).ConfigureAwait(false);

            if (response == null || response.Data.Length == 0)
            {
                break;
            }

            allFiles.AddRange(response.Data);

            if (response.Data.Length < pageSize)
            {
                break;
            }

            index += pageSize;
        }

        // Filter out server pack files (they're linked from main files) and map to versions
        var versions = allFiles
            .Where(f => f.IsAvailable && !f.IsServerPack.GetValueOrDefault())
            .OrderByDescending(f => f.FileDate)
            .Select(f => MapToVersion(f))
            .ToList();

        return versions;
    }

    /// <inheritdoc />
    public async Task<ModpackServerFiles?> GetServerFilesAsync(string modpackId, string versionId, CancellationToken ct = default)
    {
        var url = $"mods/{modpackId}/files/{versionId}";
        var response = await GetWithRetryAsync<CurseForgeFileResponse>(url, ct).ConfigureAwait(false);

        if (response == null)
        {
            return null;
        }

        var file = response.Data;

        // Check if there's a dedicated server pack
        if (file.ServerPackFileId.HasValue)
        {
            var serverPackUrl = $"mods/{modpackId}/files/{file.ServerPackFileId.Value}";
            var serverPackResponse = await GetWithRetryAsync<CurseForgeFileResponse>(serverPackUrl, ct).ConfigureAwait(false);

            if (serverPackResponse != null)
            {
                var serverFile = serverPackResponse.Data;
                var downloadUrl = await GetDownloadUrlAsync(modpackId, serverFile.Id.ToString(), ct).ConfigureAwait(false);

                if (downloadUrl == null)
                {
                    _logger.LogWarning(
                        "Server pack file {FileId} requires manual download (terms not accepted)",
                        serverFile.Id);
                    return null;
                }

                return new ModpackServerFiles
                {
                    DownloadUrl = downloadUrl,
                    SizeBytes = serverFile.FileLength,
                    Sha256 = GetSha256Hash(serverFile.Hashes),
                    FileType = ModpackServerFileType.ServerPack,
                    RequiredJavaVersion = DetectRequiredJavaVersion(file),
                    RecommendedRamMb = EstimateRecommendedRam(file)
                };
            }
        }

        // No dedicated server pack, return the full pack
        var mainDownloadUrl = await GetDownloadUrlAsync(modpackId, versionId, ct).ConfigureAwait(false);

        if (mainDownloadUrl == null)
        {
            _logger.LogWarning(
                "File {FileId} requires manual download (terms not accepted)",
                file.Id);
            return null;
        }

        return new ModpackServerFiles
        {
            DownloadUrl = mainDownloadUrl,
            SizeBytes = file.FileLength,
            Sha256 = GetSha256Hash(file.Hashes),
            FileType = ModpackServerFileType.FullPack,
            RequiredJavaVersion = DetectRequiredJavaVersion(file),
            RecommendedRamMb = EstimateRecommendedRam(file)
        };
    }

    /// <inheritdoc />
    public async Task<ModpackDownloadResult> DownloadServerFilesAsync(
        string modpackId,
        string versionId,
        string destinationPath,
        IProgress<ModpackDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            // Get server files info
            var serverFiles = await GetServerFilesAsync(modpackId, versionId, ct).ConfigureAwait(false);

            if (serverFiles == null)
            {
                return ModpackDownloadResult.Failed(
                    "Unable to get server files. The file may require accepting terms on CurseForge website.");
            }

            // Report starting
            progress?.Report(new ModpackDownloadProgress
            {
                Phase = ModpackDownloadPhase.Starting,
                Message = "Starting download...",
                Percentage = 0
            });

            // Create destination directory
            Directory.CreateDirectory(destinationPath);

            // Download the pack
            var tempFile = Path.Combine(Path.GetTempPath(), $"cf_modpack_{modpackId}_{versionId}.zip");

            try
            {
                var downloadSuccess = await DownloadFileWithProgressAsync(
                    serverFiles.DownloadUrl,
                    tempFile,
                    serverFiles.SizeBytes,
                    progress,
                    ct).ConfigureAwait(false);

                if (!downloadSuccess)
                {
                    return ModpackDownloadResult.Failed("Download failed");
                }

                // Verify checksum if available
                if (!string.IsNullOrEmpty(serverFiles.Sha256))
                {
                    progress?.Report(new ModpackDownloadProgress
                    {
                        Phase = ModpackDownloadPhase.Verifying,
                        Message = "Verifying file integrity...",
                        Percentage = 62
                    });

                    var actualHash = await ComputeSha256Async(tempFile, ct).ConfigureAwait(false);
                    if (!string.Equals(actualHash, serverFiles.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Checksum mismatch for modpack. Expected: {Expected}, Got: {Actual}",
                            serverFiles.Sha256, actualHash);
                        // Don't fail on checksum mismatch, just log it
                    }
                }

                // Extract files
                progress?.Report(new ModpackDownloadProgress
                {
                    Phase = ModpackDownloadPhase.Extracting,
                    Message = "Extracting files...",
                    Percentage = 65
                });

                await ExtractPackAsync(tempFile, destinationPath, serverFiles.FileType, progress, ct).ConfigureAwait(false);

                // Processing phase (cleanup client-only mods if full pack)
                ManifestInfo? manifestInfo = null;
                if (serverFiles.FileType == ModpackServerFileType.FullPack)
                {
                    progress?.Report(new ModpackDownloadProgress
                    {
                        Phase = ModpackDownloadPhase.PostInstall,
                        Message = "Processing server files...",
                        Percentage = 65
                    });

                    manifestInfo = await ProcessFullPackForServerAsync(destinationPath, progress, ct).ConfigureAwait(false);
                }

                // Complete
                progress?.Report(ModpackDownloadProgress.Completed());

                var computedHash = await ComputeSha256Async(tempFile, ct).ConfigureAwait(false);

                // Return result with manifest info if available
                if (manifestInfo is not null)
                {
                    return ModpackDownloadResult.Succeeded(
                        destinationPath,
                        computedHash,
                        serverFiles.FileType,
                        manifestInfo.MinecraftVersion,
                        manifestInfo.LoaderType,
                        manifestInfo.LoaderVersion);
                }

                return ModpackDownloadResult.Succeeded(
                    destinationPath,
                    computedHash,
                    serverFiles.FileType);
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp file {TempFile}", tempFile);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            return ModpackDownloadResult.Failed("Download was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download modpack {ModpackId} version {VersionId}", modpackId, versionId);
            return ModpackDownloadResult.Failed($"Download failed: {ex.Message}");
        }
    }

    #region Private Helper Methods

    private static int MapSortField(ModpackSearchSort sort) => sort switch
    {
        ModpackSearchSort.Popularity => CurseForgeSortField.Popularity,
        ModpackSearchSort.RecentlyUpdated => CurseForgeSortField.LastUpdated,
        ModpackSearchSort.TotalDownloads => CurseForgeSortField.TotalDownloads,
        ModpackSearchSort.Name => CurseForgeSortField.Name,
        _ => CurseForgeSortField.Popularity
    };

    private static int? MapModLoaderType(ModLoaderType loader) => loader switch
    {
        ModLoaderType.Forge => CurseForgeModLoaderType.Forge,
        ModLoaderType.Fabric => CurseForgeModLoaderType.Fabric,
        ModLoaderType.Quilt => CurseForgeModLoaderType.Quilt,
        ModLoaderType.NeoForge => CurseForgeModLoaderType.NeoForge,
        _ => null
    };

    private static ModLoaderType MapCurseForgeLoader(int loaderType) => loaderType switch
    {
        CurseForgeModLoaderType.Forge => ModLoaderType.Forge,
        CurseForgeModLoaderType.Fabric => ModLoaderType.Fabric,
        CurseForgeModLoaderType.Quilt => ModLoaderType.Quilt,
        CurseForgeModLoaderType.NeoForge => ModLoaderType.NeoForge,
        _ => ModLoaderType.None
    };

    private ModpackSearchItem MapToSearchItem(CurseForgeMod mod)
    {
        var minecraftVersions = ExtractMinecraftVersions(mod.LatestFiles);
        var loaders = DetectLoaders(mod);
        var hasServerPack = mod.LatestFiles.Any(f =>
            f.ServerPackFileId.HasValue || IsServerPackFileName(f.FileName));

        return new ModpackSearchItem
        {
            Id = mod.Id.ToString(),
            Slug = mod.Slug,
            Name = mod.Name,
            Summary = mod.Summary,
            IconUrl = mod.Logo?.Url,
            Author = mod.Authors.FirstOrDefault()?.Name,
            Downloads = mod.DownloadCount,
            UpdatedAt = mod.DateModified,
            MinecraftVersions = minecraftVersions,
            Loaders = loaders,
            Provider = ModpackProviderType.CurseForge,
            HasServerPack = hasServerPack
        };
    }

    private ModpackVersion MapToVersion(CurseForgeFile file)
    {
        var minecraftVersion = ExtractMinecraftVersionFromFile(file);
        var loader = DetectLoaderFromFile(file);

        return new ModpackVersion
        {
            Id = file.Id.ToString(),
            Name = file.DisplayName,
            MinecraftVersion = minecraftVersion,
            Loader = loader,
            LoaderVersion = null, // CurseForge doesn't provide this directly
            ReleasedAt = file.FileDate,
            Downloads = file.DownloadCount,
            Type = MapReleaseType(file.ReleaseType),
            Changelog = null, // Would need separate API call for changelog
            HasServerPack = file.ServerPackFileId.HasValue || IsServerPackFileName(file.FileName),
            ServerPackSize = file.ServerPackFileId.HasValue ? null : file.FileLength
        };
    }

    private static ModpackVersionType MapReleaseType(int releaseType) => releaseType switch
    {
        CurseForgeReleaseType.Release => ModpackVersionType.Release,
        CurseForgeReleaseType.Beta => ModpackVersionType.Beta,
        CurseForgeReleaseType.Alpha => ModpackVersionType.Alpha,
        _ => ModpackVersionType.Release
    };

    private static List<string> ExtractMinecraftVersions(CurseForgeFile[] files)
    {
        var versions = new HashSet<string>();

        foreach (var file in files)
        {
            foreach (var version in file.GameVersions)
            {
                // Filter to only Minecraft versions (format: x.x.x or x.x)
                if (MinecraftVersionRegex().IsMatch(version))
                {
                    versions.Add(version);
                }
            }
        }

        return versions.OrderByDescending(v => v).ToList();
    }

    private static string ExtractMinecraftVersionFromFile(CurseForgeFile file)
    {
        // Try sortable game versions first (more accurate)
        if (file.SortableGameVersions != null)
        {
            foreach (var sgv in file.SortableGameVersions)
            {
                if (MinecraftVersionRegex().IsMatch(sgv.GameVersion))
                {
                    return sgv.GameVersion;
                }
            }
        }

        // Fall back to game versions
        foreach (var version in file.GameVersions)
        {
            if (MinecraftVersionRegex().IsMatch(version))
            {
                return version;
            }
        }

        return "Unknown";
    }

    private List<ModLoaderType> DetectLoaders(CurseForgeMod mod)
    {
        var loaders = new HashSet<ModLoaderType>();

        // Check categories for loader indicators
        foreach (var category in mod.Categories)
        {
            if (LoaderCategoryIds.TryGetValue(category.Id, out var loader))
            {
                loaders.Add(loader);
            }
        }

        // Check latest files indexes for loader type
        if (mod.LatestFilesIndexes != null)
        {
            foreach (var fileIndex in mod.LatestFilesIndexes)
            {
                if (fileIndex.ModLoader.HasValue)
                {
                    var loader = MapCurseForgeLoader(fileIndex.ModLoader.Value);
                    if (loader != ModLoaderType.None)
                    {
                        loaders.Add(loader);
                    }
                }
            }
        }

        // If no specific loader found, check file names for patterns
        if (loaders.Count == 0)
        {
            foreach (var file in mod.LatestFiles)
            {
                var fileLoader = DetectLoaderFromFileName(file.FileName);
                if (fileLoader != ModLoaderType.None)
                {
                    loaders.Add(fileLoader);
                }
            }
        }

        // Default to Forge if no loader detected (most common for modpacks)
        if (loaders.Count == 0)
        {
            loaders.Add(ModLoaderType.Forge);
        }

        return loaders.ToList();
    }

    private static ModLoaderType DetectLoaderFromFile(CurseForgeFile file)
    {
        // Check sortable game versions for loader type ID
        if (file.SortableGameVersions != null)
        {
            foreach (var sgv in file.SortableGameVersions)
            {
                // Type ID 68441 is Forge, 75125 is Fabric, etc.
                // But we'll rely on file name patterns as type IDs aren't documented
            }
        }

        // Check game versions for loader names
        foreach (var version in file.GameVersions)
        {
            var lower = version.ToLowerInvariant();
            if (lower.Contains("fabric"))
                return ModLoaderType.Fabric;
            if (lower.Contains("quilt"))
                return ModLoaderType.Quilt;
            if (lower.Contains("neoforge"))
                return ModLoaderType.NeoForge;
            if (lower.Contains("forge"))
                return ModLoaderType.Forge;
        }

        // Fall back to file name detection
        return DetectLoaderFromFileName(file.FileName);
    }

    private static ModLoaderType DetectLoaderFromFileName(string fileName)
    {
        var lower = fileName.ToLowerInvariant();

        if (lower.Contains("fabric"))
            return ModLoaderType.Fabric;
        if (lower.Contains("quilt"))
            return ModLoaderType.Quilt;
        if (lower.Contains("neoforge"))
            return ModLoaderType.NeoForge;
        if (lower.Contains("forge"))
            return ModLoaderType.Forge;

        return ModLoaderType.None;
    }

    private static bool IsServerPackFileName(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.Contains("-server") ||
               lower.Contains("_server") ||
               lower.Contains("server-") ||
               lower.Contains("server_") ||
               lower.EndsWith("-server.zip", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ModpackLink> BuildLinks(CurseForgeMod mod)
    {
        var links = new List<ModpackLink>();

        if (!string.IsNullOrEmpty(mod.Links?.WebsiteUrl))
        {
            links.Add(new ModpackLink { Type = "website", Url = mod.Links.WebsiteUrl, Name = "CurseForge" });
        }

        if (!string.IsNullOrEmpty(mod.Links?.WikiUrl))
        {
            links.Add(new ModpackLink { Type = "wiki", Url = mod.Links.WikiUrl, Name = "Wiki" });
        }

        if (!string.IsNullOrEmpty(mod.Links?.SourceUrl))
        {
            links.Add(new ModpackLink { Type = "source", Url = mod.Links.SourceUrl, Name = "Source" });
        }

        if (!string.IsNullOrEmpty(mod.Links?.IssuesUrl))
        {
            links.Add(new ModpackLink { Type = "issues", Url = mod.Links.IssuesUrl, Name = "Issues" });
        }

        return links;
    }

    private static int GetModCountFromLatestFile(CurseForgeMod mod)
    {
        // The modules array in a file represents the contents of the modpack
        // Each module with name starting with "mods/" is a mod
        var latestFile = mod.LatestFiles.FirstOrDefault();
        if (latestFile?.Modules == null)
        {
            return 0;
        }

        return latestFile.Modules.Count(m =>
            m.Name.StartsWith("mods/", StringComparison.OrdinalIgnoreCase) ||
            m.Name.StartsWith("mods\\", StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetSha256Hash(CurseForgeHash[] hashes)
    {
        // CurseForge uses SHA1 (algo=1) and MD5 (algo=2), not SHA256
        // Return null as we don't have SHA256
        return null;
    }

    private static string? DetectRequiredJavaVersion(CurseForgeFile file)
    {
        var mcVersion = ExtractMinecraftVersionFromFile(file);

        // Minecraft version to Java requirements
        // 1.17+ requires Java 16+
        // 1.18+ requires Java 17+
        // 1.20.5+ requires Java 21+
        if (string.IsNullOrEmpty(mcVersion) || mcVersion == "Unknown")
        {
            return "17"; // Safe default
        }

        var parts = mcVersion.Split('.');
        if (parts.Length >= 2 && int.TryParse(parts[1], out var minor))
        {
            if (minor >= 20 && parts.Length >= 3 && int.TryParse(parts[2], out var patch) && patch >= 5)
            {
                return "21";
            }
            if (minor >= 18)
            {
                return "17";
            }
            if (minor >= 17)
            {
                return "16";
            }
        }

        return "8"; // Pre-1.17
    }

    private static long EstimateRecommendedRam(CurseForgeFile file)
    {
        // Estimate RAM based on file size
        // Larger packs generally need more RAM
        var sizeMb = file.FileLength / (1024 * 1024);

        return sizeMb switch
        {
            > 500 => 8192, // Large packs (500MB+) -> 8GB
            > 200 => 6144, // Medium-large packs (200-500MB) -> 6GB
            > 100 => 4096, // Medium packs (100-200MB) -> 4GB
            > 50 => 3072,  // Small-medium packs (50-100MB) -> 3GB
            _ => 2048      // Small packs (<50MB) -> 2GB
        };
    }

    private async Task<string?> GetDescriptionAsync(string modpackId, CancellationToken ct)
    {
        var url = $"mods/{modpackId}/description";
        var response = await GetWithRetryAsync<CurseForgeDescriptionResponse>(url, ct).ConfigureAwait(false);
        return response?.Data;
    }

    private async Task<string?> GetDownloadUrlAsync(string modpackId, string fileId, CancellationToken ct)
    {
        // First try the direct download URL from file data
        var url = $"mods/{modpackId}/files/{fileId}/download-url";
        var response = await GetWithRetryAsync<CurseForgeDownloadUrlResponse>(url, ct).ConfigureAwait(false);
        return response?.Data;
    }

    private async Task<bool> DownloadFileWithProgressAsync(
        string url,
        string destinationPath,
        long expectedSize,
        IProgress<ModpackDownloadProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                ct).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? expectedSize;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var fileStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            var buffer = new byte[BufferSize];
            long bytesRead = 0;
            int read;
            var lastProgressReport = DateTime.UtcNow;

            while ((read = await contentStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                bytesRead += read;

                // Report progress at most every 100ms (downloading is 0-60%)
                if (progress != null && (DateTime.UtcNow - lastProgressReport).TotalMilliseconds >= 100)
                {
                    var downloadPercentage = totalBytes > 0 ? (double)bytesRead / totalBytes : 0;
                    var overallPercentage = downloadPercentage * 60; // 0-60% for download

                    progress.Report(new ModpackDownloadProgress
                    {
                        Phase = ModpackDownloadPhase.Downloading,
                        Message = $"Downloading... {bytesRead / (1024 * 1024):F1} MB / {totalBytes / (1024 * 1024):F1} MB",
                        Percentage = overallPercentage,
                        BytesDownloaded = bytesRead,
                        TotalBytes = totalBytes
                    });

                    lastProgressReport = DateTime.UtcNow;
                }
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to download file from {Url}", url);
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error downloading file to {Path}", destinationPath);
            return false;
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task ExtractPackAsync(
        string zipPath,
        string destinationPath,
        ModpackServerFileType fileType,
        IProgress<ModpackDownloadProgress>? progress,
        CancellationToken ct)
    {
        await using var zipStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
        var totalEntries = entries.Count;
        var processedEntries = 0;

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            var entryPath = Path.Combine(destinationPath, entry.FullName);
            var entryDir = Path.GetDirectoryName(entryPath);

            if (!string.IsNullOrEmpty(entryDir))
            {
                Directory.CreateDirectory(entryDir);
            }

            // Skip directories
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                continue;
            }

            await using var entryStream = entry.Open();
            await using var outputStream = new FileStream(entryPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            await entryStream.CopyToAsync(outputStream, ct).ConfigureAwait(false);

            processedEntries++;

            // Report extraction progress (60-90%)
            if (progress != null && processedEntries % 10 == 0)
            {
                var extractPercentage = (double)processedEntries / totalEntries;
                var overallPercentage = 60 + (extractPercentage * 30); // 60-90% for extraction

                progress.Report(new ModpackDownloadProgress
                {
                    Phase = ModpackDownloadPhase.Extracting,
                    Message = $"Extracting... {processedEntries}/{totalEntries}",
                    Percentage = overallPercentage,
                    CurrentFile = entry.Name
                });
            }
        }
    }

    private async Task<ManifestInfo?> ProcessFullPackForServerAsync(
        string destinationPath,
        IProgress<ModpackDownloadProgress>? progress,
        CancellationToken ct)
    {
        // When processing a full pack, we need to:
        // 1. Look for manifest.json or modrinth.index.json to identify client-only mods
        // 2. Remove client-only mods if identified
        // 3. Keep all server-side mods

        var modsPath = Path.Combine(destinationPath, "mods");

        // Check for CurseForge manifest.json
        var manifestPath = Path.Combine(destinationPath, "manifest.json");
        if (File.Exists(manifestPath))
        {
            return await ProcessCurseForgeManifestAsync(destinationPath, manifestPath, progress, ct).ConfigureAwait(false);
        }

        // No manifest to process, just clean up obvious client-only mods if mods folder exists
        if (Directory.Exists(modsPath))
        {
            await CleanupClientOnlyModsAsync(modsPath, ct).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Parsed manifest information returned from processing.
    /// </summary>
    private record ManifestInfo(
        string? MinecraftVersion,
        ModLoaderType LoaderType,
        string? LoaderVersion
    );

    private async Task<ManifestInfo?> ProcessCurseForgeManifestAsync(
        string destinationPath,
        string manifestPath,
        IProgress<ModpackDownloadProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            // Parse the manifest
            var manifest = await ParseManifestAsync(manifestPath, ct).ConfigureAwait(false);
            if (manifest == null)
            {
                _logger.LogWarning("Failed to parse manifest at {Path}", manifestPath);
                return null;
            }

            _logger.LogInformation(
                "Processing CurseForge manifest: {Name} with {ModCount} mods",
                manifest.Name, manifest.Files.Length);

            // Extract manifest loader info
            var (loaderType, loaderVersion) = ParseManifestModLoader(manifest);
            var manifestInfo = new ManifestInfo(
                manifest.Minecraft.Version,
                loaderType,
                loaderVersion
            );

            _logger.LogInformation(
                "Manifest specifies: Minecraft {McVersion}, {Loader} {LoaderVersion}",
                manifestInfo.MinecraftVersion,
                manifestInfo.LoaderType,
                manifestInfo.LoaderVersion ?? "latest");

            // Download mods from manifest
            if (manifest.Files.Length > 0)
            {
                var modsPath = Path.Combine(destinationPath, "mods");
                Directory.CreateDirectory(modsPath);

                var result = await DownloadManifestModsAsync(
                    manifest.Files,
                    modsPath,
                    progress,
                    ct).ConfigureAwait(false);

                if (result.FailedMods.Count > 0)
                {
                    _logger.LogWarning(
                        "Failed to download {Count} mods: {Mods}",
                        result.FailedMods.Count,
                        string.Join(", ", result.FailedMods.Select(m => $"{m.ModName ?? m.ProjectId.ToString()} ({m.Reason})")));
                }

                _logger.LogInformation(
                    "Manifest processing complete: {Downloaded} downloaded, {Skipped} skipped, {Failed} failed",
                    result.DownloadedMods, result.SkippedMods, result.FailedMods.Count);
            }

            // Process overrides folder
            var overridesFolder = manifest.Overrides ?? "overrides";
            var overridesPath = Path.Combine(destinationPath, overridesFolder);

            if (Directory.Exists(overridesPath))
            {
                progress?.Report(new ModpackDownloadProgress
                {
                    Phase = ModpackDownloadPhase.PostInstall,
                    Message = "Extracting overrides...",
                    Percentage = 90
                });

                await ExtractOverridesAsync(overridesPath, destinationPath, ct).ConfigureAwait(false);
            }

            return manifestInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process CurseForge manifest at {Path}", manifestPath);
            return null;
        }
    }

    private static async Task<CurseForgeManifest?> ParseManifestAsync(string manifestPath, CancellationToken ct)
    {
        try
        {
            var manifestJson = await File.ReadAllTextAsync(manifestPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<CurseForgeManifest>(manifestJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the mod loader type and version from the manifest's modLoaders array.
    /// Example: "neoforge-21.1.216" → (NeoForge, "21.1.216")
    /// </summary>
    private static (ModLoaderType LoaderType, string? Version) ParseManifestModLoader(CurseForgeManifest manifest)
    {
        var primaryLoader = manifest.Minecraft.ModLoaders?.FirstOrDefault(ml => ml.Primary)
            ?? manifest.Minecraft.ModLoaders?.FirstOrDefault();

        if (primaryLoader is null || string.IsNullOrEmpty(primaryLoader.Id))
        {
            return (ModLoaderType.None, null);
        }

        var loaderId = primaryLoader.Id.ToLowerInvariant();

        // Parse loader ID format: "loader-version" (e.g., "neoforge-21.1.216", "forge-47.2.0", "fabric-0.15.3")
        if (loaderId.StartsWith("neoforge-"))
        {
            return (ModLoaderType.NeoForge, primaryLoader.Id[9..]); // Skip "neoforge-"
        }
        if (loaderId.StartsWith("forge-"))
        {
            return (ModLoaderType.Forge, primaryLoader.Id[6..]); // Skip "forge-"
        }
        if (loaderId.StartsWith("fabric-"))
        {
            return (ModLoaderType.Fabric, primaryLoader.Id[7..]); // Skip "fabric-"
        }
        if (loaderId.StartsWith("quilt-"))
        {
            return (ModLoaderType.Quilt, primaryLoader.Id[6..]); // Skip "quilt-"
        }

        // Try to detect from name alone
        if (loaderId.Contains("neoforge"))
            return (ModLoaderType.NeoForge, null);
        if (loaderId.Contains("forge"))
            return (ModLoaderType.Forge, null);
        if (loaderId.Contains("fabric"))
            return (ModLoaderType.Fabric, null);
        if (loaderId.Contains("quilt"))
            return (ModLoaderType.Quilt, null);

        return (ModLoaderType.None, null);
    }

    private async Task<ManifestDownloadResult> DownloadManifestModsAsync(
        CurseForgeManifestFile[] files,
        string modsPath,
        IProgress<ModpackDownloadProgress>? progress,
        CancellationToken ct)
    {
        var totalMods = files.Length;
        var downloadedMods = 0;
        var skippedMods = 0;
        var failedMods = new List<FailedModDownload>();

        // Get unique project IDs to fetch mod metadata in batches
        var projectIds = files.Select(f => f.ProjectId).Distinct().ToArray();
        var modMetadata = new Dictionary<int, CurseForgeMod>();

        progress?.Report(new ModpackDownloadProgress
        {
            Phase = ModpackDownloadPhase.PostInstall,
            Message = "Fetching mod metadata...",
            Percentage = 65
        });

        // Fetch mod metadata in batches (for client-only detection)
        foreach (var batch in projectIds.Chunk(BatchSize))
        {
            ct.ThrowIfCancellationRequested();

            var mods = await _apiClient.GetModsBatchAsync(batch, ct).ConfigureAwait(false);
            foreach (var mod in mods)
            {
                modMetadata[mod.Id] = mod;
            }
        }

        // Get file metadata in batches (for download URLs)
        var fileIds = files.Select(f => f.FileId).ToArray();
        var fileMetadata = new Dictionary<int, CurseForgeFile>();

        progress?.Report(new ModpackDownloadProgress
        {
            Phase = ModpackDownloadPhase.PostInstall,
            Message = "Fetching file metadata...",
            Percentage = 68
        });

        foreach (var batch in fileIds.Chunk(BatchSize))
        {
            ct.ThrowIfCancellationRequested();

            var fileInfos = await _apiClient.GetFilesBatchAsync(batch, ct).ConfigureAwait(false);
            foreach (var file in fileInfos)
            {
                fileMetadata[file.Id] = file;
            }
        }

        // Prepare download tasks
        var downloadTasks = new List<Task<(CurseForgeManifestFile File, bool Success, string? Error, bool Skipped)>>();
        var processedCount = 0;

        progress?.Report(new ModpackDownloadProgress
        {
            Phase = ModpackDownloadPhase.PostInstall,
            Message = $"Downloading mods (0/{totalMods})...",
            Percentage = 70
        });

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            // Check if mod is client-only
            if (modMetadata.TryGetValue(file.ProjectId, out var mod) && IsLikelyClientOnlyMod(mod))
            {
                _logger.LogDebug("Skipping client-only mod: {ModName}", mod.Name);
                Interlocked.Increment(ref skippedMods);
                continue;
            }

            // Skip optional mods that aren't required
            if (!file.Required)
            {
                _logger.LogDebug("Skipping optional mod: {ProjectId}", file.ProjectId);
                Interlocked.Increment(ref skippedMods);
                continue;
            }

            downloadTasks.Add(DownloadSingleModAsync(
                file,
                modsPath,
                modMetadata.GetValueOrDefault(file.ProjectId),
                fileMetadata.GetValueOrDefault(file.FileId),
                () =>
                {
                    var current = Interlocked.Increment(ref processedCount);
                    // Progress 70-88%
                    var pct = 70 + ((double)current / totalMods * 18);
                    progress?.Report(new ModpackDownloadProgress
                    {
                        Phase = ModpackDownloadPhase.PostInstall,
                        Message = $"Downloading mods ({current}/{totalMods})...",
                        Percentage = pct
                    });
                },
                ct));
        }

        // Wait for all downloads to complete
        var results = await Task.WhenAll(downloadTasks).ConfigureAwait(false);

        foreach (var (file, success, error, skipped) in results)
        {
            if (skipped)
            {
                skippedMods++;
            }
            else if (success)
            {
                downloadedMods++;
            }
            else
            {
                var modName = modMetadata.TryGetValue(file.ProjectId, out var mod) ? mod.Name : null;
                failedMods.Add(new FailedModDownload(file.ProjectId, file.FileId, modName, error ?? "unknown"));
            }
        }

        return ManifestDownloadResult.Succeeded(totalMods, downloadedMods, skippedMods, failedMods);
    }

    private async Task<(CurseForgeManifestFile File, bool Success, string? Error, bool Skipped)> DownloadSingleModAsync(
        CurseForgeManifestFile manifestFile,
        string modsPath,
        CurseForgeMod? mod,
        CurseForgeFile? file,
        Action onProgress,
        CancellationToken ct)
    {
        await _downloadSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Get download URL
            string? downloadUrl = file?.DownloadUrl;

            // If no direct URL, try to get it via API (handles restricted mods)
            if (string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = await _apiClient.GetDownloadUrlAsync(
                    manifestFile.ProjectId,
                    manifestFile.FileId,
                    ct).ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                // Check if mod has distribution disabled
                if (mod?.AllowModDistribution == false)
                {
                    _logger.LogWarning(
                        "Mod {ModName} (ID: {ProjectId}) has third-party distribution disabled",
                        mod.Name, manifestFile.ProjectId);
                    return (manifestFile, false, "restricted", false);
                }

                return (manifestFile, false, "unavailable", false);
            }

            // Determine filename
            var fileName = file?.FileName ?? $"{manifestFile.ProjectId}_{manifestFile.FileId}.jar";
            var destPath = Path.Combine(modsPath, fileName);

            // Download the file
            var (success, error) = await _apiClient.DownloadFileAsync(
                downloadUrl,
                destPath,
                null,
                ct).ConfigureAwait(false);

            onProgress();

            if (!success)
            {
                _logger.LogWarning(
                    "Failed to download mod {ModName} (ID: {ProjectId}): {Error}",
                    mod?.Name ?? manifestFile.ProjectId.ToString(),
                    manifestFile.ProjectId,
                    error);
                return (manifestFile, false, "download_failed", false);
            }

            return (manifestFile, true, null, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error downloading mod {ProjectId}/{FileId}",
                manifestFile.ProjectId,
                manifestFile.FileId);
            return (manifestFile, false, "download_failed", false);
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    private static bool IsLikelyClientOnlyMod(CurseForgeMod mod)
    {
        var nameLower = mod.Name.ToLowerInvariant();
        var slugLower = mod.Slug.ToLowerInvariant();

        // Check against known client-only patterns
        foreach (var pattern in ClientOnlyPatterns)
        {
            if (nameLower.Contains(pattern) || slugLower.Contains(pattern))
            {
                return true;
            }
        }

        // Check categories for client-side indicators
        // Category 423 = Shaders, 424 = Resource Packs (client-only by nature)
        foreach (var category in mod.Categories)
        {
            var categoryLower = category.Name.ToLowerInvariant();
            if (categoryLower.Contains("shader") ||
                categoryLower.Contains("resource pack") ||
                categoryLower.Contains("texture"))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task ExtractOverridesAsync(string overridesPath, string destinationPath, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            foreach (var dir in Directory.GetDirectories(overridesPath))
            {
                ct.ThrowIfCancellationRequested();

                var destDir = Path.Combine(destinationPath, Path.GetFileName(dir));
                if (Directory.Exists(destDir))
                {
                    // Merge directories
                    foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(dir, file);
                        var destFile = Path.Combine(destDir, relativePath);
                        var destFileDir = Path.GetDirectoryName(destFile);
                        if (!string.IsNullOrEmpty(destFileDir))
                        {
                            Directory.CreateDirectory(destFileDir);
                        }
                        File.Copy(file, destFile, overwrite: true);
                    }
                }
                else
                {
                    Directory.Move(dir, destDir);
                }
            }

            foreach (var file in Directory.GetFiles(overridesPath))
            {
                ct.ThrowIfCancellationRequested();

                var destFile = Path.Combine(destinationPath, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            // Clean up overrides folder
            Directory.Delete(overridesPath, recursive: true);
        }, ct).ConfigureAwait(false);
    }

    private async Task CleanupClientOnlyModsAsync(string modsPath, CancellationToken ct)
    {
        // Known client-only mod patterns
        var clientOnlyPatterns = new[]
        {
            "optifine", "optifabric", "shaders", "shader",
            "better-fps", "fps-reducer", "smoothboot",
            "entityculling", "entity-culling", "cullleaves",
            "oculus", "iris", "sodium-extra", "reese-sodium",
            "dynamic-fps", "modernfix-forge" // Keep modernfix-fabric as it's server compatible
        };

        await Task.Run(() =>
        {
            foreach (var modFile in Directory.GetFiles(modsPath, "*.jar"))
            {
                ct.ThrowIfCancellationRequested();

                var fileName = Path.GetFileNameWithoutExtension(modFile).ToLowerInvariant();
                if (clientOnlyPatterns.Any(p => fileName.Contains(p)))
                {
                    try
                    {
                        File.Delete(modFile);
                        _logger.LogDebug("Removed client-only mod: {ModFile}", Path.GetFileName(modFile));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to remove client-only mod: {ModFile}", modFile);
                    }
                }
            }
        }, ct).ConfigureAwait(false);
    }

    #endregion

    #region HTTP Helpers with Retry and Rate Limiting

    private async Task<T?> GetWithRetryAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("CurseForge API key is not configured");
            return null;
        }

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await WaitForRateLimitAsync(cancellationToken).ConfigureAwait(false);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("x-api-key", _settings.ApiKey);

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                UpdateRateLimitInfo(response);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("Access forbidden for {Url} - API key may be invalid or resource restricted", url);
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = GetRetryAfterSeconds(response);
                    _logger.LogWarning(
                        "Rate limited by CurseForge API, waiting {Seconds}s before retry (attempt {Attempt}/{MaxRetries})",
                        retryAfter, attempt, MaxRetries);

                    await Task.Delay(TimeSpan.FromSeconds(retryAfter + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s
                _logger.LogWarning(
                    ex,
                    "HTTP error on attempt {Attempt}/{MaxRetries}, retrying in {Delay}s",
                    attempt, MaxRetries, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize response from {Url}", url);
                return null;
            }
        }

        _logger.LogError("Failed to get {Url} after {MaxRetries} attempts", url, MaxRetries);
        return null;
    }

    private async Task WaitForRateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_rateLimitRemaining < 10)
            {
                var waitTime = _rateLimitResetTime - DateTime.UtcNow;
                if (waitTime > TimeSpan.Zero)
                {
                    _logger.LogDebug(
                        "Rate limit buffer reached ({Remaining} remaining), waiting {Seconds}s",
                        _rateLimitRemaining, waitTime.TotalSeconds);

                    await Task.Delay(waitTime + TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private void UpdateRateLimitInfo(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues))
        {
            if (int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
            {
                _rateLimitRemaining = remaining;
            }
        }

        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues))
        {
            if (int.TryParse(resetValues.FirstOrDefault(), out var resetSeconds))
            {
                _rateLimitResetTime = DateTime.UtcNow.AddSeconds(resetSeconds);
            }
        }
    }

    private static int GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            if (int.TryParse(values.FirstOrDefault(), out var seconds))
            {
                return seconds;
            }
        }

        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues))
        {
            if (int.TryParse(resetValues.FirstOrDefault(), out var seconds))
            {
                return seconds;
            }
        }

        // Default to 60 seconds if header not present
        return 60;
    }

    #endregion

    [GeneratedRegex(@"^1\.\d+(\.\d+)?$")]
    private static partial Regex MinecraftVersionRegex();
}

/// <summary>
/// CurseForge API description response wrapper.
/// </summary>
internal sealed record CurseForgeDescriptionResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("data")] string Data
);
