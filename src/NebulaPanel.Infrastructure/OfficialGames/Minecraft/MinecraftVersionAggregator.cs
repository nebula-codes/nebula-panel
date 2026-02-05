using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Fetchers;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Aggregates Minecraft versions from all sources with caching.
/// </summary>
public class MinecraftVersionAggregator
{
    private readonly IReadOnlyList<IMinecraftVersionFetcher> _fetchers;
    private readonly ILogger<MinecraftVersionAggregator> _logger;
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(6);
    private static readonly string CacheDirectory = Path.Combine("data", "cache");
    private static readonly string CacheFilePath = Path.Combine(CacheDirectory, "minecraft-versions.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MinecraftVersionAggregator(
        IEnumerable<IMinecraftVersionFetcher> fetchers,
        ILogger<MinecraftVersionAggregator> logger)
    {
        _fetchers = fetchers.ToList();
        _logger = logger;
    }

    /// <summary>
    /// Gets all available Minecraft versions, using cache if valid.
    /// </summary>
    public async Task<IReadOnlyList<GameVersionInfo>> GetAllVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        // Try cache first
        var cached = await GetCachedVersionsAsync(cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            _logger.LogDebug("Returning {Count} cached Minecraft versions", cached.Count);
            return cached;
        }

        // Fetch fresh data (with lock to prevent parallel fetches)
        await _fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check cache after acquiring lock
            cached = await GetCachedVersionsAsync(cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            var versions = await FetchAllVersionsAsync(cancellationToken).ConfigureAwait(false);

            // Cache results
            await SetCachedVersionsAsync(versions, cancellationToken).ConfigureAwait(false);

            return versions;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    /// <summary>
    /// Gets versions grouped by Minecraft version for UI display.
    /// </summary>
    public async Task<Dictionary<string, IReadOnlyList<GameVersionInfo>>> GetVersionsGroupedByMinecraftVersionAsync(
        CancellationToken cancellationToken = default)
    {
        var versions = await GetAllVersionsAsync(cancellationToken).ConfigureAwait(false);

        return versions
            .Where(v => v.MinecraftVersion is not null)
            .GroupBy(v => v.MinecraftVersion!)
            .OrderByDescending(g => ParseVersion(g.Key))
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<GameVersionInfo>)g.OrderBy(v => v.LoaderType).ToList());
    }

    /// <summary>
    /// Forces a refresh of the version cache.
    /// </summary>
    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        await _fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var versions = await FetchAllVersionsAsync(cancellationToken).ConfigureAwait(false);
            await SetCachedVersionsAsync(versions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task<IReadOnlyList<GameVersionInfo>> FetchAllVersionsAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching Minecraft versions from {Count} sources", _fetchers.Count);

        // Fetch from all sources in parallel
        var fetchTasks = _fetchers.Select(async fetcher =>
        {
            try
            {
                var result = await fetcher.FetchVersionsAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Fetched {Count} versions from {Source}", result.Count, fetcher.SourceName);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch versions from {Source}", fetcher.SourceName);
                return Array.Empty<GameVersionInfo>();
            }
        });

        var results = await Task.WhenAll(fetchTasks).ConfigureAwait(false);

        // Combine and sort all versions
        var allVersions = results
            .SelectMany(r => r)
            .OrderByDescending(v => v.IsRecommended)
            .ThenByDescending(v => v.ReleaseDate ?? DateTime.MinValue)
            .ThenByDescending(v => ParseVersion(v.MinecraftVersion ?? "0.0.0"))
            .ThenBy(v => v.LoaderType)
            .ToList();

        _logger.LogInformation("Fetched {Count} total Minecraft versions from {SourceCount} sources",
            allVersions.Count, _fetchers.Count);

        // If fetch returned nothing, try to use expired cache
        if (allVersions.Count == 0)
        {
            var expiredCache = await GetCachedVersionsAsync(cancellationToken, ignoreExpiration: true)
                .ConfigureAwait(false);
            if (expiredCache is not null && expiredCache.Count > 0)
            {
                _logger.LogWarning("Using expired cache due to fetch failure ({Count} versions)", expiredCache.Count);
                return expiredCache;
            }
        }

        return allVersions;
    }

    private async Task<IReadOnlyList<GameVersionInfo>?> GetCachedVersionsAsync(
        CancellationToken cancellationToken,
        bool ignoreExpiration = false)
    {
        try
        {
            if (!File.Exists(CacheFilePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(CacheFilePath, cancellationToken).ConfigureAwait(false);
            var cacheData = JsonSerializer.Deserialize<VersionCacheData>(json, JsonOptions);

            if (cacheData is null)
            {
                return null;
            }

            // Check expiration
            if (!ignoreExpiration && DateTime.UtcNow > cacheData.ExpiresAt)
            {
                _logger.LogDebug("Minecraft version cache expired at {ExpiresAt}", cacheData.ExpiresAt);
                return null;
            }

            return cacheData.Versions
                .Select(v => v.ToGameVersionInfo())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Minecraft version cache");
            return null;
        }
    }

    private async Task SetCachedVersionsAsync(
        IReadOnlyList<GameVersionInfo> versions,
        CancellationToken cancellationToken)
    {
        try
        {
            // Ensure cache directory exists
            Directory.CreateDirectory(CacheDirectory);

            var cacheData = new VersionCacheData
            {
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(CacheExpiration),
                Versions = versions.Select(CachedVersionInfo.FromGameVersionInfo).ToList()
            };

            var json = JsonSerializer.Serialize(cacheData, JsonOptions);
            await File.WriteAllTextAsync(CacheFilePath, json, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Cached {Count} Minecraft versions until {ExpiresAt}",
                versions.Count, cacheData.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write Minecraft version cache");
        }
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
