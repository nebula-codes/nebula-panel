namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents metadata about a release from GitHub.
/// </summary>
public record ReleaseInfo
{
    /// <summary>
    /// The version tag (e.g., "1.2.3" or "1.2.3-beta.1").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// The release name/title.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The release body/changelog in markdown format.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// When the release was published.
    /// </summary>
    public DateTime PublishedAt { get; init; }

    /// <summary>
    /// Whether this is a pre-release version.
    /// </summary>
    public bool IsPreRelease { get; init; }

    /// <summary>
    /// The downloadable assets for this release.
    /// </summary>
    public List<ReleaseAsset> Assets { get; init; } = [];

    /// <summary>
    /// The minimum version required to upgrade from, or null if any version can upgrade.
    /// </summary>
    public string? MinimumUpgradeVersion { get; init; }

    /// <summary>
    /// Whether this release requires database migrations.
    /// </summary>
    public bool RequiresDatabaseMigration { get; init; }

    /// <summary>
    /// List of breaking changes in this release.
    /// </summary>
    public List<string> BreakingChanges { get; init; } = [];
}
