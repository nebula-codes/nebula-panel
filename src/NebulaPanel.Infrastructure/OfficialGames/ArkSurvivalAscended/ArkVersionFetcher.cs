using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.ArkSurvivalAscended;

/// <summary>
/// Fetches available ASA server versions via SteamCMD app info.
/// ASA doesn't publish semver versions, so we use Steam build IDs.
/// </summary>
public class ArkVersionFetcher(
    ISteamCmdService steamCmdService,
    IMemoryCache cache,
    ILogger<ArkVersionFetcher> logger)
{
    private readonly ISteamCmdService _steamCmdService = steamCmdService;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<ArkVersionFetcher> _logger = logger;

    private const string CacheKey = "ark-survival-ascended-versions";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    /// <summary>
    /// Fetches available ASA server versions from Steam.
    /// </summary>
    public async Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<IReadOnlyList<GameVersionInfo>>(CacheKey, out var cachedVersions) && cachedVersions is not null)
        {
            _logger.LogDebug("Returning cached ASA versions ({Count} versions)", cachedVersions.Count);
            return cachedVersions;
        }

        var versions = new List<GameVersionInfo>();

        // Always add "latest" as the recommended option
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
            // Try to get current build info from SteamCMD
            if (_steamCmdService.IsInstalled)
            {
                var appInfo = await _steamCmdService.GetAppInfoAsync(
                    ArkSurvivalAscendedProvider.AsaServerAppId,
                    "",
                    cancellationToken).ConfigureAwait(false);

                if (appInfo?.BuildId is not null)
                {
                    versions.Add(new GameVersionInfo
                    {
                        Version = $"build-{appInfo.BuildId}",
                        DisplayName = $"Build {appInfo.BuildId}",
                        VersionType = GameVersionType.Release,
                        IsRecommended = false,
                        ReleaseDate = appInfo.LastUpdated
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch ASA build info from Steam, using latest-only versions");
        }

        _cache.Set(CacheKey, (IReadOnlyList<GameVersionInfo>)versions, CacheDuration);
        _logger.LogInformation("Fetched {Count} ASA versions", versions.Count);

        return versions;
    }
}
