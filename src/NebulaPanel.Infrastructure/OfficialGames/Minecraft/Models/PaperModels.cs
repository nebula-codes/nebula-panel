using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

/// <summary>
/// Response from https://api.papermc.io/v2/projects/paper
/// </summary>
public record PaperProjectResponse
{
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("project_name")]
    public required string ProjectName { get; init; }

    [JsonPropertyName("version_groups")]
    public required List<string> VersionGroups { get; init; }

    /// <summary>
    /// List of supported Minecraft versions.
    /// </summary>
    [JsonPropertyName("versions")]
    public required List<string> Versions { get; init; }
}

/// <summary>
/// Response from https://api.papermc.io/v2/projects/paper/versions/{version}
/// </summary>
public record PaperVersionResponse
{
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("project_name")]
    public required string ProjectName { get; init; }

    /// <summary>
    /// The Minecraft version.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    /// List of build numbers for this Minecraft version.
    /// </summary>
    [JsonPropertyName("builds")]
    public required List<int> Builds { get; init; }
}

/// <summary>
/// Response from https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{build}
/// </summary>
public record PaperBuildResponse
{
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("project_name")]
    public required string ProjectName { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("build")]
    public required int Build { get; init; }

    [JsonPropertyName("time")]
    public required DateTime Time { get; init; }

    /// <summary>
    /// Build channel: "default" or "experimental"
    /// </summary>
    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    [JsonPropertyName("promoted")]
    public bool Promoted { get; init; }

    [JsonPropertyName("downloads")]
    public required PaperDownloads Downloads { get; init; }
}

/// <summary>
/// Download information for a Paper build.
/// </summary>
public record PaperDownloads
{
    [JsonPropertyName("application")]
    public required PaperDownloadEntry Application { get; init; }

    [JsonPropertyName("mojang-mappings")]
    public PaperDownloadEntry? MojangMappings { get; init; }
}

/// <summary>
/// Individual download entry.
/// </summary>
public record PaperDownloadEntry
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }
}
