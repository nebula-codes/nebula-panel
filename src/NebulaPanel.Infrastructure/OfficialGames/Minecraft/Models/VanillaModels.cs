using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

/// <summary>
/// Response from https://launchermeta.mojang.com/mc/game/version_manifest_v2.json
/// </summary>
public record MinecraftVersionManifest
{
    [JsonPropertyName("latest")]
    public required LatestVersions Latest { get; init; }

    [JsonPropertyName("versions")]
    public required List<MinecraftVersionEntry> Versions { get; init; }
}

/// <summary>
/// Latest version identifiers from Mojang.
/// </summary>
public record LatestVersions
{
    [JsonPropertyName("release")]
    public required string Release { get; init; }

    [JsonPropertyName("snapshot")]
    public required string Snapshot { get; init; }
}

/// <summary>
/// Individual version entry from Mojang's version manifest.
/// </summary>
public record MinecraftVersionEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Version type: "release", "snapshot", "old_beta", "old_alpha"
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("time")]
    public required DateTime Time { get; init; }

    [JsonPropertyName("releaseTime")]
    public required DateTime ReleaseTime { get; init; }

    [JsonPropertyName("sha1")]
    public string? Sha1 { get; init; }

    [JsonPropertyName("complianceLevel")]
    public int ComplianceLevel { get; init; }
}
