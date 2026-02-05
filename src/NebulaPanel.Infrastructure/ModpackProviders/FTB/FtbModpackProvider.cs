using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.ModpackProviders.FTB;

/// <summary>
/// Modpack provider implementation for Feed The Beast (FTB) - a curated modpack platform.
/// </summary>
public sealed partial class FtbModpackProvider : IModpackProvider
{
    private readonly FtbApiClient _apiClient;
    private readonly ILogger<FtbModpackProvider> _logger;

    public FtbModpackProvider(FtbApiClient apiClient, ILogger<FtbModpackProvider> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public ModpackProviderType ProviderType => ModpackProviderType.FTB;

    /// <inheritdoc />
    public string DisplayName => "Feed The Beast";

    /// <inheritdoc />
    public string? IconUrl => "https://www.feed-the-beast.com/favicon.ico";

    /// <inheritdoc />
    public async Task<ModpackSearchResult> SearchAsync(ModpackSearchQuery query, CancellationToken ct = default)
    {
        // FTB search is simpler - just uses term parameter
        // We'll fetch more results and filter client-side for MC version/loader
        var fetchLimit = Math.Max(query.PageSize * 3, 100); // Fetch extra for filtering

        var response = await _apiClient.SearchAsync(query.Query, fetchLimit, ct).ConfigureAwait(false);

        if (response == null || response.Packs.Length == 0)
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

        // Map to search items
        var allItems = response.Packs
            .Where(p => p.Status == "released") // Only show released packs
            .Select(MapToSearchItem)
            .ToList();

        // Apply client-side filters
        var filteredItems = allItems
            .Where(item => MatchesFilters(item, query.MinecraftVersion, query.LoaderType))
            .ToList();

        // Apply sorting
        filteredItems = ApplySort(filteredItems, query.Sort);

        // Apply pagination
        var totalCount = filteredItems.Count;
        var pagedItems = filteredItems
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new ModpackSearchResult
        {
            Modpacks = pagedItems,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / query.PageSize)
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModpackSearchItem>> GetFeaturedAsync(int count = 20, CancellationToken ct = default)
    {
        var response = await _apiClient.GetPopularAsync(count, ct).ConfigureAwait(false);

        if (response == null || response.Packs.Length == 0)
        {
            return [];
        }

        return response.Packs
            .Where(p => p.Status == "released")
            .Select(MapToSearchItem)
            .Take(count)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ModpackDetails> GetDetailsAsync(string modpackId, CancellationToken ct = default)
    {
        if (!int.TryParse(modpackId, out var packId))
        {
            throw new ArgumentException($"Invalid FTB modpack ID: {modpackId}", nameof(modpackId));
        }

        var pack = await _apiClient.GetModpackAsync(packId, ct).ConfigureAwait(false);

        if (pack == null)
        {
            throw new InvalidOperationException($"Modpack with ID {modpackId} not found on FTB");
        }

        // Get icon and banner from art
        var icon = pack.Art.FirstOrDefault(a => a.Type == "square");
        var banner = pack.Art.FirstOrDefault(a => a.Type == "splash");

        // Get authors
        var authors = pack.Authors
            .Select(a => new ModpackAuthor
            {
                Name = a.Name,
                Url = a.Website
            })
            .ToList();

        // Get MC versions and loaders from latest versions
        var mcVersions = new HashSet<string>();
        var loaders = new HashSet<ModLoaderType>();

        foreach (var version in pack.Versions.Take(10)) // Check last 10 versions
        {
            var mcTarget = version.Targets.FirstOrDefault(t =>
                t.Type.Equals("game", StringComparison.OrdinalIgnoreCase) &&
                t.Name.Equals("minecraft", StringComparison.OrdinalIgnoreCase));

            if (mcTarget != null && MinecraftVersionRegex().IsMatch(mcTarget.Version))
            {
                mcVersions.Add(mcTarget.Version);
            }

            var loader = DetectLoader(version.Targets);
            if (loader != ModLoaderType.None)
            {
                loaders.Add(loader);
            }
        }

        // Get screenshots from art
        var screenshots = pack.Art
            .Where(a => a.Type == "splash" || a.Type == "logo")
            .Select(a => new ModpackScreenshot
            {
                Url = a.Url,
                Title = a.Type
            })
            .ToList();

        // Build links
        var links = new List<ModpackLink>
        {
            new() { Type = "website", Url = $"https://www.feed-the-beast.com/modpacks/{packId}", Name = "FTB" }
        };

        if (pack.Links != null)
        {
            foreach (var link in pack.Links)
            {
                links.Add(new ModpackLink
                {
                    Type = link.Type.ToLowerInvariant(),
                    Url = link.Link,
                    Name = link.Name
                });
            }
        }

        // Get categories from tags
        var categories = pack.Tags?
            .Select(t => t.Name)
            .ToList() ?? [];

        return new ModpackDetails
        {
            Id = pack.Id.ToString(),
            Slug = pack.Name.ToLowerInvariant().Replace(' ', '-'),
            Name = pack.Name,
            Summary = pack.Synopsis,
            Description = pack.Description,
            IconUrl = icon?.Url,
            BannerUrl = banner?.Url,
            Authors = authors,
            Downloads = pack.Installs,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(pack.Released).UtcDateTime,
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(pack.Updated).UtcDateTime,
            Categories = categories,
            MinecraftVersions = mcVersions.OrderByDescending(v => v).ToList(),
            Loaders = loaders.ToList(),
            Screenshots = screenshots,
            Links = links,
            Provider = ModpackProviderType.FTB,
            ModCount = 0, // FTB doesn't provide this in pack details
            HasServerPack = true // FTB packs typically have server support
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModpackVersion>> GetVersionsAsync(string modpackId, CancellationToken ct = default)
    {
        if (!int.TryParse(modpackId, out var packId))
        {
            throw new ArgumentException($"Invalid FTB modpack ID: {modpackId}", nameof(modpackId));
        }

        var pack = await _apiClient.GetModpackAsync(packId, ct).ConfigureAwait(false);

        if (pack == null)
        {
            return [];
        }

        return pack.Versions
            .OrderByDescending(v => v.Updated)
            .Select(v => MapToVersion(v, packId))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ModpackServerFiles?> GetServerFilesAsync(string modpackId, string versionId, CancellationToken ct = default)
    {
        if (!int.TryParse(modpackId, out var packId) || !int.TryParse(versionId, out var verionIdInt))
        {
            _logger.LogWarning("Invalid FTB modpack ID {ModpackId} or version ID {VersionId}", modpackId, versionId);
            return null;
        }

        // Determine platform
        var platform = OperatingSystem.IsWindows() ? "windows" : "linux";

        var serverFiles = await _apiClient.GetServerFilesAsync(packId, verionIdInt, platform, ct).ConfigureAwait(false);

        if (serverFiles?.Files == null || serverFiles.Files.Length == 0)
        {
            _logger.LogDebug("No server files available for FTB pack {PackId} version {VersionId}", packId, versionId);
            return null;
        }

        // Find the server installer file
        var installerFile = serverFiles.Files.FirstOrDefault(f =>
            f.Name.Contains("serverinstall", StringComparison.OrdinalIgnoreCase) ||
            f.Type.Equals("install", StringComparison.OrdinalIgnoreCase));

        if (installerFile == null)
        {
            // Fall back to first file
            installerFile = serverFiles.Files[0];
        }

        // Get MC version and estimate requirements
        var mcTarget = serverFiles.Targets.FirstOrDefault(t =>
            t.Type.Equals("game", StringComparison.OrdinalIgnoreCase) &&
            t.Name.Equals("minecraft", StringComparison.OrdinalIgnoreCase));

        var javaVersion = DetectRequiredJavaVersion(mcTarget?.Version);
        var recommendedRam = serverFiles.Specs?.Recommended ?? 4096;

        return new ModpackServerFiles
        {
            DownloadUrl = installerFile.Url,
            SizeBytes = installerFile.Size,
            Sha256 = null, // FTB uses SHA1
            FileType = ModpackServerFileType.InstallerJar, // FTB uses installer scripts
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
            if (!int.TryParse(modpackId, out var packId) || !int.TryParse(versionId, out var versionIdInt))
            {
                return ModpackDownloadResult.Failed($"Invalid FTB modpack ID: {modpackId} or version ID: {versionId}");
            }

            Directory.CreateDirectory(destinationPath);

            // Get server files info
            var platform = OperatingSystem.IsWindows() ? "windows" : "linux";
            var serverFiles = await _apiClient.GetServerFilesAsync(packId, versionIdInt, platform, ct).ConfigureAwait(false);

            if (serverFiles?.Files == null || serverFiles.Files.Length == 0)
            {
                return ModpackDownloadResult.Failed("No server files available for this modpack version");
            }

            // Report starting
            progress?.Report(new ModpackDownloadProgress
            {
                Phase = ModpackDownloadPhase.Starting,
                Message = "Starting FTB server installation...",
                Percentage = 0
            });

            // Download all server files (0-40%)
            var downloadedFiles = 0;
            var totalFiles = serverFiles.Files.Length;
            var serverOnlyFiles = serverFiles.Files
                .Where(f => !f.ClientOnly)
                .ToList();

            _logger.LogInformation(
                "Downloading {FileCount} server files for FTB pack {PackId} version {VersionId}",
                serverOnlyFiles.Count, packId, versionIdInt);

            foreach (var file in serverOnlyFiles)
            {
                ct.ThrowIfCancellationRequested();

                var filePath = Path.Combine(destinationPath, file.Path.Replace('/', Path.DirectorySeparatorChar), file.Name);
                var fileDir = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }

                // Report progress
                progress?.Report(new ModpackDownloadProgress
                {
                    Phase = ModpackDownloadPhase.Downloading,
                    Message = $"Downloading {file.Name}...",
                    Percentage = (double)downloadedFiles / serverOnlyFiles.Count * 40,
                    CurrentFile = file.Name
                });

                var downloaded = await _apiClient.DownloadFileToPathAsync(file.Url, filePath, null, ct).ConfigureAwait(false);

                if (!downloaded)
                {
                    _logger.LogWarning("Failed to download file: {FilePath}", file.Path);
                }

                downloadedFiles++;
            }

            // Post-install: Write EULA (90%)
            progress?.Report(new ModpackDownloadProgress
            {
                Phase = ModpackDownloadPhase.PostInstall,
                Message = "Accepting EULA...",
                Percentage = 90
            });

            var eulaPath = Path.Combine(destinationPath, "eula.txt");
            await File.WriteAllTextAsync(eulaPath, "eula=true", ct).ConfigureAwait(false);

            // Create start script if needed (95%)
            progress?.Report(new ModpackDownloadProgress
            {
                Phase = ModpackDownloadPhase.PostInstall,
                Message = "Finalizing installation...",
                Percentage = 95
            });

            // Check for existing start scripts, create one if missing
            await EnsureStartScriptAsync(destinationPath, serverFiles, ct).ConfigureAwait(false);

            // Complete (100%)
            progress?.Report(ModpackDownloadProgress.Completed());

            _logger.LogInformation(
                "Successfully installed FTB server pack {PackId} version {VersionId} to {Path}",
                packId, versionIdInt, destinationPath);

            return ModpackDownloadResult.Succeeded(destinationPath, null, ModpackServerFileType.ServerPack);
        }
        catch (OperationCanceledException)
        {
            return ModpackDownloadResult.Failed("Download was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download FTB server pack {ModpackId} version {VersionId}", modpackId, versionId);
            return ModpackDownloadResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    #region Private Helper Methods

    private ModpackSearchItem MapToSearchItem(FtbPackSummary pack)
    {
        // Get MC versions and loaders from versions
        var mcVersions = new List<string>();
        var loaders = new List<ModLoaderType>();

        if (pack.Versions != null && pack.Versions.Length > 0)
        {
            // We only have summary info here, so we'll mark as having server pack
            // and let details fetch provide accurate version info
        }

        // Get icon from art
        var iconUrl = pack.Art
            .FirstOrDefault(a => a.Type == "square")?.Url ??
            pack.Art.FirstOrDefault()?.Url;

        // Get primary author
        var author = pack.Authors.FirstOrDefault()?.Name ?? "FTB Team";

        return new ModpackSearchItem
        {
            Id = pack.Id.ToString(),
            Slug = pack.Name.ToLowerInvariant().Replace(' ', '-'),
            Name = pack.Name,
            Summary = pack.Synopsis,
            IconUrl = iconUrl,
            Author = author,
            Downloads = pack.Installs,
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(pack.Updated).UtcDateTime,
            MinecraftVersions = mcVersions,
            Loaders = loaders,
            Provider = ModpackProviderType.FTB,
            HasServerPack = true // FTB packs typically have server support
        };
    }

    private ModpackVersion MapToVersion(FtbPackVersionInfo version, int packId)
    {
        // Get MC version from targets
        var mcTarget = version.Targets.FirstOrDefault(t =>
            t.Type.Equals("game", StringComparison.OrdinalIgnoreCase) &&
            t.Name.Equals("minecraft", StringComparison.OrdinalIgnoreCase));

        var mcVersion = mcTarget?.Version ?? "Unknown";

        // Detect loader
        var loader = DetectLoader(version.Targets);
        var loaderVersion = GetLoaderVersion(version.Targets);

        // Map version type
        var versionType = version.Type.ToLowerInvariant() switch
        {
            "release" => ModpackVersionType.Release,
            "beta" => ModpackVersionType.Beta,
            "alpha" => ModpackVersionType.Alpha,
            _ => ModpackVersionType.Release
        };

        return new ModpackVersion
        {
            Id = version.Id.ToString(),
            Name = version.Name,
            MinecraftVersion = mcVersion,
            Loader = loader,
            LoaderVersion = loaderVersion,
            ReleasedAt = DateTimeOffset.FromUnixTimeSeconds(version.Updated).UtcDateTime,
            Downloads = 0, // FTB doesn't provide per-version download counts
            Type = versionType,
            Changelog = null, // FTB doesn't include changelog in version info
            HasServerPack = true,
            ServerPackSize = null
        };
    }

    private static ModLoaderType DetectLoader(FtbTarget[] targets)
    {
        var loaderTarget = targets.FirstOrDefault(t =>
            t.Type.Equals("modloader", StringComparison.OrdinalIgnoreCase));

        if (loaderTarget == null)
        {
            return ModLoaderType.None;
        }

        return loaderTarget.Name.ToLowerInvariant() switch
        {
            "forge" => ModLoaderType.Forge,
            "neoforge" => ModLoaderType.NeoForge,
            "fabric" => ModLoaderType.Fabric,
            "quilt" => ModLoaderType.Quilt,
            _ => ModLoaderType.None
        };
    }

    private static string? GetLoaderVersion(FtbTarget[] targets)
    {
        var loaderTarget = targets.FirstOrDefault(t =>
            t.Type.Equals("modloader", StringComparison.OrdinalIgnoreCase));

        return loaderTarget?.Version;
    }

    private static bool MatchesFilters(ModpackSearchItem item, string? minecraftVersion, ModLoaderType? loaderType)
    {
        // If no filters, everything matches
        if (string.IsNullOrEmpty(minecraftVersion) && (!loaderType.HasValue || loaderType.Value == ModLoaderType.None))
        {
            return true;
        }

        // Check Minecraft version
        if (!string.IsNullOrEmpty(minecraftVersion))
        {
            if (!item.MinecraftVersions.Contains(minecraftVersion))
            {
                // FTB search results don't always include version info
                // So we allow items without version info through the filter
                // They'll be filtered more accurately when details are fetched
                if (item.MinecraftVersions.Count > 0)
                {
                    return false;
                }
            }
        }

        // Check loader type
        if (loaderType.HasValue && loaderType.Value != ModLoaderType.None)
        {
            if (!item.Loaders.Contains(loaderType.Value))
            {
                // Same as above - allow items without loader info through
                if (item.Loaders.Count > 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static List<ModpackSearchItem> ApplySort(List<ModpackSearchItem> items, ModpackSearchSort sort)
    {
        return sort switch
        {
            ModpackSearchSort.Popularity => items.OrderByDescending(i => i.Downloads).ToList(),
            ModpackSearchSort.RecentlyUpdated => items.OrderByDescending(i => i.UpdatedAt).ToList(),
            ModpackSearchSort.TotalDownloads => items.OrderByDescending(i => i.Downloads).ToList(),
            ModpackSearchSort.Name => items.OrderBy(i => i.Name).ToList(),
            _ => items
        };
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
            if (minor >= 21)
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

    private async Task EnsureStartScriptAsync(
        string destinationPath,
        FtbServerFilesResponse serverFiles,
        CancellationToken ct)
    {
        // Check for existing start scripts
        var existingScripts = new[]
        {
            "start.sh", "start.bat", "ServerStart.sh", "ServerStart.bat",
            "run.sh", "run.bat", "start-server.sh", "start-server.bat"
        };

        foreach (var script in existingScripts)
        {
            if (File.Exists(Path.Combine(destinationPath, script)))
            {
                _logger.LogDebug("Found existing start script: {Script}", script);
                return; // Start script already exists
            }
        }

        // Get recommended RAM
        var ramMb = serverFiles.Specs?.Recommended ?? 4096;

        // Create start script
        if (OperatingSystem.IsWindows())
        {
            var batPath = Path.Combine(destinationPath, "start.bat");
            var batContent = $"""
                @echo off
                java -Xms{ramMb}M -Xmx{ramMb}M -jar server.jar nogui
                pause
                """;
            await File.WriteAllTextAsync(batPath, batContent, ct).ConfigureAwait(false);
        }
        else
        {
            var shPath = Path.Combine(destinationPath, "start.sh");
            var shContent = $"""
                #!/bin/bash
                java -Xms{ramMb}M -Xmx{ramMb}M -jar server.jar nogui
                """;
            await File.WriteAllTextAsync(shPath, shContent, ct).ConfigureAwait(false);

            // Make executable on Linux
            try
            {
                var chmod = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{shPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(chmod);
                if (process != null)
                {
                    await process.WaitForExitAsync(ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to make start script executable");
            }
        }
    }

    #endregion

    [GeneratedRegex(@"^1\.\d+(\.\d+)?$")]
    private static partial Regex MinecraftVersionRegex();
}
