using System.Text.Json.Serialization;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

/// <summary>
/// Cached Minecraft version data with metadata.
/// </summary>
public record VersionCacheData
{
    /// <summary>
    /// When the cache was last populated.
    /// </summary>
    [JsonPropertyName("fetchedAt")]
    public required DateTime FetchedAt { get; init; }

    /// <summary>
    /// When the cache expires.
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public required DateTime ExpiresAt { get; init; }

    /// <summary>
    /// The cached version information.
    /// </summary>
    [JsonPropertyName("versions")]
    public required List<CachedVersionInfo> Versions { get; init; }
}

/// <summary>
/// Serializable version of GameVersionInfo for caching.
/// </summary>
public record CachedVersionInfo
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("releaseDate")]
    public DateTime? ReleaseDate { get; init; }

    [JsonPropertyName("versionType")]
    public required string VersionType { get; init; }

    [JsonPropertyName("loaderType")]
    public string? LoaderType { get; init; }

    [JsonPropertyName("minecraftVersion")]
    public string? MinecraftVersion { get; init; }

    [JsonPropertyName("isRecommended")]
    public bool IsRecommended { get; init; }

    /// <summary>
    /// Converts to domain GameVersionInfo.
    /// </summary>
    public GameVersionInfo ToGameVersionInfo()
    {
        return new GameVersionInfo
        {
            Version = Version,
            DisplayName = DisplayName,
            ReleaseDate = ReleaseDate,
            VersionType = Enum.TryParse<Domain.Enums.GameVersionType>(VersionType, true, out var vt)
                ? vt
                : Domain.Enums.GameVersionType.Release,
            LoaderType = string.IsNullOrEmpty(LoaderType)
                ? null
                : Enum.TryParse<Domain.Enums.MinecraftLoader>(LoaderType, true, out var lt)
                    ? lt
                    : null,
            MinecraftVersion = MinecraftVersion,
            IsRecommended = IsRecommended
        };
    }

    /// <summary>
    /// Creates from domain GameVersionInfo.
    /// </summary>
    public static CachedVersionInfo FromGameVersionInfo(GameVersionInfo info)
    {
        return new CachedVersionInfo
        {
            Version = info.Version,
            DisplayName = info.DisplayName,
            ReleaseDate = info.ReleaseDate,
            VersionType = info.VersionType.ToString(),
            LoaderType = info.LoaderType?.ToString(),
            MinecraftVersion = info.MinecraftVersion,
            IsRecommended = info.IsRecommended
        };
    }
}
