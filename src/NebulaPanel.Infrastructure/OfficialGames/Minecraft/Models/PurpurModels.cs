using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

/// <summary>
/// Response from https://api.purpurmc.org/v2/purpur
/// </summary>
public record PurpurProjectResponse
{
    [JsonPropertyName("project")]
    public required string Project { get; init; }

    /// <summary>
    /// List of supported Minecraft versions.
    /// </summary>
    [JsonPropertyName("versions")]
    public required List<string> Versions { get; init; }
}

/// <summary>
/// Response from https://api.purpurmc.org/v2/purpur/{version}
/// </summary>
public record PurpurVersionResponse
{
    [JsonPropertyName("project")]
    public required string Project { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("builds")]
    public required PurpurBuilds Builds { get; init; }
}

/// <summary>
/// Build information for a Purpur version.
/// </summary>
public record PurpurBuilds
{
    /// <summary>
    /// Latest build number.
    /// </summary>
    [JsonPropertyName("latest")]
    public required string Latest { get; init; }

    /// <summary>
    /// All available build numbers.
    /// </summary>
    [JsonPropertyName("all")]
    public required List<string> All { get; init; }
}

/// <summary>
/// Response from https://api.purpurmc.org/v2/purpur/{version}/{build}
/// </summary>
public record PurpurBuildResponse
{
    [JsonPropertyName("project")]
    public required string Project { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("build")]
    public required string Build { get; init; }

    [JsonPropertyName("result")]
    public required string Result { get; init; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("duration")]
    public long Duration { get; init; }

    [JsonPropertyName("md5")]
    public string? Md5 { get; init; }
}
