using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Fetchers;

/// <summary>
/// Fetches Paper versions from the PaperMC API.
/// </summary>
public class PaperVersionFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<PaperVersionFetcher> logger) : IMinecraftVersionFetcher
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly ILogger<PaperVersionFetcher> _logger = logger;

    private const string BaseUrl = "https://api.papermc.io/v2/projects/paper";
    private const int MaxVersionsToFetch = 20;

    public string SourceName => "paper";
    public MinecraftLoader LoaderType => MinecraftLoader.Paper;

    public async Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching Paper versions from PaperMC API");

            var projectResponse = await _httpClient.GetFromJsonAsync<PaperProjectResponse>(
                BaseUrl, cancellationToken).ConfigureAwait(false);

            if (projectResponse is null)
            {
                _logger.LogWarning("Received null response from PaperMC API");
                return [];
            }

            var versions = new List<GameVersionInfo>();
            var latestMcVersion = projectResponse.Versions.LastOrDefault();

            // Fetch builds for recent MC versions (limit to prevent too many API calls)
            var versionsToFetch = projectResponse.Versions
                .TakeLast(MaxVersionsToFetch)
                .Reverse()
                .ToList();

            foreach (var mcVersion in versionsToFetch)
            {
                try
                {
                    var versionUrl = $"{BaseUrl}/versions/{mcVersion}";
                    var versionResponse = await _httpClient.GetFromJsonAsync<PaperVersionResponse>(
                        versionUrl, cancellationToken).ConfigureAwait(false);

                    if (versionResponse?.Builds.Count > 0)
                    {
                        var latestBuild = versionResponse.Builds.Max();
                        versions.Add(new GameVersionInfo
                        {
                            Version = $"paper-{mcVersion}-{latestBuild}",
                            DisplayName = $"Paper {mcVersion} (Build {latestBuild})",
                            VersionType = GameVersionType.Release,
                            LoaderType = MinecraftLoader.Paper,
                            MinecraftVersion = mcVersion,
                            IsRecommended = mcVersion == latestMcVersion
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch Paper builds for MC {Version}", mcVersion);
                }
            }

            _logger.LogDebug("Fetched {Count} Paper versions", versions.Count);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Paper versions");
            return [];
        }
    }
}
