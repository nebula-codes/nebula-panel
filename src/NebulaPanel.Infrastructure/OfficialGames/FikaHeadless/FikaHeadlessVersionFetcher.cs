using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.FikaHeadless;

/// <summary>
/// Fetches available Fika Headless Client versions from GitHub container registry tags.
/// </summary>
public class FikaHeadlessVersionFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<FikaHeadlessVersionFetcher> logger)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<FikaHeadlessVersionFetcher> _logger = logger;

    private const string GitHubTagsUrl = "https://api.github.com/repos/zhliau/fika-headless-docker/tags";

    public async Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("FikaHeadless");
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

                if (tagName.Contains("-old", StringComparison.OrdinalIgnoreCase))
                    continue;

                versions.Add(new GameVersionInfo
                {
                    Version = tagName,
                    DisplayName = $"Headless {tagName}",
                    ReleaseDate = DateTime.UtcNow,
                    VersionType = GameVersionType.Release,
                    IsRecommended = isFirst
                });

                isFirst = false;
            }

            _logger.LogInformation("Fetched {Count} Fika Headless versions from GitHub", versions.Count);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Fika Headless versions from GitHub");
            return [];
        }
    }
}
