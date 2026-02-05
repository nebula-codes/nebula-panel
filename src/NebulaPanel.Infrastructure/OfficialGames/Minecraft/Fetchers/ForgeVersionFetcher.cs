using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Fetchers;

/// <summary>
/// Fetches Forge versions from the MinecraftForge promotions API.
/// </summary>
public class ForgeVersionFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<ForgeVersionFetcher> logger) : IMinecraftVersionFetcher
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly ILogger<ForgeVersionFetcher> _logger = logger;

    private const string PromotionsUrl = "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";

    public string SourceName => "forge";
    public MinecraftLoader LoaderType => MinecraftLoader.Forge;

    public async Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching Forge versions from MinecraftForge API");

            var promotions = await _httpClient.GetFromJsonAsync<ForgePromotions>(
                PromotionsUrl, cancellationToken).ConfigureAwait(false);

            if (promotions is null)
            {
                _logger.LogWarning("Received null response from MinecraftForge API");
                return [];
            }

            // Parse promotions into structured data
            var forgeVersions = ParsePromotions(promotions.Promos);

            // Group by Minecraft version and prefer recommended over latest
            var versionsByMc = forgeVersions
                .GroupBy(v => v.MinecraftVersion)
                .Select(g =>
                {
                    var recommended = g.FirstOrDefault(v => v.IsRecommended);
                    var latest = g.FirstOrDefault(v => v.IsLatest);
                    return recommended ?? latest;
                })
                .Where(v => v is not null)
                .Select(v => v!)
                .OrderByDescending(v => ParseVersion(v.MinecraftVersion))
                .ToList();

            // Find the latest MC version with Forge support
            var latestMcVersion = versionsByMc.FirstOrDefault()?.MinecraftVersion;

            var versions = versionsByMc
                .Select(fv => new GameVersionInfo
                {
                    Version = $"forge-{fv.MinecraftVersion}-{fv.ForgeVersion}",
                    DisplayName = $"Forge {fv.MinecraftVersion} ({fv.ForgeVersion}){(fv.IsRecommended ? " [Recommended]" : "")}",
                    VersionType = GameVersionType.Release,
                    LoaderType = MinecraftLoader.Forge,
                    MinecraftVersion = fv.MinecraftVersion,
                    IsRecommended = fv.MinecraftVersion == latestMcVersion && fv.IsRecommended
                })
                .ToList();

            _logger.LogDebug("Fetched {Count} Forge versions", versions.Count);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Forge versions");
            return [];
        }
    }

    private static List<ForgeVersionInfo> ParsePromotions(Dictionary<string, string> promos)
    {
        var versions = new List<ForgeVersionInfo>();

        foreach (var (key, forgeVersion) in promos)
        {
            // Keys are formatted as "{mcVersion}-latest" or "{mcVersion}-recommended"
            var parts = key.Split('-');
            if (parts.Length < 2) continue;

            var mcVersion = parts[0];
            var promoType = parts[^1]; // Last part

            // Handle versions like "1.20.4-latest" or "1.7.10_pre4-latest"
            if (parts.Length > 2)
            {
                mcVersion = string.Join("-", parts[..^1]);
            }

            versions.Add(new ForgeVersionInfo
            {
                MinecraftVersion = mcVersion,
                ForgeVersion = forgeVersion,
                IsRecommended = promoType == "recommended",
                IsLatest = promoType == "latest"
            });
        }

        return versions;
    }

    private static Version ParseVersion(string versionString)
    {
        // Handle versions like "1.20.4" or "1.7.10_pre4"
        var cleanVersion = versionString.Split('_')[0].Split('-')[0];
        var parts = cleanVersion.Split('.');

        try
        {
            var major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
            var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            var patch = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            return new Version(major, minor, patch);
        }
        catch
        {
            return new Version(0, 0, 0);
        }
    }
}
