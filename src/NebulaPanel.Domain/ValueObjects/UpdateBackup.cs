namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents metadata about a pre-update backup.
/// </summary>
public record UpdateBackup
{
    /// <summary>
    /// The unique identifier for this backup.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// When the backup was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// The version of the application when the backup was created.
    /// </summary>
    public required string FromVersion { get; init; }

    /// <summary>
    /// The version being upgraded to.
    /// </summary>
    public required string ForVersion { get; init; }

    /// <summary>
    /// The path to the backup directory.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The total size of the backup in bytes.
    /// </summary>
    public long SizeBytes { get; init; }
}
