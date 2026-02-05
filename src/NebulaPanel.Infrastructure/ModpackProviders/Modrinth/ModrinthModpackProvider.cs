using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.ModProviders.Modrinth;

namespace NebulaPanel.Infrastructure.ModpackProviders.Modrinth;

/// <summary>
/// Modpack provider implementation for Modrinth - a modern, open-source mod repository.
/// </summary>
public sealed partial class ModrinthModpackProvider : IModpackProvider
{
    private const int BufferSize = 81920; // 80KB buffer for downloads
    private const int MaxRetries = 3;

    private readonly ModrinthApiClient _apiClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ModrinthModpackProvider> _logger;

    public ModrinthModpackProvider(
        ModrinthApiClient apiClient,
        IHttpClientFactory httpClientFactory,
        ILogger<ModrinthModpackProvider> logger)
    {
        _apiClient = apiClient;
        _httpClient = httpClientFactory.CreateClient("Modrinth");
        _logger = logger;
    }

    /// <inheritdoc />
    public ModpackProviderType ProviderType => ModpackProviderType.Modrinth;

    /// <inheritdoc />
    public string DisplayName => "Modrinth";

    /// <inheritdoc />
    public string? IconUrl => "https://modrinth.com/favicon.ico";

    /// <inheritdoc />
    public async Task<ModpackSearchResult> SearchAsync(ModpackSearchQuery query, CancellationToken ct = default)
    {
        // Build facets for the search
        var facets = new List<string[]>
        {
            new[] { "project_type:modpack" }
        };

        if (!string.IsNullOrWhiteSpace(query.MinecraftVersion))
        {
            facets.Add(new[] { $"versions:{query.MinecraftVersion}" });
        }

        if (query.LoaderType.HasValue && query.LoaderType.Value != ModLoaderType.None)
        {
            var loaderFacet = MapLoaderToFacet(query.LoaderType.Value);
            if (loaderFacet != null)
            {
                facets.Add(new[] { $"categories:{loaderFacet}" });
            }
        }

        var index = MapSortToIndex(query.Sort);
        var offset = (query.Page - 1) * query.PageSize;

        var response = await _apiClient.SearchAsync(
            query.Query,
            facets,
            index,
            offset,
            query.PageSize,
            ct).ConfigureAwait(false);

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

        var modpacks = response.Hits.Select(MapToSearchItem).ToList();

        return new ModpackSearchResult
        {
            Modpacks = modpacks,
            TotalCount = response.TotalHits,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)response.TotalHits / query.PageSize)
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModpackSearchItem>> GetFeaturedAsync(int count = 20, CancellationToken ct = default)
    {
        var facets = new List<string[]>
        {
            new[] { "project_type:modpack" }
        };

        var response = await _apiClient.SearchAsync(
            null,
            facets,
            "downloads",
            0,
            count,
            ct).ConfigureAwait(false);

        if (response == null)
        {
            return [];
        }

        return response.Hits.Select(MapToSearchItem).ToList();
    }

    /// <inheritdoc />
    public async Task<ModpackDetails> GetDetailsAsync(string modpackId, CancellationToken ct = default)
    {
        var project = await _apiClient.GetProjectAsync(modpackId, ct).ConfigureAwait(false);

        if (project == null)
        {
            throw new InvalidOperationException($"Modpack with ID {modpackId} not found");
        }

        // Get team members for author information
        var teamMembers = await _apiClient.GetProjectTeamAsync(project.Id, ct).ConfigureAwait(false);

        var authors = teamMembers
            .Where(m => m.Accepted)
            .OrderBy(m => m.Ordering)
            .Select(m => new ModpackAuthor
            {
                Name = m.User.Name ?? m.User.Username,
                Url = $"https://modrinth.com/user/{m.User.Username}"
            })
            .ToList();

        var screenshots = project.Gallery?
            .OrderBy(g => g.Ordering)
            .Select(g => new ModpackScreenshot
            {
                Url = g.Url,
                Title = g.Title
            })
            .ToList() ?? [];

        var links = BuildLinks(project);
        var loaders = DetectLoaders(project.Loaders);
        var hasServerPack = project.ServerSide != "unsupported";

        return new ModpackDetails
        {
            Id = project.Id,
            Slug = project.Slug,
            Name = project.Title,
            Summary = project.Description,
            Description = project.Body,
            IconUrl = project.IconUrl,
            BannerUrl = project.Gallery?.FirstOrDefault(g => g.Featured)?.Url,
            Authors = authors,
            Downloads = project.Downloads,
            CreatedAt = ParseDateTime(project.Published),
            UpdatedAt = ParseDateTime(project.Updated),
            Categories = project.Categories.ToList(),
            MinecraftVersions = project.GameVersions.Where(v => MinecraftVersionRegex().IsMatch(v)).ToList(),
            Loaders = loaders,
            Screenshots = screenshots,
            Links = links,
            Provider = ModpackProviderType.Modrinth,
            ModCount = 0, // Modrinth doesn't provide this directly, would need to fetch versions
            HasServerPack = hasServerPack
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModpackVersion>> GetVersionsAsync(string modpackId, CancellationToken ct = default)
    {
        var versions = await _apiClient.GetProjectVersionsAsync(modpackId, cancellationToken: ct).ConfigureAwait(false);

        return versions
            .Where(v => v.Status == "listed" || v.Status == "archived")
            .OrderByDescending(v => ParseDateTime(v.DatePublished))
            .Select(MapToVersion)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ModpackServerFiles?> GetServerFilesAsync(string modpackId, string versionId, CancellationToken ct = default)
    {
        var version = await _apiClient.GetVersionAsync(versionId, ct).ConfigureAwait(false);

        if (version == null)
        {
            return null;
        }

        // Find the primary .mrpack file
        var mrpackFile = version.Files.FirstOrDefault(f => f.Primary && f.Filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))
            ?? version.Files.FirstOrDefault(f => f.Filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase));

        if (mrpackFile == null)
        {
            _logger.LogWarning("No .mrpack file found for version {VersionId}", versionId);
            return null;
        }

        // Detect Java version from Minecraft version
        var mcVersion = version.GameVersions
            .FirstOrDefault(v => MinecraftVersionRegex().IsMatch(v));
        var javaVersion = DetectRequiredJavaVersion(mcVersion);

        // Estimate RAM based on file size
        var recommendedRam = EstimateRecommendedRam(mrpackFile.Size);

        return new ModpackServerFiles
        {
            DownloadUrl = mrpackFile.Url,
            SizeBytes = mrpackFile.Size,
            Sha256 = null, // Modrinth uses SHA512, not SHA256
            FileType = ModpackServerFileType.FullPack, // MRPACK needs processing
            RequiredJavaVersion = javaVersion,
            RecommendedRamMb = recommendedRam
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
                return ModpackDownloadResult.Failed("Unable to get server files for this modpack version");
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

            // Download the mrpack file
            var tempMrpackFile = Path.Combine(Path.GetTempPath(), $"modrinth_modpack_{modpackId}_{versionId}.mrpack");

            try
            {
                // Phase 1: Download modpack index (0-10%)
                progress?.Report(new ModpackDownloadProgress
                {
                    Phase = ModpackDownloadPhase.Downloading,
                    Message = "Downloading modpack index...",
                    Percentage = 2
                });

                var downloadSuccess = await DownloadFileAsync(
                    serverFiles.DownloadUrl,
                    tempMrpackFile,
                    ct).ConfigureAwait(false);

                if (!downloadSuccess)
                {
                    return ModpackDownloadResult.Failed("Failed to download modpack file");
                }

                progress?.Report(new ModpackDownloadProgress
                {
                    Phase = ModpackDownloadPhase.Downloading,
                    Message = "Parsing modpack index...",
                    Percentage = 10
                });

                // Phase 2: Parse and download mods (10-80%)
                var mrpackResult = await ProcessMrpackAsync(
                    tempMrpackFile,
                    destinationPath,
                    progress,
                    ct).ConfigureAwait(false);

                if (!mrpackResult.Success)
                {
                    return ModpackDownloadResult.Failed(mrpackResult.Error ?? "Failed to process modpack");
                }

                // Phase 3: Complete (95-100%)
                progress?.Report(new ModpackDownloadProgress
                {
                    Phase = ModpackDownloadPhase.PostInstall,
                    Message = "Finalizing installation...",
                    Percentage = 95
                });

                // Compute hash of the downloaded mrpack
                var fileHash = await ComputeSha512Async(tempMrpackFile, ct).ConfigureAwait(false);

                progress?.Report(ModpackDownloadProgress.Completed());

                return ModpackDownloadResult.Succeeded(
                    destinationPath,
                    fileHash,
                    ModpackServerFileType.FullPack);
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempMrpackFile))
                {
                    try
                    {
                        File.Delete(tempMrpackFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp file {TempFile}", tempMrpackFile);
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

    private static string MapSortToIndex(ModpackSearchSort sort) => sort switch
    {
        ModpackSearchSort.Popularity => "downloads",
        ModpackSearchSort.RecentlyUpdated => "updated",
        ModpackSearchSort.TotalDownloads => "downloads",
        ModpackSearchSort.Name => "relevance",
        _ => "downloads"
    };

    private static string? MapLoaderToFacet(ModLoaderType loader) => loader switch
    {
        ModLoaderType.Fabric => "fabric",
        ModLoaderType.Forge => "forge",
        ModLoaderType.Quilt => "quilt",
        ModLoaderType.NeoForge => "neoforge",
        _ => null
    };

    private ModpackSearchItem MapToSearchItem(ModrinthSearchHit hit)
    {
        var loaders = new List<ModLoaderType>();
        if (hit.Categories != null)
        {
            loaders = DetectLoaders(hit.Categories);
        }

        var minecraftVersions = hit.Versions?
            .Where(v => MinecraftVersionRegex().IsMatch(v))
            .OrderByDescending(v => v)
            .ToList() ?? [];

        return new ModpackSearchItem
        {
            Id = hit.ProjectId,
            Slug = hit.Slug,
            Name = hit.Title,
            Summary = hit.Description,
            IconUrl = hit.IconUrl,
            Author = hit.Author,
            Downloads = hit.Downloads,
            UpdatedAt = hit.DateModified != null ? ParseDateTime(hit.DateModified) : null,
            MinecraftVersions = minecraftVersions,
            Loaders = loaders,
            Provider = ModpackProviderType.Modrinth,
            HasServerPack = hit.ServerSide != "unsupported"
        };
    }

    private ModpackVersion MapToVersion(ModrinthVersion version)
    {
        var mcVersion = version.GameVersions
            .FirstOrDefault(v => MinecraftVersionRegex().IsMatch(v)) ?? "Unknown";

        var loader = DetectPrimaryLoader(version.Loaders);
        var loaderVersion = GetLoaderVersion(version.Loaders);

        // Check if this version has an .mrpack file (which it should for modpacks)
        var hasMrpack = version.Files.Any(f => f.Filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase));
        var mrpackSize = version.Files
            .FirstOrDefault(f => f.Filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))?.Size;

        return new ModpackVersion
        {
            Id = version.Id,
            Name = version.Name,
            MinecraftVersion = mcVersion,
            Loader = loader,
            LoaderVersion = loaderVersion,
            ReleasedAt = ParseDateTime(version.DatePublished),
            Downloads = version.Downloads,
            Type = MapVersionType(version.VersionType),
            Changelog = version.Changelog,
            HasServerPack = hasMrpack,
            ServerPackSize = mrpackSize
        };
    }

    private static ModpackVersionType MapVersionType(string versionType) => versionType.ToLowerInvariant() switch
    {
        "release" => ModpackVersionType.Release,
        "beta" => ModpackVersionType.Beta,
        "alpha" => ModpackVersionType.Alpha,
        _ => ModpackVersionType.Release
    };

    private static List<ModLoaderType> DetectLoaders(IEnumerable<string> loaders)
    {
        var result = new List<ModLoaderType>();

        foreach (var loader in loaders)
        {
            var loaderType = loader.ToLowerInvariant() switch
            {
                "fabric" => ModLoaderType.Fabric,
                "forge" => ModLoaderType.Forge,
                "quilt" => ModLoaderType.Quilt,
                "neoforge" => ModLoaderType.NeoForge,
                _ => ModLoaderType.None
            };

            if (loaderType != ModLoaderType.None && !result.Contains(loaderType))
            {
                result.Add(loaderType);
            }
        }

        return result;
    }

    private static ModLoaderType DetectPrimaryLoader(string[] loaders)
    {
        foreach (var loader in loaders)
        {
            var loaderType = loader.ToLowerInvariant() switch
            {
                "fabric" => ModLoaderType.Fabric,
                "forge" => ModLoaderType.Forge,
                "quilt" => ModLoaderType.Quilt,
                "neoforge" => ModLoaderType.NeoForge,
                _ => ModLoaderType.None
            };

            if (loaderType != ModLoaderType.None)
            {
                return loaderType;
            }
        }

        return ModLoaderType.None;
    }

    private static string? GetLoaderVersion(string[] loaders)
    {
        // Modrinth loaders array contains loader names, not versions
        // Loader versions are in the MRPACK dependencies
        return null;
    }

    private static List<ModpackLink> BuildLinks(ModrinthProject project)
    {
        var links = new List<ModpackLink>
        {
            new() { Type = "website", Url = $"https://modrinth.com/modpack/{project.Slug}", Name = "Modrinth" }
        };

        if (!string.IsNullOrEmpty(project.WikiUrl))
        {
            links.Add(new ModpackLink { Type = "wiki", Url = project.WikiUrl, Name = "Wiki" });
        }

        if (!string.IsNullOrEmpty(project.SourceUrl))
        {
            links.Add(new ModpackLink { Type = "source", Url = project.SourceUrl, Name = "Source" });
        }

        if (!string.IsNullOrEmpty(project.IssuesUrl))
        {
            links.Add(new ModpackLink { Type = "issues", Url = project.IssuesUrl, Name = "Issues" });
        }

        if (!string.IsNullOrEmpty(project.DiscordUrl))
        {
            links.Add(new ModpackLink { Type = "discord", Url = project.DiscordUrl, Name = "Discord" });
        }

        // Add donation links
        if (project.DonationUrls != null)
        {
            foreach (var donation in project.DonationUrls)
            {
                links.Add(new ModpackLink
                {
                    Type = "donation",
                    Url = donation.Url,
                    Name = donation.Platform
                });
            }
        }

        return links;
    }

    private static DateTime ParseDateTime(string dateString)
    {
        if (DateTime.TryParse(dateString, out var result))
        {
            return result.ToUniversalTime();
        }
        return DateTime.UtcNow;
    }

    private static string? DetectRequiredJavaVersion(string? mcVersion)
    {
        if (string.IsNullOrEmpty(mcVersion))
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

    private static long EstimateRecommendedRam(long fileSize)
    {
        var sizeMb = fileSize / (1024 * 1024);

        return sizeMb switch
        {
            > 500 => 8192, // Large packs (500MB+) -> 8GB
            > 200 => 6144, // Medium-large packs (200-500MB) -> 6GB
            > 100 => 4096, // Medium packs (100-200MB) -> 4GB
            > 50 => 3072,  // Small-medium packs (50-100MB) -> 3GB
            _ => 2048      // Small packs (<50MB) -> 2GB
        };
    }

    private async Task<bool> DownloadFileAsync(string url, string destinationPath, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var fileStream = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    useAsync: true);

                await contentStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
                return true;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(ex, "Download attempt {Attempt}/{MaxRetries} failed, retrying in {Delay}s",
                    attempt, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt == MaxRetries)
            {
                _logger.LogError(ex, "Failed to download file from {Url} after {MaxRetries} attempts", url, MaxRetries);
                return false;
            }
        }

        return false;
    }

    private async Task<(bool Success, string? Error)> ProcessMrpackAsync(
        string mrpackPath,
        string destinationPath,
        IProgress<ModpackDownloadProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            await using var zipStream = new FileStream(mrpackPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            // Find and parse modrinth.index.json
            var indexEntry = archive.GetEntry("modrinth.index.json");
            if (indexEntry == null)
            {
                return (false, "Invalid MRPACK: missing modrinth.index.json");
            }

            MrpackIndex? index;
            await using (var indexStream = indexEntry.Open())
            {
                index = await JsonSerializer.DeserializeAsync<MrpackIndex>(indexStream, cancellationToken: ct)
                    .ConfigureAwait(false);
            }

            if (index == null)
            {
                return (false, "Failed to parse modrinth.index.json");
            }

            // Filter files for server (exclude client-only)
            var serverFiles = index.Files
                .Where(f => f.Env?.Server != "unsupported")
                .ToList();

            _logger.LogInformation(
                "Processing MRPACK {Name} with {TotalFiles} files ({ServerFiles} for server)",
                index.Name, index.Files.Length, serverFiles.Count);

            // Download each mod file (10-80%)
            var totalFiles = serverFiles.Count;
            var downloadedFiles = 0;
            var failedFiles = new List<string>();

            foreach (var file in serverFiles)
            {
                ct.ThrowIfCancellationRequested();

                var filePath = Path.Combine(destinationPath, file.Path.Replace('/', Path.DirectorySeparatorChar));
                var fileDir = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }

                // Try each download URL
                var downloaded = false;
                foreach (var downloadUrl in file.Downloads)
                {
                    try
                    {
                        downloaded = await DownloadFileWithVerificationAsync(
                            downloadUrl,
                            filePath,
                            file.Hashes.Sha512,
                            file.Hashes.Sha1,
                            ct).ConfigureAwait(false);

                        if (downloaded) break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to download {File} from {Url}", file.Path, downloadUrl);
                    }
                }

                if (!downloaded)
                {
                    failedFiles.Add(file.Path);
                    _logger.LogWarning("Failed to download file: {FilePath}", file.Path);
                }

                downloadedFiles++;

                // Report progress (10-80%)
                if (progress != null)
                {
                    var downloadProgress = (double)downloadedFiles / totalFiles;
                    var overallPercentage = 10 + (downloadProgress * 70);

                    progress.Report(new ModpackDownloadProgress
                    {
                        Phase = ModpackDownloadPhase.Downloading,
                        Message = $"Downloading mods... {downloadedFiles}/{totalFiles}",
                        Percentage = overallPercentage,
                        CurrentFile = Path.GetFileName(file.Path)
                    });
                }
            }

            // Extract overrides (80-95%)
            progress?.Report(new ModpackDownloadProgress
            {
                Phase = ModpackDownloadPhase.Extracting,
                Message = "Extracting overrides...",
                Percentage = 80
            });

            await ExtractOverridesAsync(archive, "overrides", destinationPath, ct).ConfigureAwait(false);
            await ExtractOverridesAsync(archive, "server-overrides", destinationPath, ct).ConfigureAwait(false);

            progress?.Report(new ModpackDownloadProgress
            {
                Phase = ModpackDownloadPhase.Extracting,
                Message = "Extraction complete",
                Percentage = 95
            });

            if (failedFiles.Count > 0)
            {
                _logger.LogWarning(
                    "Completed with {FailedCount} failed downloads: {FailedFiles}",
                    failedFiles.Count, string.Join(", ", failedFiles.Take(5)));
            }

            return (true, null);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse modrinth.index.json");
            return (false, "Invalid modrinth.index.json format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MRPACK");
            return (false, $"Error processing modpack: {ex.Message}");
        }
    }

    private async Task<bool> DownloadFileWithVerificationAsync(
        string url,
        string destinationPath,
        string? expectedSha512,
        string? expectedSha1,
        CancellationToken ct)
    {
        var tempPath = destinationPath + ".tmp";

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var fileStream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            await contentStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);

            // Verify hash if provided
            if (!string.IsNullOrEmpty(expectedSha512))
            {
                var actualHash = await ComputeSha512Async(tempPath, ct).ConfigureAwait(false);
                if (!string.Equals(actualHash, expectedSha512, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "SHA512 mismatch for {Path}. Expected: {Expected}, Got: {Actual}",
                        destinationPath, expectedSha512, actualHash);
                    File.Delete(tempPath);
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(expectedSha1))
            {
                var actualHash = await ComputeSha1Async(tempPath, ct).ConfigureAwait(false);
                if (!string.Equals(actualHash, expectedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "SHA1 mismatch for {Path}. Expected: {Expected}, Got: {Actual}",
                        destinationPath, expectedSha1, actualHash);
                    File.Delete(tempPath);
                    return false;
                }
            }

            // Move temp file to final destination
            File.Move(tempPath, destinationPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Download failed for {Url}", url);
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }
            return false;
        }
    }

    private async Task ExtractOverridesAsync(
        ZipArchive archive,
        string overridesFolder,
        string destinationPath,
        CancellationToken ct)
    {
        var prefix = overridesFolder + "/";
        var entries = archive.Entries
            .Where(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                       !string.IsNullOrEmpty(e.Name))
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Extracting {Count} files from {Folder}", entries.Count, overridesFolder);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = entry.FullName[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var targetPath = Path.Combine(destinationPath, relativePath);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            await using var entryStream = entry.Open();
            await using var outputStream = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            await entryStream.CopyToAsync(outputStream, ct).ConfigureAwait(false);
        }
    }

    private static async Task<string> ComputeSha512Async(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var hash = await SHA512.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> ComputeSha1Async(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var hash = await SHA1.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion

    [GeneratedRegex(@"^1\.\d+(\.\d+)?$")]
    private static partial Regex MinecraftVersionRegex();
}
