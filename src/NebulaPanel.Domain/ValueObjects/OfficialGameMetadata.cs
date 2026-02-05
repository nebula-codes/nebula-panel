namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Runtime metadata for an official game provider.
/// This is computed at runtime and not persisted directly.
/// </summary>
public record OfficialGameMetadata
{
    /// <summary>
    /// The game's unique slug identifier.
    /// </summary>
    public required string GameSlug { get; init; }

    /// <summary>
    /// Display name of the game.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Type of provider: "Code" for IOfficialGameProvider implementations,
    /// "Json" for JSON-defined games.
    /// </summary>
    public required string ProviderType { get; init; }

    /// <summary>
    /// Whether this game is enabled and available for server creation.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Number of cached versions available.
    /// </summary>
    public int? CachedVersionCount { get; init; }

    /// <summary>
    /// When versions were last fetched from upstream sources.
    /// </summary>
    public DateTime? LastVersionCheck { get; init; }

    /// <summary>
    /// When the configuration schemas were last updated.
    /// </summary>
    public DateTime? LastSchemaUpdate { get; init; }

    /// <summary>
    /// Version string of the schema definition.
    /// </summary>
    public string? SchemaVersion { get; init; }

    /// <summary>
    /// Path to the game icon.
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// Number of servers using this game.
    /// </summary>
    public int ServerCount { get; init; }

    /// <summary>
    /// Whether this provider was loaded from a user-provided definition.
    /// </summary>
    public bool IsUserDefined { get; init; }
}
