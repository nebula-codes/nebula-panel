using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.FileManagement;

/// <summary>
/// Service for relocating game server installations to new directories.
/// </summary>
public class ServerRelocationService : IServerRelocationService
{
    private readonly IGameServerRepository _serverRepository;
    private readonly NebulaPanelDbContext _dbContext;
    private readonly IServerRelocationNotifier _notifier;
    private readonly ILogger<ServerRelocationService> _logger;

    // In-memory tracking of active relocations
    private static readonly ConcurrentDictionary<Guid, ServerRelocationJob> _activeJobs = new();

    public ServerRelocationService(
        IGameServerRepository serverRepository,
        NebulaPanelDbContext dbContext,
        IServerRelocationNotifier notifier,
        ILogger<ServerRelocationService> logger)
    {
        _serverRepository = serverRepository;
        _dbContext = dbContext;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<Result<ServerRelocationValidation>> ValidateRelocationAsync(
        Guid serverId,
        string newPath,
        CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
            return Result.Failure<ServerRelocationValidation>($"Server with ID '{serverId}' not found.");

        // Check if server is stopped
        if (server.Status != ServerStatus.Stopped)
            return Result.Failure<ServerRelocationValidation>("Server must be stopped before relocation.");

        // Check if there's already an active relocation for this server
        if (_activeJobs.Values.Any(j => j.ServerId == serverId && !j.IsCompleted))
            return Result.Failure<ServerRelocationValidation>("A relocation is already in progress for this server.");

        var oldPath = server.InstallPath;

        // Validate paths
        var pathValidation = ValidatePaths(oldPath, newPath);
        if (!pathValidation.IsSuccess)
            return Result.Failure<ServerRelocationValidation>(pathValidation.Error!);

        // Check if source directory exists
        if (!Directory.Exists(oldPath))
            return Result.Failure<ServerRelocationValidation>($"Source directory does not exist: {oldPath}");

        // Calculate total size and file count
        long totalSize = 0;
        int fileCount = 0;

        try
        {
            var dirInfo = new DirectoryInfo(oldPath);
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                totalSize += file.Length;
                fileCount++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate files in {Path}", oldPath);
            return Result.Failure<ServerRelocationValidation>($"Failed to access source directory: {ex.Message}");
        }

        // Check available space on destination drive
        long availableSpace = 0;
        try
        {
            var destDir = Path.GetDirectoryName(newPath) ?? newPath;
            // Create parent directory if it doesn't exist to check drive info
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            var driveInfo = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(newPath))!);
            availableSpace = driveInfo.AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check available space for {Path}", newPath);
            return Result.Failure<ServerRelocationValidation>($"Failed to check destination disk space: {ex.Message}");
        }

        // Ensure enough space (with 10% buffer)
        var requiredSpace = (long)(totalSize * 1.1);
        if (availableSpace < requiredSpace)
        {
            return Result.Success(new ServerRelocationValidation(
                IsValid: false,
                ErrorMessage: $"Insufficient disk space. Required: {FormatBytes(requiredSpace)}, Available: {FormatBytes(availableSpace)}",
                TotalSizeBytes: totalSize,
                TotalFileCount: fileCount,
                AvailableSpaceBytes: availableSpace,
                OldPath: oldPath,
                NewPath: newPath
            ));
        }

        // Check if destination already exists
        if (Directory.Exists(newPath))
        {
            var existingFiles = Directory.EnumerateFileSystemEntries(newPath).Any();
            if (existingFiles)
            {
                return Result.Success(new ServerRelocationValidation(
                    IsValid: false,
                    ErrorMessage: "Destination directory already exists and is not empty.",
                    TotalSizeBytes: totalSize,
                    TotalFileCount: fileCount,
                    AvailableSpaceBytes: availableSpace,
                    OldPath: oldPath,
                    NewPath: newPath
                ));
            }
        }

        return Result.Success(new ServerRelocationValidation(
            IsValid: true,
            ErrorMessage: null,
            TotalSizeBytes: totalSize,
            TotalFileCount: fileCount,
            AvailableSpaceBytes: availableSpace,
            OldPath: oldPath,
            NewPath: newPath
        ));
    }

    public async Task<Result<Guid>> StartRelocationAsync(
        Guid serverId,
        string newPath,
        bool deleteOldFiles = true,
        CancellationToken cancellationToken = default)
    {
        // Validate first
        var validationResult = await ValidateRelocationAsync(serverId, newPath, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsSuccess)
            return Result.Failure<Guid>(validationResult.Error!);

        var validation = validationResult.Value!;
        if (!validation.IsValid)
            return Result.Failure<Guid>(validation.ErrorMessage ?? "Validation failed");

        var relocationId = Guid.NewGuid();

        // Create and register the job
        var job = new ServerRelocationJob(
            relocationId,
            serverId,
            validation.OldPath,
            newPath,
            deleteOldFiles,
            validation.TotalSizeBytes,
            validation.TotalFileCount,
            _dbContext,
            _notifier,
            _logger);

        _activeJobs[relocationId] = job;

        _logger.LogInformation("Starting relocation for server {ServerId}: {OldPath} -> {NewPath}",
            serverId, validation.OldPath, newPath);

        // Start the job in the background
        _ = Task.Run(async () =>
        {
            try
            {
                await job.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Server relocation job {RelocationId} failed with exception", relocationId);
            }
        }, CancellationToken.None);

        return Result.Success(relocationId);
    }

    public Task<Result<ServerRelocationStatus>> GetRelocationStatusAsync(
        Guid relocationId,
        CancellationToken cancellationToken = default)
    {
        if (!_activeJobs.TryGetValue(relocationId, out var job))
            return Task.FromResult(Result.Failure<ServerRelocationStatus>($"Relocation job '{relocationId}' not found."));

        var status = new ServerRelocationStatus(
            job.RelocationId,
            job.ServerId,
            job.OldPath,
            job.NewPath,
            job.CurrentProgress,
            job.StartedAt,
            job.CompletedAt);

        return Task.FromResult(Result.Success(status));
    }

    public Task<Result> CancelRelocationAsync(
        Guid relocationId,
        CancellationToken cancellationToken = default)
    {
        if (!_activeJobs.TryGetValue(relocationId, out var job))
            return Task.FromResult(Result.Failure($"Relocation job '{relocationId}' not found."));

        if (job.IsCompleted)
            return Task.FromResult(Result.Failure("Relocation has already completed."));

        job.Cancel();
        _logger.LogInformation("Cancellation requested for relocation job {RelocationId}", relocationId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<ServerRelocationStatus?>> GetActiveRelocationForServerAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var job = _activeJobs.Values.FirstOrDefault(j => j.ServerId == serverId && !j.IsCompleted);
        if (job is null)
            return Task.FromResult(Result.Success<ServerRelocationStatus?>(null));

        var status = new ServerRelocationStatus(
            job.RelocationId,
            job.ServerId,
            job.OldPath,
            job.NewPath,
            job.CurrentProgress,
            job.StartedAt,
            job.CompletedAt);

        return Task.FromResult(Result.Success<ServerRelocationStatus?>(status));
    }

    private static Result ValidatePaths(string oldPath, string newPath)
    {
        // Normalize paths
        oldPath = Path.GetFullPath(oldPath);
        newPath = Path.GetFullPath(newPath);

        // Prevent path traversal
        if (newPath.Contains(".."))
            return Result.Failure("Invalid path: path traversal detected.");

        // Prevent relocating to same location
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            return Result.Failure("New path cannot be the same as the current path.");

        // Prevent relocating to subdirectory of itself
        if (newPath.StartsWith(oldPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Cannot relocate to a subdirectory of the current location.");

        // Prevent relocating from subdirectory of new path
        if (oldPath.StartsWith(newPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Cannot relocate to a parent directory of the current location.");

        // Check if path is on a valid drive/mount
        var root = Path.GetPathRoot(newPath);
        if (string.IsNullOrEmpty(root))
            return Result.Failure("Invalid path: no root specified.");

        return Result.Success();
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int i = 0;
        double dblBytes = bytes;
        while (dblBytes >= 1024 && i < suffixes.Length - 1)
        {
            dblBytes /= 1024;
            i++;
        }
        return $"{dblBytes:0.##} {suffixes[i]}";
    }
}

/// <summary>
/// Represents a running server relocation job.
/// </summary>
internal class ServerRelocationJob
{
    private readonly NebulaPanelDbContext _dbContext;
    private readonly IServerRelocationNotifier _notifier;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();

    public Guid RelocationId { get; }
    public Guid ServerId { get; }
    public string OldPath { get; }
    public string NewPath { get; }
    public bool DeleteOldFiles { get; }
    public long TotalBytes { get; }
    public int TotalFiles { get; }
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }
    public bool IsCompleted { get; private set; }
    public ServerRelocationProgress CurrentProgress { get; private set; } = ServerRelocationProgress.Validating();

    public ServerRelocationJob(
        Guid relocationId,
        Guid serverId,
        string oldPath,
        string newPath,
        bool deleteOldFiles,
        long totalBytes,
        int totalFiles,
        NebulaPanelDbContext dbContext,
        IServerRelocationNotifier notifier,
        ILogger logger)
    {
        RelocationId = relocationId;
        ServerId = serverId;
        OldPath = oldPath;
        NewPath = newPath;
        DeleteOldFiles = deleteOldFiles;
        TotalBytes = totalBytes;
        TotalFiles = totalFiles;
        _dbContext = dbContext;
        _notifier = notifier;
        _logger = logger;
    }

    public void Cancel() => _cts.Cancel();

    public async Task ExecuteAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken);
        var token = linkedCts.Token;

        try
        {
            await UpdateProgressAsync(ServerRelocationProgress.CountingFiles(), token).ConfigureAwait(false);

            // Create destination directory
            Directory.CreateDirectory(NewPath);

            // Copy files with progress
            long bytesCopied = 0;
            int filesCopied = 0;

            var files = Directory.EnumerateFiles(OldPath, "*", SearchOption.AllDirectories).ToList();
            var directories = Directory.EnumerateDirectories(OldPath, "*", SearchOption.AllDirectories).ToList();

            // Create directory structure first
            foreach (var dir in directories)
            {
                token.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(OldPath, dir);
                var newDir = Path.Combine(NewPath, relativePath);
                Directory.CreateDirectory(newDir);
            }

            // Copy files
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(OldPath, file);
                var destFile = Path.Combine(NewPath, relativePath);

                // Ensure parent directory exists
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                // Copy file
                var fileInfo = new FileInfo(file);
                await CopyFileAsync(file, destFile, token).ConfigureAwait(false);
                bytesCopied += fileInfo.Length;
                filesCopied++;

                // Update progress every 10 files or 10MB
                if (filesCopied % 10 == 0 || bytesCopied % (10 * 1024 * 1024) < fileInfo.Length)
                {
                    await UpdateProgressAsync(
                        ServerRelocationProgress.CopyingFiles(relativePath, bytesCopied, TotalBytes, filesCopied, TotalFiles),
                        token).ConfigureAwait(false);
                }
            }

            // Final file copy progress
            await UpdateProgressAsync(
                ServerRelocationProgress.CopyingFiles(null, bytesCopied, TotalBytes, filesCopied, TotalFiles),
                token).ConfigureAwait(false);

            // Update database configuration
            await UpdateProgressAsync(ServerRelocationProgress.UpdatingConfiguration(), token).ConfigureAwait(false);
            await UpdateServerConfigurationAsync(token).ConfigureAwait(false);

            // Clean up old directory
            if (DeleteOldFiles)
            {
                await UpdateProgressAsync(ServerRelocationProgress.CleaningUp(), token).ConfigureAwait(false);
                await DeleteDirectoryAsync(OldPath, token).ConfigureAwait(false);
            }

            await UpdateProgressAsync(ServerRelocationProgress.Completed(), token).ConfigureAwait(false);
            await _notifier.NotifyCompleteAsync(RelocationId, ServerId, true, null, token).ConfigureAwait(false);

            _logger.LogInformation("Server relocation completed successfully: {ServerId} from {OldPath} to {NewPath}",
                ServerId, OldPath, NewPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Server relocation cancelled: {RelocationId}", RelocationId);
            await RollbackAsync().ConfigureAwait(false);
            await UpdateProgressAsync(ServerRelocationProgress.Failed("Relocation was cancelled."), CancellationToken.None).ConfigureAwait(false);
            await _notifier.NotifyCompleteAsync(RelocationId, ServerId, false, "Relocation was cancelled.", CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Server relocation failed: {RelocationId}", RelocationId);
            await RollbackAsync().ConfigureAwait(false);
            await UpdateProgressAsync(ServerRelocationProgress.Failed(ex.Message), CancellationToken.None).ConfigureAwait(false);
            await _notifier.NotifyCompleteAsync(RelocationId, ServerId, false, ex.Message, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            CompletedAt = DateTime.UtcNow;
            IsCompleted = true;
        }
    }

    private async Task UpdateProgressAsync(ServerRelocationProgress progress, CancellationToken token)
    {
        CurrentProgress = progress;
        var dto = ServerRelocationProgressDto.FromProgress(RelocationId, ServerId, progress);
        await _notifier.NotifyProgressAsync(RelocationId, ServerId, dto, token).ConfigureAwait(false);
    }

    private async Task UpdateServerConfigurationAsync(CancellationToken token)
    {
        var server = await _dbContext.GameServers
            .Include(s => s.Game)
            .FirstOrDefaultAsync(s => s.Id == ServerId, token)
            .ConfigureAwait(false);

        if (server is null)
        {
            throw new InvalidOperationException($"Server {ServerId} not found in database.");
        }

        var oldPathNormalized = Path.GetFullPath(OldPath);
        var newPathNormalized = Path.GetFullPath(NewPath);

        // Update InstallPath
        server.InstallPath = newPathNormalized;

        // Update NativeConfig paths if they reference the old path
        if (server.NativeConfig is not null)
        {
            if (!string.IsNullOrEmpty(server.NativeConfig.WorkingDirectory))
            {
                server.NativeConfig.WorkingDirectory = UpdatePath(server.NativeConfig.WorkingDirectory, oldPathNormalized, newPathNormalized);
            }
            if (!string.IsNullOrEmpty(server.NativeConfig.ExecutablePath))
            {
                // ExecutablePath might be absolute or relative
                if (Path.IsPathRooted(server.NativeConfig.ExecutablePath))
                {
                    server.NativeConfig.ExecutablePath = UpdatePath(server.NativeConfig.ExecutablePath, oldPathNormalized, newPathNormalized);
                }
            }
            if (server.NativeConfig.UseStartupScript && !string.IsNullOrEmpty(server.NativeConfig.StartupScriptPath))
            {
                if (Path.IsPathRooted(server.NativeConfig.StartupScriptPath))
                {
                    server.NativeConfig.StartupScriptPath = UpdatePath(server.NativeConfig.StartupScriptPath, oldPathNormalized, newPathNormalized);
                }
            }
        }

        // Update DockerConfig volumes if they reference the old path
        if (server.DockerConfig is not null)
        {
            // Update volumes
            foreach (var volume in server.DockerConfig.Volumes.Where(v => !v.IsNamedVolume))
            {
                if (!string.IsNullOrEmpty(volume.HostPath) &&
                    volume.HostPath.StartsWith(oldPathNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    volume.HostPath = newPathNormalized + volume.HostPath[oldPathNormalized.Length..];
                }
            }

            // Clear DockerContainerId to force recreation on next start
            if (!string.IsNullOrEmpty(server.DockerContainerId))
            {
                await UpdateProgressAsync(ServerRelocationProgress.RecreatingContainer(), token).ConfigureAwait(false);
                server.DockerContainerId = null;
                _logger.LogInformation("Cleared Docker container ID for server {ServerId} to force recreation", ServerId);
            }
        }

        await _dbContext.SaveChangesAsync(token).ConfigureAwait(false);
        _logger.LogInformation("Updated server configuration for {ServerId}", ServerId);
    }

    private static string UpdatePath(string path, string oldBase, string newBase)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        var normalizedPath = Path.GetFullPath(path);
        if (normalizedPath.StartsWith(oldBase, StringComparison.OrdinalIgnoreCase))
        {
            return newBase + normalizedPath[oldBase.Length..];
        }
        return path;
    }

    private async Task RollbackAsync()
    {
        try
        {
            await UpdateProgressAsync(ServerRelocationProgress.RollingBack(), CancellationToken.None).ConfigureAwait(false);

            // Delete partially copied files
            if (Directory.Exists(NewPath))
            {
                await DeleteDirectoryAsync(NewPath, CancellationToken.None).ConfigureAwait(false);
            }

            _logger.LogInformation("Rolled back relocation for server {ServerId}", ServerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback relocation for server {ServerId}", ServerId);
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken token)
    {
        const int bufferSize = 81920; // 80KB buffer
        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await sourceStream.CopyToAsync(destStream, bufferSize, token).ConfigureAwait(false);

        // Preserve file attributes
        var sourceInfo = new FileInfo(source);
        File.SetLastWriteTimeUtc(destination, sourceInfo.LastWriteTimeUtc);
    }

    private static Task DeleteDirectoryAsync(string path, CancellationToken token)
    {
        return Task.Run(() =>
        {
            if (Directory.Exists(path))
            {
                // Remove read-only attributes from files
                var dirInfo = new DirectoryInfo(path);
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    if (file.IsReadOnly)
                        file.IsReadOnly = false;
                }

                Directory.Delete(path, true);
            }
        }, token);
    }
}
