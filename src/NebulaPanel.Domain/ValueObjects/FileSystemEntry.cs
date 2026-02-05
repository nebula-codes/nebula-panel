using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents a file or directory in a game server's file system.
/// </summary>
public record FileSystemEntry
{
    /// <summary>
    /// The name of the file or directory.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The relative path from the server's install directory.
    /// Uses forward slashes as separators.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The type of entry (File, Directory, or SymbolicLink).
    /// </summary>
    public required FileEntryType Type { get; init; }

    /// <summary>
    /// The size in bytes. Null for directories.
    /// </summary>
    public long? Size { get; init; }

    /// <summary>
    /// The last modification timestamp (UTC).
    /// </summary>
    public required DateTime ModifiedAt { get; init; }

    /// <summary>
    /// The MIME type for files (e.g., "text/plain", "application/json").
    /// Null for directories.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Whether the file can be edited in the web UI.
    /// Based on file extension and size limits.
    /// </summary>
    public bool IsEditable { get; init; }

    /// <summary>
    /// Whether the entry is hidden (starts with . or has hidden attribute).
    /// </summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// The file extension including the dot (e.g., ".json").
    /// Null for directories.
    /// </summary>
    public string? Extension { get; init; }

    /// <summary>
    /// Child entries for directories. Null for files.
    /// </summary>
    public IReadOnlyList<FileSystemEntry>? Children { get; init; }
}
