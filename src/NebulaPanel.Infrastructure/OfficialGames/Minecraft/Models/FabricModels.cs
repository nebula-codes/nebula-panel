using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

/// <summary>
/// Entry from https://meta.fabricmc.net/v2/versions/game
/// </summary>
public record FabricGameVersion
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("stable")]
    public required bool Stable { get; init; }
}

/// <summary>
/// Entry from https://meta.fabricmc.net/v2/versions/loader
/// </summary>
public record FabricLoaderVersion
{
    [JsonPropertyName("separator")]
    public required string Separator { get; init; }

    [JsonPropertyName("build")]
    public required int Build { get; init; }

    [JsonPropertyName("maven")]
    public required string Maven { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("stable")]
    public required bool Stable { get; init; }
}

/// <summary>
/// Entry from https://meta.fabricmc.net/v2/versions/installer
/// </summary>
public record FabricInstallerVersion
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("maven")]
    public required string Maven { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("stable")]
    public required bool Stable { get; init; }
}

/// <summary>
/// Response from https://meta.fabricmc.net/v2/versions/loader/{game_version}/{loader_version}/server/json
/// </summary>
public record FabricServerLauncherMeta
{
    [JsonPropertyName("version")]
    public required int Version { get; init; }

    [JsonPropertyName("mainClass")]
    public required string MainClass { get; init; }
}
