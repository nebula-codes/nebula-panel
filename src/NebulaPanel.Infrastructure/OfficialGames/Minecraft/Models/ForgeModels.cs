using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

/// <summary>
/// Response from https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json
/// </summary>
public record ForgePromotions
{
    [JsonPropertyName("homepage")]
    public required string Homepage { get; init; }

    /// <summary>
    /// Dictionary of promotion keys to Forge versions.
    /// Keys are formatted as "{mcVersion}-latest" or "{mcVersion}-recommended".
    /// Values are Forge version numbers (e.g., "49.0.19").
    /// </summary>
    [JsonPropertyName("promos")]
    public required Dictionary<string, string> Promos { get; init; }
}

/// <summary>
/// Parsed Forge version information.
/// </summary>
public record ForgeVersionInfo
{
    /// <summary>
    /// The Minecraft version (e.g., "1.20.4").
    /// </summary>
    public required string MinecraftVersion { get; init; }

    /// <summary>
    /// The Forge version number (e.g., "49.0.19").
    /// </summary>
    public required string ForgeVersion { get; init; }

    /// <summary>
    /// Whether this is the recommended version (vs just latest).
    /// </summary>
    public bool IsRecommended { get; init; }

    /// <summary>
    /// Whether this is the latest version.
    /// </summary>
    public bool IsLatest { get; init; }
}
