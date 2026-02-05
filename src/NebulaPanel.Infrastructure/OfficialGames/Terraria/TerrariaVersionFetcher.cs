using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Terraria;

/// <summary>
/// Fetches available tModLoader versions from GitHub releases.
/// </summary>
public class TerrariaVersionFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TerrariaVersionFetcher> _logger;

    private const string GitHubReleasesUrl = "https://api.github.com/repos/tModLoader/tModLoader/releases";
    private const string CacheKey = "terraria-tmodloader-versions";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public TerrariaVersionFetcher(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<TerrariaVersionFetcher> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Terraria");
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Fetches available tModLoader versions from GitHub and Steam.
    /// </summary>
    public async Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<IReadOnlyList<GameVersionInfo>>(CacheKey, out var cachedVersions) && cachedVersions is not null)
        {
            _logger.LogDebug("Returning cached tModLoader versions ({Count} versions)", cachedVersions.Count);
            return cachedVersions;
        }

        var versions = new List<GameVersionInfo>();

        // Always add "latest" Steam version as the recommended option
        versions.Add(new GameVersionInfo
        {
            Version = "latest",
            DisplayName = "Latest (Steam)",
            VersionType = GameVersionType.Release,
            IsRecommended = true,
            ReleaseDate = DateTime.UtcNow
        });

        try
        {
            // Fetch GitHub releases for specific version history
            var releases = await FetchGitHubReleasesAsync(cancellationToken).ConfigureAwait(false);
            versions.AddRange(releases);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch GitHub releases for tModLoader, using Steam-only versions");
        }

        // Cache the results
        _cache.Set(CacheKey, (IReadOnlyList<GameVersionInfo>)versions, CacheDuration);
        _logger.LogInformation("Fetched {Count} tModLoader versions", versions.Count);

        return versions;
    }

    private async Task<IEnumerable<GameVersionInfo>> FetchGitHubReleasesAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching tModLoader releases from GitHub");

        var request = new HttpRequestMessage(HttpMethod.Get, GitHubReleasesUrl);
        request.Headers.Add("Accept", "application/vnd.github.v3+json");
        request.Headers.Add("User-Agent", "NebulaPanel/1.0");

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var releases = await response.Content.ReadFromJsonAsync<List<GitHubRelease>>(cancellationToken).ConfigureAwait(false);

        if (releases is null)
        {
            _logger.LogWarning("No releases returned from GitHub");
            return [];
        }

        return releases
            .Where(r => !string.IsNullOrEmpty(r.TagName))
            .Select(r => new GameVersionInfo
            {
                Version = r.TagName!.TrimStart('v'),
                DisplayName = FormatDisplayName(r),
                ReleaseDate = r.PublishedAt,
                VersionType = DetermineVersionType(r),
                IsRecommended = false // Steam "latest" is recommended
            })
            .Take(50); // Limit to last 50 versions
    }

    private static string FormatDisplayName(GitHubRelease release)
    {
        var version = release.TagName?.TrimStart('v') ?? "Unknown";

        if (release.Prerelease)
        {
            return $"{version} (Preview)";
        }

        return version;
    }

    private static GameVersionType DetermineVersionType(GitHubRelease release)
    {
        if (release.Prerelease)
        {
            return GameVersionType.Beta;
        }

        if (release.TagName?.Contains("alpha", StringComparison.OrdinalIgnoreCase) == true)
        {
            return GameVersionType.Alpha;
        }

        return GameVersionType.Release;
    }
}

/// <summary>
/// GitHub release API response model.
/// </summary>
internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset>? Assets { get; set; }
}

/// <summary>
/// GitHub release asset model.
/// </summary>
internal class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
