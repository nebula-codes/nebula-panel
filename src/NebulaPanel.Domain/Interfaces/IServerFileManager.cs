using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Interface for managing game server file systems.
/// All operations are relative to the server's install path.
/// </summary>
public interface IServerFileManager
{
    /// <summary>
    /// Gets the contents of a directory.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A FileSystemEntry representing the directory with its children.</returns>
    Task<FileSystemEntry> GetDirectoryContentsAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default);

    /// <summary>
    /// Reads the contents of a text file.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The file contents as a string.</returns>
    Task<string> ReadFileAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default);

    /// <summary>
    /// Writes content to a text file.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteFileAsync(
        GameServer server,
        string relativePath,
        string content,
        CancellationToken ct = default);

    /// <summary>
    /// Opens a file for download as a stream.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stream containing the file contents.</returns>
    Task<Stream> DownloadFileAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default);

    /// <summary>
    /// Uploads a file from a stream.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="content">The stream containing file content.</param>
    /// <param name="length">The length of the content in bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UploadFileAsync(
        GameServer server,
        string relativePath,
        Stream content,
        long length,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a file or directory.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default);

    /// <summary>
    /// Renames a file or directory.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="newName">The new name (not path) for the item.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RenameAsync(
        GameServer server,
        string relativePath,
        string newName,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new directory.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CreateDirectoryAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a ZIP archive of a file or directory.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The archive as a byte array.</returns>
    Task<byte[]> CreateArchiveAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts an archive to a directory.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Target directory path relative to the server's install directory.</param>
    /// <param name="archive">The archive stream to extract.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExtractArchiveAsync(
        GameServer server,
        string relativePath,
        Stream archive,
        CancellationToken ct = default);

    /// <summary>
    /// Gets information about a single file or directory.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A FileSystemEntry representing the item.</returns>
    Task<FileSystemEntry> GetInfoAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a file or directory exists.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="relativePath">Path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the path exists, false otherwise.</returns>
    Task<bool> ExistsAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default);

    /// <summary>
    /// Copies a file or directory to a new location.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="sourcePath">Source path relative to the server's install directory.</param>
    /// <param name="destinationPath">Destination path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CopyAsync(
        GameServer server,
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default);

    /// <summary>
    /// Moves a file or directory to a new location.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="sourcePath">Source path relative to the server's install directory.</param>
    /// <param name="destinationPath">Destination path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MoveAsync(
        GameServer server,
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts an archive that already exists on the server filesystem.
    /// </summary>
    /// <param name="server">The game server.</param>
    /// <param name="archivePath">Path to the archive relative to the server's install directory.</param>
    /// <param name="targetPath">Target directory path relative to the server's install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExtractArchiveInPlaceAsync(
        GameServer server,
        string archivePath,
        string targetPath,
        CancellationToken ct = default);
}
