using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Fetchers;

/// <summary>
/// Fetches Purpur versions from the Purpur API.
/// </summary>
public class PurpurVersionFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<PurpurVersionFetcher> logger) : IMinecraftVersionFetcher
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly ILogger<PurpurVersionFetcher> _logger = logger;

    private const string BaseUrl = "https://api.purpurmc.org/v2/purpur";
    private const int MaxVersionsToFetch = 20;

    public string SourceName => "purpur";
    public MinecraftLoader LoaderType => MinecraftLoader.Purpur;

    public async Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching Purpur versions from Purpur API");

            var projectResponse = await _httpClient.GetFromJsonAsync<PurpurProjectResponse>(
                BaseUrl, cancellationToken).ConfigureAwait(false);

            if (projectResponse is null)
            {
                _logger.LogWarning("Received null response from Purpur API");
                return [];
            }

            var versions = new List<GameVersionInfo>();
            var latestMcVersion = projectResponse.Versions.LastOrDefault();

            // Fetch builds for recent MC versions
            var versionsToFetch = projectResponse.Versions
                .TakeLast(MaxVersionsToFetch)
                .Reverse()
                .ToList();

            foreach (var mcVersion in versionsToFetch)
            {
                try
                {
                    var versionUrl = $"{BaseUrl}/{mcVersion}";
                    var versionResponse = await _httpClient.GetFromJsonAsync<PurpurVersionResponse>(
                        versionUrl, cancellationToken).ConfigureAwait(false);

                    if (versionResponse?.Builds.All.Count > 0)
                    {
                        var latestBuild = versionResponse.Builds.Latest;
                        versions.Add(new GameVersionInfo
                        {
                            Version = $"purpur-{mcVersion}-{latestBuild}",
                            DisplayName = $"Purpur {mcVersion} (Build {latestBuild})",
                            VersionType = GameVersionType.Release,
                            LoaderType = MinecraftLoader.Purpur,
                            MinecraftVersion = mcVersion,
                            IsRecommended = mcVersion == latestMcVersion
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch Purpur builds for MC {Version}", mcVersion);
                }
            }

            _logger.LogDebug("Fetched {Count} Purpur versions", versions.Count);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Purpur versions");
            return [];
        }
    }
}
