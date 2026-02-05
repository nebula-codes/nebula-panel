using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Fetchers;

/// <summary>
/// Fetches Fabric versions from the Fabric Meta API.
/// </summary>
public class FabricVersionFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<FabricVersionFetcher> logger) : IMinecraftVersionFetcher
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly ILogger<FabricVersionFetcher> _logger = logger;

    private const string GameVersionsUrl = "https://meta.fabricmc.net/v2/versions/game";
    private const string LoaderVersionsUrl = "https://meta.fabricmc.net/v2/versions/loader";
    private const int MaxVersionsToInclude = 30;

    public string SourceName => "fabric";
    public MinecraftLoader LoaderType => MinecraftLoader.Fabric;

    public async Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching Fabric versions from Fabric Meta API");

            // Fetch game versions and loader versions in parallel
            var gameVersionsTask = _httpClient.GetFromJsonAsync<List<FabricGameVersion>>(
                GameVersionsUrl, cancellationToken);
            var loaderVersionsTask = _httpClient.GetFromJsonAsync<List<FabricLoaderVersion>>(
                LoaderVersionsUrl, cancellationToken);

            await Task.WhenAll(gameVersionsTask, loaderVersionsTask).ConfigureAwait(false);

            var gameVersions = await gameVersionsTask.ConfigureAwait(false);
            var loaderVersions = await loaderVersionsTask.ConfigureAwait(false);

            if (gameVersions is null || loaderVersions is null)
            {
                _logger.LogWarning("Received null response from Fabric Meta API");
                return [];
            }

            // Get the latest stable loader version
            var latestLoader = loaderVersions.FirstOrDefault(l => l.Stable)?.Version
                ?? loaderVersions.FirstOrDefault()?.Version;

            if (latestLoader is null)
            {
                _logger.LogWarning("No Fabric loader versions available");
                return [];
            }

            // Get stable game versions (or all if not enough stable ones)
            var stableGameVersions = gameVersions
                .Where(v => v.Stable)
                .Take(MaxVersionsToInclude)
                .ToList();

            var latestStableVersion = stableGameVersions.FirstOrDefault()?.Version;

            var versions = stableGameVersions
                .Select(gv => new GameVersionInfo
                {
                    Version = $"fabric-{gv.Version}-{latestLoader}",
                    DisplayName = $"Fabric {gv.Version} (Loader {latestLoader})",
                    VersionType = GameVersionType.Release,
                    LoaderType = MinecraftLoader.Fabric,
                    MinecraftVersion = gv.Version,
                    IsRecommended = gv.Version == latestStableVersion
                })
                .ToList();

            _logger.LogDebug("Fetched {Count} Fabric versions with loader {Loader}", versions.Count, latestLoader);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Fabric versions");
            return [];
        }
    }
}
