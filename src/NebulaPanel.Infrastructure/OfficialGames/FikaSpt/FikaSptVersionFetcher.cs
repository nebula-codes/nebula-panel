using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.FikaSpt;

/// <summary>
/// Fetches available FIKA SPT server versions from GitHub container registry tags.
/// </summary>
public class FikaSptVersionFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<FikaSptVersionFetcher> logger)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<FikaSptVersionFetcher> _logger = logger;

    private const string GitHubTagsUrl = "https://api.github.com/repos/zhliau/fika-spt-server-docker/tags";

    public async Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("FikaSpt");
            var response = await client.GetAsync(GitHubTagsUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var versions = new List<GameVersionInfo>();
            var isFirst = true;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var tagName = element.GetProperty("name").GetString();
                if (string.IsNullOrEmpty(tagName))
                    continue;

                // Skip deprecated/old tags
                if (tagName.Contains("-old", StringComparison.OrdinalIgnoreCase))
                    continue;

                versions.Add(new GameVersionInfo
                {
                    Version = tagName,
                    DisplayName = $"SPT {tagName}",
                    ReleaseDate = DateTime.UtcNow, // GitHub tags API doesn't include dates
                    VersionType = GameVersionType.Release,
                    IsRecommended = isFirst // First (latest) tag is recommended
                });

                isFirst = false;
            }

            _logger.LogInformation("Fetched {Count} FIKA SPT versions from GitHub", versions.Count);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FIKA SPT versions from GitHub");
            return [];
        }
    }
}
