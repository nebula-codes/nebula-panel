using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for managing game server files.
/// </summary>
public class FileExplorerService(
    IGameServerRepository serverRepository,
    IServerFileManager fileManager,
    ILogger<FileExplorerService> logger) : IFileExplorerService
{
    private readonly IGameServerRepository _serverRepository = serverRepository;
    private readonly IServerFileManager _fileManager = fileManager;
    private readonly ILogger<FileExplorerService> _logger = logger;

    public async Task<Result<FileSystemEntryDto>> GetDirectoryContentsAsync(
        Guid serverId,
        string path,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure<FileSystemEntryDto>(Error.NotFound("Server", serverId.ToString()));
            }

            var entry = await _fileManager.GetDirectoryContentsAsync(server, path, ct).ConfigureAwait(false);
            return MapToDto(entry);
        }
        catch (DirectoryNotFoundException ex)
        {
            return Result.Failure<FileSystemEntryDto>(Error.NotFound("Directory", ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure<FileSystemEntryDto>(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting directory contents for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure<FileSystemEntryDto>(Error.FromException(ex));
        }
    }

    public async Task<Result<ReadFileResponse>> ReadFileAsync(
        Guid serverId,
        string path,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure<ReadFileResponse>(Error.NotFound("Server", serverId.ToString()));
            }

            var info = await _fileManager.GetInfoAsync(server, path, ct).ConfigureAwait(false);
            if (info.Type != FileEntryType.File)
            {
                return Result.Failure<ReadFileResponse>(Error.Validation("Path is not a file."));
            }

            var content = await _fileManager.ReadFileAsync(server, path, ct).ConfigureAwait(false);

            return new ReadFileResponse
            {
                Path = info.Path,
                Content = content,
                MimeType = info.MimeType ?? "text/plain",
                Size = info.Size ?? 0,
                ModifiedAt = info.ModifiedAt,
                Extension = info.Extension
            };
        }
        catch (FileNotFoundException ex)
        {
            return Result.Failure<ReadFileResponse>(Error.NotFound("File", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ReadFileResponse>(Error.InvalidOperation(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure<ReadFileResponse>(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure<ReadFileResponse>(Error.FromException(ex));
        }
    }

    public async Task<Result> WriteFileAsync(
        Guid serverId,
        WriteFileRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure(Error.NotFound("Server", serverId.ToString()));
            }

            // Check if file exists when CreateIfNotExists is false
            if (!request.CreateIfNotExists)
            {
                var exists = await _fileManager.ExistsAsync(server, request.Path, ct).ConfigureAwait(false);
                if (!exists)
                {
                    return Result.Failure(Error.NotFound("File", request.Path));
                }
            }

            await _fileManager.WriteFileAsync(server, request.Path, request.Content, ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, path {Path}", serverId, request.Path);
            return Result.Failure(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing file for server {ServerId}, path {Path}", serverId, request.Path);
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result<FileDownloadInfo>> DownloadFileAsync(
        Guid serverId,
        string path,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure<FileDownloadInfo>(Error.NotFound("Server", serverId.ToString()));
            }

            var info = await _fileManager.GetInfoAsync(server, path, ct).ConfigureAwait(false);
            if (info.Type != FileEntryType.File)
            {
                return Result.Failure<FileDownloadInfo>(Error.Validation("Path is not a file. Use archive for directories."));
            }

            var stream = await _fileManager.DownloadFileAsync(server, path, ct).ConfigureAwait(false);

            return new FileDownloadInfo
            {
                FileName = info.Name,
                MimeType = info.MimeType ?? "application/octet-stream",
                Content = stream,
                Size = info.Size ?? 0
            };
        }
        catch (FileNotFoundException ex)
        {
            return Result.Failure<FileDownloadInfo>(Error.NotFound("File", ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure<FileDownloadInfo>(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure<FileDownloadInfo>(Error.FromException(ex));
        }
    }

    public async Task<Result<UploadResult>> UploadFileAsync(
        Guid serverId,
        string path,
        string fileName,
        Stream content,
        long length,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure<UploadResult>(Error.NotFound("Server", serverId.ToString()));
            }

            // Sanitize file name
            fileName = SanitizeFileName(fileName);
            var filePath = string.IsNullOrEmpty(path) ? fileName : $"{path}/{fileName}";

            await _fileManager.UploadFileAsync(server, filePath, content, length, ct).ConfigureAwait(false);

            return new UploadResult
            {
                Path = filePath,
                Name = fileName,
                Size = length,
                Success = true
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, path {Path}", serverId, path);
            return new UploadResult
            {
                Path = path,
                Name = fileName,
                Size = length,
                Success = false,
                Error = "Access denied."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file for server {ServerId}, path {Path}", serverId, path);
            return new UploadResult
            {
                Path = path,
                Name = fileName,
                Size = length,
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<Result<BulkUploadResult>> UploadFilesAsync(
        Guid serverId,
        string path,
        IEnumerable<(string FileName, Stream Content, long Length)> files,
        CancellationToken ct = default)
    {
        var results = new List<UploadResult>();

        foreach (var (fileName, content, length) in files)
        {
            ct.ThrowIfCancellationRequested();

            var result = await UploadFileAsync(serverId, path, fileName, content, length, ct).ConfigureAwait(false);
            results.Add(result.IsSuccess ? result.Value! : new UploadResult
            {
                Path = path,
                Name = fileName,
                Size = length,
                Success = false,
                Error = result.Error
            });
        }

        return new BulkUploadResult
        {
            Results = results,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success)
        };
    }

    public async Task<Result> DeleteAsync(
        Guid serverId,
        string path,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure(Error.NotFound("Server", serverId.ToString()));
            }

            await _fileManager.DeleteAsync(server, path, ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (FileNotFoundException ex)
        {
            return Result.Failure(Error.NotFound("File", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.InvalidOperation(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result> RenameAsync(
        Guid serverId,
        RenameRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure(Error.NotFound("Server", serverId.ToString()));
            }

            // Sanitize new name
            var newName = SanitizeFileName(request.NewName);

            await _fileManager.RenameAsync(server, request.Path, newName, ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (FileNotFoundException ex)
        {
            return Result.Failure(Error.NotFound("File", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.InvalidOperation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, path {Path}", serverId, request.Path);
            return Result.Failure(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming for server {ServerId}, path {Path}", serverId, request.Path);
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result> CreateDirectoryAsync(
        Guid serverId,
        CreateDirectoryRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure(Error.NotFound("Server", serverId.ToString()));
            }

            await _fileManager.CreateDirectoryAsync(server, request.Path, ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.InvalidOperation(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, path {Path}", serverId, request.Path);
            return Result.Failure(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating directory for server {ServerId}, path {Path}", serverId, request.Path);
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result<FileDownloadInfo>> CreateArchiveAsync(
        Guid serverId,
        string path,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure<FileDownloadInfo>(Error.NotFound("Server", serverId.ToString()));
            }

            var info = await _fileManager.GetInfoAsync(server, path, ct).ConfigureAwait(false);
            var archiveBytes = await _fileManager.CreateArchiveAsync(server, path, ct).ConfigureAwait(false);

            var fileName = $"{info.Name}.zip";
            var stream = new MemoryStream(archiveBytes);

            return new FileDownloadInfo
            {
                FileName = fileName,
                MimeType = "application/zip",
                Content = stream,
                Size = archiveBytes.Length
            };
        }
        catch (FileNotFoundException ex)
        {
            return Result.Failure<FileDownloadInfo>(Error.NotFound("File", ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure<FileDownloadInfo>(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating archive for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure<FileDownloadInfo>(Error.FromException(ex));
        }
    }

    public async Task<Result> ExtractArchiveAsync(
        Guid serverId,
        string path,
        Stream archive,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure(Error.NotFound("Server", serverId.ToString()));
            }

            await _fileManager.ExtractArchiveAsync(server, path, archive, ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.InvalidOperation(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting archive for server {ServerId}, path {Path}", serverId, path);
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result> CopyAsync(
        Guid serverId,
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure(Error.NotFound("Server", serverId.ToString()));
            }

            await _fileManager.CopyAsync(server, sourcePath, destinationPath, ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (FileNotFoundException ex)
        {
            return Result.Failure(Error.NotFound("File", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.InvalidOperation(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, copy {Source} -> {Destination}", serverId, sourcePath, destinationPath);
            return Result.Failure(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying for server {ServerId}, {Source} -> {Destination}", serverId, sourcePath, destinationPath);
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result> MoveAsync(
        Guid serverId,
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure(Error.NotFound("Server", serverId.ToString()));
            }

            await _fileManager.MoveAsync(server, sourcePath, destinationPath, ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (FileNotFoundException ex)
        {
            return Result.Failure(Error.NotFound("File", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.InvalidOperation(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, move {Source} -> {Destination}", serverId, sourcePath, destinationPath);
            return Result.Failure(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving for server {ServerId}, {Source} -> {Destination}", serverId, sourcePath, destinationPath);
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result> ExtractArchiveInPlaceAsync(
        Guid serverId,
        string archivePath,
        string targetPath,
        CancellationToken ct = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, ct).ConfigureAwait(false);
            if (server is null)
            {
                return Result.Failure(Error.NotFound("Server", serverId.ToString()));
            }

            await _fileManager.ExtractArchiveInPlaceAsync(server, archivePath, targetPath, ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (FileNotFoundException ex)
        {
            return Result.Failure(Error.NotFound("File", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.InvalidOperation(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied for server {ServerId}, extract {ArchivePath}", serverId, archivePath);
            return Result.Failure(Error.Forbidden("Access denied."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting archive for server {ServerId}, {ArchivePath}", serverId, archivePath);
            return Result.Failure(Error.FromException(ex));
        }
    }

    #region Private Methods

    private static FileSystemEntryDto MapToDto(FileSystemEntry entry)
    {
        return new FileSystemEntryDto
        {
            Name = entry.Name,
            Path = entry.Path,
            Type = entry.Type == FileEntryType.Directory ? "directory" : "file",
            Size = entry.Size,
            ModifiedAt = entry.ModifiedAt,
            MimeType = entry.MimeType,
            IsEditable = entry.IsEditable,
            IsHidden = entry.IsHidden,
            Extension = entry.Extension,
            Children = entry.Children?.Select(MapToDto).ToList()
        };
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove path separators and invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // Ensure not empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException("Invalid file name.", nameof(fileName));
        }

        return sanitized.Trim();
    }

    #endregion
}
