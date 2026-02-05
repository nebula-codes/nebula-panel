using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents a downloadable asset from a release.
/// </summary>
public record ReleaseAsset
{
    /// <summary>
    /// The filename of the asset (e.g., "nebula-panel-linux-x64.tar.gz").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The URL to download the asset.
    /// </summary>
    public required string DownloadUrl { get; init; }

    /// <summary>
    /// The size of the asset in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// The SHA256 hash of the asset for verification, or null if not available.
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// The target platform for this asset.
    /// </summary>
    public PlatformTarget Platform { get; init; }
}
