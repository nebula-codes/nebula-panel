using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Fetchers;

/// <summary>
/// Fetches vanilla Minecraft versions from Mojang's version manifest.
/// </summary>
public class VanillaVersionFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<VanillaVersionFetcher> logger) : IMinecraftVersionFetcher
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Minecraft");
    private readonly ILogger<VanillaVersionFetcher> _logger = logger;

    private const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    public string SourceName => "vanilla";
    public MinecraftLoader LoaderType => MinecraftLoader.Vanilla;

    public async Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching Vanilla Minecraft versions from Mojang");

            var manifest = await _httpClient.GetFromJsonAsync<MinecraftVersionManifest>(
                ManifestUrl, cancellationToken).ConfigureAwait(false);

            if (manifest is null)
            {
                _logger.LogWarning("Received null manifest from Mojang API");
                return [];
            }

            var versions = manifest.Versions
                .Select(v => new GameVersionInfo
                {
                    Version = $"vanilla-{v.Id}",
                    DisplayName = $"Vanilla {v.Id}",
                    ReleaseDate = v.ReleaseTime,
                    VersionType = MapVersionType(v.Type),
                    LoaderType = MinecraftLoader.Vanilla,
                    MinecraftVersion = v.Id,
                    IsRecommended = v.Id == manifest.Latest.Release
                })
                .ToList();

            _logger.LogDebug("Fetched {Count} Vanilla versions", versions.Count);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Vanilla Minecraft versions");
            return [];
        }
    }

    private static GameVersionType MapVersionType(string type) => type.ToLowerInvariant() switch
    {
        "release" => GameVersionType.Release,
        "snapshot" => GameVersionType.Snapshot,
        "old_beta" => GameVersionType.Beta,
        "old_alpha" => GameVersionType.Alpha,
        _ => GameVersionType.Release
    };
}
