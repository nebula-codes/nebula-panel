using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.ModProviders.SptForge;

namespace NebulaPanel.Infrastructure.ModProviders;

/// <summary>
/// SPT Forge mod provider for SPT/Tarkov mods (forge.sp-tarkov.com).
/// </summary>
public sealed class SptForgeProvider : IModProvider
{
    private readonly SptForgeApiClient _apiClient;
    private readonly ILogger<SptForgeProvider> _logger;

    public SptForgeProvider(SptForgeApiClient apiClient, ILogger<SptForgeProvider> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public ModProviderType ProviderType => ModProviderType.SptForge;
    public string DisplayName => "SPT Forge";
    public string? IconUrl => "https://forge.sp-tarkov.com/favicon.ico";

    public Task<bool> SupportsGameAsync(string gameSlug, CancellationToken cancellationToken = default)
    {
        var supported = gameSlug.Equals("fika-spt", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(supported);
    }

    public async Task<ModSearchResult> SearchAsync(
        ModSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.SearchModsAsync(
            query: query.Query,
            sptVersion: query.GameVersion,
            page: query.Page,
            perPage: query.PageSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response?.Data is null)
        {
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        var mods = response.Data.Select(mod => new ModSearchItem(
            Id: mod.Id.ToString(),
            Slug: mod.Slug,
            Name: mod.Name,
            Summary: mod.Teaser ?? mod.Description,
            IconUrl: mod.Thumbnail,
            Author: mod.User?.Name,
            Downloads: mod.Downloads,
            UpdatedAt: TryParseDateTime(mod.UpdatedAt),
            Categories: mod.Category is not null ? [mod.Category.Name] : [],
            GameVersions: [],
            Provider: ModProviderType.SptForge
        )).ToList();

        var meta = response.Meta;
        var totalCount = meta?.Total ?? mods.Count;
        var totalPages = meta?.LastPage ?? 1;

        return new ModSearchResult(mods, totalCount, query.Page, query.PageSize, totalPages);
    }

    public async Task<ModDetails?> GetDetailsAsync(
        string modId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetModAsync(modId, cancellationToken).ConfigureAwait(false);
        var mod = response?.Data;

        if (mod is null)
            return null;

        var authors = new List<ModAuthor>();
        if (mod.User is not null)
        {
            authors.Add(new ModAuthor(
                mod.User.Name,
                $"https://forge.sp-tarkov.com/user/{mod.User.Id}"));
        }

        var gameVersions = new List<string>();

        // Collect unique SPT versions from all version entries
        if (mod.Versions is not null)
        {
            foreach (var v in mod.Versions)
            {
                if (v.SptVersion is not null && !gameVersions.Contains(v.SptVersion))
                    gameVersions.Add(v.SptVersion);
            }
        }

        var sourceUrl = mod.SourceCodeLinks?
            .FirstOrDefault(l => l.Url.Contains("github.com", StringComparison.OrdinalIgnoreCase))?.Url;

        // Collect dependencies from the latest version
        var dependencies = new List<ModDependency>();
        var latestVersion = mod.Versions?.FirstOrDefault();
        if (latestVersion?.Dependencies is not null)
        {
            dependencies = MapDependencies(latestVersion.Dependencies);
        }

        return new ModDetails(
            Id: mod.Id.ToString(),
            Slug: mod.Slug,
            Name: mod.Name,
            Summary: mod.Teaser ?? mod.Description,
            Description: mod.Description,
            IconUrl: mod.Thumbnail,
            BannerUrl: null,
            PageUrl: $"https://forge.sp-tarkov.com/mod/{mod.Id}/{mod.Slug}",
            SourceUrl: sourceUrl,
            WikiUrl: null,
            DiscordUrl: null,
            Authors: authors,
            Downloads: mod.Downloads,
            CreatedAt: TryParseDateTime(mod.CreatedAt) ?? DateTime.UtcNow,
            UpdatedAt: TryParseDateTime(mod.UpdatedAt) ?? DateTime.UtcNow,
            Categories: mod.Category is not null ? [mod.Category.Name] : [],
            GameVersions: gameVersions,
            Loaders: ["spt"],
            Screenshots: [],
            Dependencies: dependencies,
            Provider: ModProviderType.SptForge
        );
    }

    public async Task<IReadOnlyList<ModVersion>> GetVersionsAsync(
        string modId,
        string? gameVersion,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetModVersionsAsync(modId, cancellationToken).ConfigureAwait(false);

        // Also get versions from mod details as a secondary source
        var modResponse = await _apiClient.GetModAsync(modId, cancellationToken).ConfigureAwait(false);

        // Merge both sources, deduplicating by version ID
        var allRawVersions = new Dictionary<int, SptForge.SptForgeModVersion>();

        if (response?.Data is not null)
        {
            foreach (var v in response.Data)
                allRawVersions[v.Id] = v;
        }

        if (modResponse?.Data?.Versions is not null)
        {
            foreach (var v in modResponse.Data.Versions)
                allRawVersions.TryAdd(v.Id, v);
        }

        if (allRawVersions.Count == 0)
            return [];

        var versions = allRawVersions.Values
            .Where(v => gameVersion is null
                || v.SptVersion is null
                || v.SptVersion.Contains(gameVersion, StringComparison.OrdinalIgnoreCase))
            .Select(v => new ModVersion(
                Id: v.Id.ToString(),
                Version: v.Version,
                Name: v.Version,
                Changelog: v.Description,
                GameVersions: v.SptVersion is not null ? [v.SptVersion] : [],
                Loaders: ["spt"],
                ReleasedAt: TryParseDateTime(v.CreatedAt) ?? DateTime.UtcNow,
                Downloads: v.Downloads ?? 0,
                VersionType: ModVersionType.Release,
                Dependencies: MapDependencies(v.Dependencies),
                Files: MapFiles(v)
            ))
            .OrderByDescending(v => v.ReleasedAt)
            .ToList();

        return versions;
    }

    public async Task<ModDownloadResult> DownloadAsync(
        string modId,
        string versionId,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get mod details (includes source_code_links for GitHub fallback)
            var modResponse = await _apiClient.GetModAsync(modId, cancellationToken).ConfigureAwait(false);

            // Get version details to find the download URL
            var versionsResponse = await _apiClient.GetModVersionsAsync(modId, cancellationToken).ConfigureAwait(false);
            var allVersions = versionsResponse?.Data;
            var version = allVersions?.FirstOrDefault(v => v.Id.ToString() == versionId);

            // Fallback: check versions embedded in mod details (in case paginated endpoint missed it)
            version ??= modResponse?.Data?.Versions?.FirstOrDefault(v => v.Id.ToString() == versionId);

            if (version is null)
                return new ModDownloadResult(false, null, $"Version {versionId} not found", null);

            Directory.CreateDirectory(destinationPath);

            // Determine file extension from the download URL (fallback to .zip)
            var ext = GetArchiveExtension(version.DownloadUrl);
            var fileName = $"{modId}-{version.Version}{ext}";
            var filePath = Path.Combine(destinationPath, fileName);

            // Check if the download URL points to a different GitHub repo than source_code_links.
            // Some SPT Forge entries have stale/wrong download URLs pointing to the wrong repo
            // (e.g. a client-side lib instead of the server-side mod).
            var githubSourceUrl = modResponse?.Data?.SourceCodeLinks?
                .FirstOrDefault(l => l.Url.Contains("github.com", StringComparison.OrdinalIgnoreCase))?.Url;

            if (!string.IsNullOrEmpty(version.DownloadUrl)
                && !string.IsNullOrEmpty(githubSourceUrl)
                && IsGitHubRepoMismatch(version.DownloadUrl, githubSourceUrl))
            {
                _logger.LogWarning(
                    "Download URL for mod {ModId} points to a different GitHub repo than source_code_links. " +
                    "Preferring source_code_links repo: {SourceUrl}",
                    modId, githubSourceUrl);

                var ghResult = await TryDownloadFromGitHubReleasesAsync(
                    githubSourceUrl, version.Version, filePath, progress, cancellationToken).ConfigureAwait(false);

                if (ghResult.Success)
                {
                    _logger.LogInformation(
                        "Downloaded SPT Forge mod {ModId} version {Version} from canonical GitHub repo to {Path}",
                        modId, version.Version, ghResult.FilePath);
                    return ghResult;
                }

                _logger.LogWarning(
                    "Canonical GitHub repo download failed for mod {ModId}, falling back to stored download URL",
                    modId);
            }

            // Try the link field first
            if (!string.IsNullOrEmpty(version.DownloadUrl))
            {
                _logger.LogInformation("SPT Forge download URL for mod {ModId} v{Version}: {Url}",
                    modId, version.Version, version.DownloadUrl);

                var (success, error) = await _apiClient.DownloadFileAsync(
                    version.DownloadUrl,
                    filePath,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                if (success)
                {
                    _logger.LogInformation("Downloaded SPT Forge mod {ModId} version {Version} to {Path}",
                        modId, version.Version, filePath);
                    return new ModDownloadResult(true, filePath, null, null);
                }

                _logger.LogWarning(
                    "Direct download failed for mod {ModId}: {Error}. Trying fallbacks.",
                    modId, error);
            }

            // Fallback 1: try other versions' download links (newer versions may have working URLs)
            if (allVersions is not null)
            {
                var otherVersions = allVersions
                    .Where(v => v.Id.ToString() != versionId && !string.IsNullOrEmpty(v.DownloadUrl))
                    .ToList();

                foreach (var altVersion in otherVersions)
                {
                    _logger.LogInformation(
                        "Trying alternate version download URL for mod {ModId}: v{Version} at {Url}",
                        modId, altVersion.Version, altVersion.DownloadUrl);

                    var altExt = GetArchiveExtension(altVersion.DownloadUrl);
                    var altFilePath = Path.Combine(destinationPath, $"{modId}-{altVersion.Version}{altExt}");

                    var (altSuccess, altError) = await _apiClient.DownloadFileAsync(
                        altVersion.DownloadUrl!,
                        altFilePath,
                        progress,
                        cancellationToken).ConfigureAwait(false);

                    if (altSuccess)
                    {
                        _logger.LogInformation(
                            "Downloaded SPT Forge mod {ModId} via alternate version {Version} to {Path}",
                            modId, altVersion.Version, altFilePath);
                        return new ModDownloadResult(true, altFilePath, null, null);
                    }
                }
            }

            // Fallback 2: try to download from GitHub Releases via source_code_links
            // (skip if we already tried the canonical repo above due to mismatch)
            var alreadyTriedCanonicalGitHub = !string.IsNullOrEmpty(version.DownloadUrl)
                && !string.IsNullOrEmpty(githubSourceUrl)
                && IsGitHubRepoMismatch(version.DownloadUrl, githubSourceUrl);

            if (githubSourceUrl is not null && !alreadyTriedCanonicalGitHub)
            {
                var ghResult = await TryDownloadFromGitHubReleasesAsync(
                    githubSourceUrl, version.Version, filePath, progress, cancellationToken).ConfigureAwait(false);

                if (ghResult.Success)
                {
                    _logger.LogInformation(
                        "Downloaded SPT Forge mod {ModId} version {Version} from GitHub Releases to {Path}",
                        modId, version.Version, filePath);
                    return ghResult;
                }

                _logger.LogWarning("GitHub Releases fallback also failed for mod {ModId}: {Error}",
                    modId, ghResult.Error);
            }

            // Fallback 3: try probing the download URL with the latest version from the mod page
            // Handles stale links where the CDN file was updated but SPT Forge wasn't
            if (!string.IsNullOrEmpty(version.DownloadUrl) && modResponse?.Data?.Versions is not null)
            {
                var probeResult = await TryProbeVersionedUrlAsync(
                    version.DownloadUrl, modResponse.Data.Versions, destinationPath, modId,
                    progress, cancellationToken).ConfigureAwait(false);

                if (probeResult is not null)
                    return probeResult;
            }

            return new ModDownloadResult(false, null,
                "Could not download mod: the download link is invalid and no GitHub release was found.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download SPT Forge mod {ModId} version {VersionId}", modId, versionId);
            return new ModDownloadResult(false, null, $"Download failed: {ex.Message}", null);
        }
    }

    /// <summary>
    /// When a download URL contains a version number in the filename (e.g., ModName-1.1.0.7z),
    /// tries replacing it with version strings from other known versions to find a working URL.
    /// This handles the common case where mod authors update their CDN files without updating SPT Forge.
    /// </summary>
    private async Task<ModDownloadResult?> TryProbeVersionedUrlAsync(
        string originalUrl,
        SptForgeModVersion[] knownVersions,
        string destinationPath,
        string modId,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find the version string in the URL filename
            // e.g., https://downloads.example.com/mods/WTT-Armory-1.1.0.7z → "1.1.0"
            var versionPattern = System.Text.RegularExpressions.Regex.Match(
                originalUrl, @"[\-_](\d+\.\d+(?:\.\d+)*(?:\.\d+)*)(\.[a-zA-Z0-9]+)$");

            if (!versionPattern.Success)
                return null;

            var oldVersion = versionPattern.Groups[1].Value;
            var fileExtension = versionPattern.Groups[2].Value;

            // Collect candidate versions to try (from all known versions, newest first)
            var candidateVersions = knownVersions
                .Where(v => v.Version != oldVersion)
                .Select(v => v.Version)
                .Distinct()
                .ToList();

            if (candidateVersions.Count == 0)
                return null;

            _logger.LogInformation(
                "Probing URL version replacement for mod {ModId}: base version {OldVersion}, trying {Count} alternatives",
                modId, oldVersion, candidateVersions.Count);

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "NebulaPanel");
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            foreach (var candidateVersion in candidateVersions)
            {
                var probeUrl = originalUrl.Replace(
                    $"{oldVersion}{fileExtension}",
                    $"{candidateVersion}{fileExtension}");

                try
                {
                    using var headResponse = await httpClient.SendAsync(
                        new HttpRequestMessage(HttpMethod.Head, probeUrl),
                        cancellationToken).ConfigureAwait(false);

                    if (!headResponse.IsSuccessStatusCode)
                        continue;

                    _logger.LogInformation(
                        "Found working URL at version {Version}: {Url}", candidateVersion, probeUrl);

                    var ext = GetArchiveExtension(probeUrl);
                    var filePath = Path.Combine(destinationPath, $"{modId}-{candidateVersion}{ext}");

                    var (success, error) = await _apiClient.DownloadFileAsync(
                        probeUrl, filePath, progress, cancellationToken).ConfigureAwait(false);

                    if (success)
                        return new ModDownloadResult(true, filePath, null, null);
                }
                catch
                {
                    // Probe failed, try next
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "URL version probing failed for mod {ModId}", modId);
        }

        return null;
    }

    private async Task<ModDownloadResult> TryDownloadFromGitHubReleasesAsync(
        string githubUrl,
        string modVersion,
        string filePath,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract owner/repo from GitHub URL
            // e.g., https://github.com/user/repo or https://github.com/user/repo/...
            var match = System.Text.RegularExpressions.Regex.Match(
                githubUrl, @"github\.com/([^/]+)/([^/]+)");

            if (!match.Success)
                return new ModDownloadResult(false, null, "Could not parse GitHub repository URL", null);

            var owner = match.Groups[1].Value;
            var repo = match.Groups[2].Value.TrimEnd('/');

            // Try to find a release matching the version
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";
            _logger.LogInformation("Checking GitHub Releases at {Url} for version {Version}", apiUrl, modVersion);

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "NebulaPanel");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            var releases = await httpClient
                .GetFromJsonAsync<System.Text.Json.JsonElement[]>(apiUrl, cancellationToken)
                .ConfigureAwait(false);

            if (releases is null || releases.Length == 0)
                return new ModDownloadResult(false, null, "No GitHub releases found", null);

            // Find the release matching the mod version, or use the latest
            System.Text.Json.JsonElement? targetRelease = null;
            foreach (var release in releases)
            {
                var tagName = release.GetProperty("tag_name").GetString() ?? "";
                if (tagName.Contains(modVersion, StringComparison.OrdinalIgnoreCase) ||
                    tagName.TrimStart('v').Equals(modVersion, StringComparison.OrdinalIgnoreCase))
                {
                    targetRelease = release;
                    break;
                }
            }

            // Fall back to latest release
            targetRelease ??= releases[0];

            var assets = targetRelease.Value.GetProperty("assets");
            if (assets.GetArrayLength() == 0)
                return new ModDownloadResult(false, null, "GitHub release has no downloadable assets", null);

            // Find the best asset (prefer .zip, .7z, etc.)
            string? downloadUrl = null;
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                var url = asset.GetProperty("browser_download_url").GetString();

                if (url is null) continue;

                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = url;
                    break;
                }

                // Use first asset as fallback
                downloadUrl ??= url;
            }

            if (downloadUrl is null)
                return new ModDownloadResult(false, null, "No suitable download asset found in GitHub release", null);

            // Update file path extension to match the actual asset
            var ghExt = GetArchiveExtension(downloadUrl);
            var currentExt = Path.GetExtension(filePath);
            if (!ghExt.Equals(currentExt, StringComparison.OrdinalIgnoreCase))
            {
                filePath = Path.ChangeExtension(filePath, ghExt);
            }

            _logger.LogInformation("Downloading from GitHub Releases: {Url}", downloadUrl);

            var (success, error) = await _apiClient.DownloadFileAsync(
                downloadUrl, filePath, progress, cancellationToken).ConfigureAwait(false);

            return success
                ? new ModDownloadResult(true, filePath, null, null)
                : new ModDownloadResult(false, null, error, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub Releases fallback failed for {Url}", githubUrl);
            return new ModDownloadResult(false, null, $"GitHub fallback failed: {ex.Message}", null);
        }
    }

    public async Task<IReadOnlyList<ModUpdateInfo>> CheckUpdatesAsync(
        IEnumerable<InstalledModInfo> installedMods,
        string? gameVersion,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<ModUpdateInfo>();
        var modsToCheck = installedMods
            .Where(m => m.Provider == ModProviderType.SptForge)
            .ToList();

        foreach (var mod in modsToCheck)
        {
            try
            {
                var versions = await GetVersionsAsync(mod.ModId, gameVersion, cancellationToken)
                    .ConfigureAwait(false);

                if (versions.Count == 0)
                    continue;

                var latestVersion = versions[0];

                if (!mod.VersionId.Equals(latestVersion.Id, StringComparison.OrdinalIgnoreCase))
                {
                    var details = await GetDetailsAsync(mod.ModId, cancellationToken).ConfigureAwait(false);
                    var installedVersion = versions.FirstOrDefault(v => v.Id == mod.VersionId);

                    updates.Add(new ModUpdateInfo(
                        InstalledModId: Guid.Empty,
                        ModName: details?.Name ?? mod.ModId,
                        CurrentVersion: installedVersion?.Version ?? mod.VersionId,
                        LatestVersion: latestVersion.Version,
                        LatestVersionId: latestVersion.Id,
                        ReleasedAt: latestVersion.ReleasedAt,
                        Changelog: latestVersion.Changelog,
                        Provider: ModProviderType.SptForge
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check updates for SPT Forge mod {ModId}", mod.ModId);
            }
        }

        return updates;
    }

    private static List<ModDependency> MapDependencies(SptForgeDependency[]? dependencies)
    {
        if (dependencies is null or { Length: 0 })
            return [];

        return dependencies.Select(d => new ModDependency(
            ModId: d.ModId.ToString(),
            ModName: d.ModName,
            VersionId: d.VersionId?.ToString(),
            Type: d.Type.ToLowerInvariant() switch
            {
                "required" => ModDependencyType.Required,
                "optional" => ModDependencyType.Optional,
                "incompatible" => ModDependencyType.Incompatible,
                _ => ModDependencyType.Optional
            }
        )).ToList();
    }

    private static List<ModFile> MapFiles(SptForgeModVersion version)
    {
        if (string.IsNullOrEmpty(version.DownloadUrl))
            return [];

        return
        [
            new ModFile(
                FileName: $"mod-{version.ModId}-{version.Version}.zip",
                Url: version.DownloadUrl,
                SizeBytes: version.FileSize ?? 0,
                Sha512: null,
                Sha1: null,
                IsPrimary: true
            )
        ];
    }

    private static DateTime? TryParseDateTime(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        return DateTime.TryParse(dateString, out var result)
            ? result.ToUniversalTime()
            : null;
    }

    private static string GetArchiveExtension(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return ".zip";

        // Strip query string before checking extension
        var path = url.Split('?')[0].Split('#')[0];
        var ext = Path.GetExtension(path).ToLowerInvariant();

        return ext is ".zip" or ".7z" or ".rar" ? ext : ".zip";
    }

    /// <summary>
    /// Checks if a download URL points to a different GitHub repo than the source_code_links URL.
    /// This detects cases where SPT Forge has a stale/wrong download link (e.g. pointing to
    /// a client-side library instead of the actual server mod).
    /// </summary>
    private static bool IsGitHubRepoMismatch(string downloadUrl, string sourceCodeUrl)
    {
        var downloadRepo = ExtractGitHubOwnerRepo(downloadUrl);
        var sourceRepo = ExtractGitHubOwnerRepo(sourceCodeUrl);

        if (downloadRepo is null || sourceRepo is null)
            return false;

        return !downloadRepo.Equals(sourceRepo, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractGitHubOwnerRepo(string url)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            url, @"github\.com/([^/]+/[^/]+)");
        return match.Success ? match.Groups[1].Value.TrimEnd('/') : null;
    }
}
