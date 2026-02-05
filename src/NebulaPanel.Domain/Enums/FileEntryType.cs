namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Represents the type of a file system entry.
/// </summary>
public enum FileEntryType
{
    /// <summary>
    /// A regular file.
    /// </summary>
    File,

    /// <summary>
    /// A directory/folder.
    /// </summary>
    Directory,

    /// <summary>
    /// A symbolic link.
    /// </summary>
    SymbolicLink
}
