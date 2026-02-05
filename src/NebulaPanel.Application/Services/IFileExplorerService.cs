using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for managing game server files.
/// </summary>
public interface IFileExplorerService
{
    /// <summary>
    /// Gets the contents of a directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Relative path to the directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Directory contents or error.</returns>
    Task<Result<FileSystemEntryDto>> GetDirectoryContentsAsync(
        Guid serverId,
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Reads a text file.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Relative path to the file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>File content and metadata or error.</returns>
    Task<Result<ReadFileResponse>> ReadFileAsync(
        Guid serverId,
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Writes content to a text file.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="request">Write request with path and content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<Result> WriteFileAsync(
        Guid serverId,
        WriteFileRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a file for download.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Relative path to the file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Download info with stream or error.</returns>
    Task<Result<FileDownloadInfo>> DownloadFileAsync(
        Guid serverId,
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Uploads a file.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Target directory path.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="content">The file content stream.</param>
    /// <param name="length">The content length.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Upload result or error.</returns>
    Task<Result<UploadResult>> UploadFileAsync(
        Guid serverId,
        string path,
        string fileName,
        Stream content,
        long length,
        CancellationToken ct = default);

    /// <summary>
    /// Uploads multiple files.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Target directory path.</param>
    /// <param name="files">Collection of files to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Bulk upload result or error.</returns>
    Task<Result<BulkUploadResult>> UploadFilesAsync(
        Guid serverId,
        string path,
        IEnumerable<(string FileName, Stream Content, long Length)> files,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a file or directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Relative path to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<Result> DeleteAsync(
        Guid serverId,
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Renames a file or directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="request">Rename request with path and new name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<Result> RenameAsync(
        Guid serverId,
        RenameRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="request">Request with directory path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<Result> CreateDirectoryAsync(
        Guid serverId,
        CreateDirectoryRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a ZIP archive of a file or directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Path to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Archive download info or error.</returns>
    Task<Result<FileDownloadInfo>> CreateArchiveAsync(
        Guid serverId,
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts an archive to a directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Target directory path.</param>
    /// <param name="archive">The archive stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<Result> ExtractArchiveAsync(
        Guid serverId,
        string path,
        Stream archive,
        CancellationToken ct = default);

    /// <summary>
    /// Copies a file or directory to a new location.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="sourcePath">Source path relative to server install directory.</param>
    /// <param name="destinationPath">Destination path relative to server install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<Result> CopyAsync(
        Guid serverId,
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default);

    /// <summary>
    /// Moves a file or directory to a new location.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="sourcePath">Source path relative to server install directory.</param>
    /// <param name="destinationPath">Destination path relative to server install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<Result> MoveAsync(
        Guid serverId,
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts an archive that already exists on the server filesystem.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="archivePath">Path to the archive relative to server install directory.</param>
    /// <param name="targetPath">Target directory path relative to server install directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<Result> ExtractArchiveInPlaceAsync(
        Guid serverId,
        string archivePath,
        string targetPath,
        CancellationToken ct = default);
}
