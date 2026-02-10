using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

public class BackupService(
    IBackupRepository backupRepository,
    IBackupFileManager backupFileManager,
    IGameServerRepository serverRepository,
    IGameServerService serverService,
    IBackupSettings backupSettings,
    ILogger<BackupService> logger) : IBackupService
{
    private readonly IBackupRepository _backupRepository = backupRepository;
    private readonly IBackupFileManager _backupFileManager = backupFileManager;
    private readonly IGameServerRepository _serverRepository = serverRepository;
    private readonly IGameServerService _serverService = serverService;
    private readonly IBackupSettings _settings = backupSettings;
    private readonly ILogger<BackupService> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #region Query Operations

    public async Task<IReadOnlyList<BackupListItemDto>> GetAllBackupsAsync(CancellationToken cancellationToken = default)
    {
        var backups = await _backupRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return backups.Select(MapToListItemDto).ToList();
    }

    public async Task<IReadOnlyList<BackupListItemDto>> GetBackupsByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var backups = await _backupRepository.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        return backups.Select(MapToListItemDto).ToList();
    }

    public async Task<Result<BackupDto>> GetBackupByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var backup = await _backupRepository.GetByIdWithServerAsync(id, cancellationToken).ConfigureAwait(false);
        if (backup is null)
        {
            return Result.Failure<BackupDto>(Error.NotFound("Backup", id.ToString()));
        }
        return MapToDto(backup);
    }

    public async Task<BackupSummaryDto> GetServerBackupSummaryAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var totalCount = await _backupRepository.GetCountByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        var totalSize = await _backupRepository.GetTotalSizeByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        var latestBackup = await _backupRepository.GetLatestCompletedByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        var scheduledCount = await _backupRepository.GetCountByServerIdAndTypeAsync(serverId, BackupType.Scheduled, cancellationToken).ConfigureAwait(false);
        var manualCount = await _backupRepository.GetCountByServerIdAndTypeAsync(serverId, BackupType.Manual, cancellationToken).ConfigureAwait(false);

        // Get full/selective counts by querying the backups
        var backups = await _backupRepository.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        var fullCount = backups.Count(b => b.Scope == BackupScope.Full && b.Status == BackupStatus.Completed);
        var selectiveCount = backups.Count(b => b.Scope == BackupScope.Selective && b.Status == BackupStatus.Completed);

        return new BackupSummaryDto(
            totalCount,
            totalSize,
            latestBackup?.CreatedAt,
            scheduledCount,
            manualCount,
            fullCount,
            selectiveCount
        );
    }

    #endregion

    #region Backup Creation

    public async Task<Result<BackupDto>> CreateFullBackupAsync(
        Guid serverId,
        string? name = null,
        string? notes = null,
        BackupType type = BackupType.Manual,
        Guid? scheduledTaskId = null,
        CancellationToken cancellationToken = default)
    {
        return await CreateBackupInternalAsync(
            serverId,
            BackupScope.Full,
            null,
            name,
            notes,
            type,
            scheduledTaskId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<BackupDto>> CreateSelectiveBackupAsync(
        Guid serverId,
        IEnumerable<string> relativePaths,
        string? name = null,
        string? notes = null,
        BackupType type = BackupType.Manual,
        Guid? scheduledTaskId = null,
        CancellationToken cancellationToken = default)
    {
        var paths = relativePaths?.ToArray();
        if (paths is null || paths.Length == 0)
        {
            return Result.Failure<BackupDto>(Error.Validation("At least one path must be specified for a selective backup."));
        }

        return await CreateBackupInternalAsync(
            serverId,
            BackupScope.Selective,
            paths,
            name,
            notes,
            type,
            scheduledTaskId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<BackupDto>> CreateBackupAsync(
        CreateBackupRequest request,
        BackupType type = BackupType.Manual,
        Guid? scheduledTaskId = null,
        CancellationToken cancellationToken = default)
    {
        return request.Scope switch
        {
            BackupScope.Full => await CreateFullBackupAsync(
                request.ServerId,
                request.Name,
                request.Notes,
                type,
                scheduledTaskId,
                cancellationToken).ConfigureAwait(false),

            BackupScope.Selective => await CreateSelectiveBackupAsync(
                request.ServerId,
                request.SelectivePaths ?? [],
                request.Name,
                request.Notes,
                type,
                scheduledTaskId,
                cancellationToken).ConfigureAwait(false),

            _ => Result.Failure<BackupDto>(Error.Validation($"Unknown backup scope: {request.Scope}"))
        };
    }

    private async Task<Result<BackupDto>> CreateBackupInternalAsync(
        Guid serverId,
        BackupScope scope,
        string[]? selectivePaths,
        string? name,
        string? notes,
        BackupType type,
        Guid? scheduledTaskId,
        CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure<BackupDto>(Error.NotFound("Server", serverId.ToString()));
        }

        if (string.IsNullOrEmpty(server.InstallPath) || !Directory.Exists(server.InstallPath))
        {
            return Result.Failure<BackupDto>(Error.InvalidOperation($"Server install path does not exist: {server.InstallPath}"));
        }

        // Generate backup name if not provided
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var scopeSuffix = scope == BackupScope.Full ? "Full" : "Selective";
        var sanitizedServerName = SanitizeFileName(server.Name);
        var backupName = name ?? $"{sanitizedServerName}_{timestamp}_{scopeSuffix}";

        // Create backup entity first
        var backup = new Backup
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            Name = backupName,
            FilePath = "", // Will be set after file creation
            FileSizeBytes = 0,
            CreatedAt = DateTime.UtcNow,
            Type = type,
            Status = BackupStatus.InProgress,
            Scope = scope,
            SourcePaths = selectivePaths is not null ? JsonSerializer.Serialize(selectivePaths, JsonOptions) : null,
            ScheduledTaskId = scheduledTaskId,
            Notes = notes,
            IncludesWorld = true,
            IncludesConfig = true,
            IncludesMods = scope == BackupScope.Full
        };

        await _backupRepository.AddAsync(backup, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Creating {Scope} backup '{BackupName}' for server {ServerName} ({ServerId})",
            scope, backupName, server.Name, serverId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var backupDirectory = Path.Combine(_settings.GetResolvedBackupDirectory(), serverId.ToString());
            _backupFileManager.EnsureBackupDirectoryExists(backupDirectory);

            var backupFilePath = await _backupFileManager.CreateBackupArchiveAsync(
                server,
                backupDirectory,
                backupName,
                selectivePaths,
                cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            // Update backup with file info
            backup.FilePath = backupFilePath;
            backup.FileSizeBytes = _backupFileManager.GetBackupFileSize(backupFilePath);
            backup.Status = BackupStatus.Completed;

            await _backupRepository.UpdateAsync(backup, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Backup '{BackupName}' completed in {Duration:N1}s, size: {Size}",
                backupName, stopwatch.Elapsed.TotalSeconds, FormatFileSize(backup.FileSizeBytes));

            // Reload to get server info
            backup = await _backupRepository.GetByIdWithServerAsync(backup.Id, cancellationToken).ConfigureAwait(false);
            return MapToDto(backup!);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Failed to create backup '{BackupName}' for server {ServerName}",
                backupName, server.Name);

            // Mark backup as failed
            backup.Status = BackupStatus.Failed;
            backup.Notes = $"{notes ?? ""}\nError: {ex.Message}".Trim();
            await _backupRepository.UpdateAsync(backup, cancellationToken).ConfigureAwait(false);

            return Result.Failure<BackupDto>(Error.FromException(ex));
        }
    }

    #endregion

    #region Backup Operations

    public async Task<Result> RestoreBackupAsync(
        Guid backupId,
        bool stopServerFirst = true,
        bool createPreRestoreBackup = true,
        CancellationToken cancellationToken = default)
    {
        var backup = await _backupRepository.GetByIdWithServerAsync(backupId, cancellationToken).ConfigureAwait(false);
        if (backup is null)
        {
            return Result.Failure(Error.NotFound("Backup", backupId.ToString()));
        }

        if (backup.Status != BackupStatus.Completed)
        {
            return Result.Failure(Error.InvalidOperation($"Cannot restore backup with status '{backup.Status}'. Only completed backups can be restored."));
        }

        if (!_backupFileManager.BackupFileExists(backup.FilePath))
        {
            return Result.Failure(Error.NotFound("Backup file", backup.FilePath));
        }

        var server = backup.Server;

        _logger.LogInformation(
            "Restoring backup '{BackupName}' to server {ServerName} ({ServerId})",
            backup.Name, server.Name, server.Id);

        // Stop server if requested and running
        var wasRunning = server.Status == ServerStatus.Running;
        if (stopServerFirst && wasRunning)
        {
            _logger.LogInformation("Stopping server {ServerName} before restore", server.Name);
            var stopResult = await _serverService.StopServerAsync(server.Id, cancellationToken).ConfigureAwait(false);
            if (stopResult.IsFailure)
            {
                return Result.Failure(Error.InvalidOperation($"Failed to stop server before restore: {stopResult.Error}"));
            }

            // Wait for server to fully stop
            await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            // Create pre-restore backup if requested
            if (createPreRestoreBackup && _settings.CreatePreRestoreBackup)
            {
                _logger.LogInformation("Creating pre-restore backup for server {ServerName}", server.Name);
                var preRestoreResult = await CreateFullBackupAsync(
                    server.Id,
                    $"PreRestore_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                    $"Automatic backup created before restoring '{backup.Name}'",
                    BackupType.PreRestore,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (preRestoreResult.IsFailure)
                {
                    _logger.LogWarning("Failed to create pre-restore backup: {Error}", preRestoreResult.Error);
                    // Continue with restore anyway
                }
            }

            // Extract backup
            await _backupFileManager.ExtractBackupArchiveAsync(server, backup.FilePath, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Backup '{BackupName}' restored successfully to server {ServerName}",
                backup.Name, server.Name);

            // Restart server if it was running
            if (wasRunning)
            {
                _logger.LogInformation("Restarting server {ServerName} after restore", server.Name);
                await _serverService.StartServerAsync(server.Id, cancellationToken).ConfigureAwait(false);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore backup '{BackupName}' to server {ServerName}",
                backup.Name, server.Name);
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result> DeleteBackupAsync(Guid backupId, CancellationToken cancellationToken = default)
    {
        var backup = await _backupRepository.GetByIdAsync(backupId, cancellationToken).ConfigureAwait(false);
        if (backup is null)
        {
            return Result.Failure(Error.NotFound("Backup", backupId.ToString()));
        }

        _logger.LogInformation("Deleting backup '{BackupName}' ({BackupId})", backup.Name, backupId);

        try
        {
            // Delete file if it exists
            if (_backupFileManager.BackupFileExists(backup.FilePath))
            {
                await _backupFileManager.DeleteBackupFileAsync(backup.FilePath, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Delete database record
            await _backupRepository.DeleteAsync(backupId, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Backup '{BackupName}' deleted successfully", backup.Name);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete backup '{BackupName}'", backup.Name);
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result<Stream>> DownloadBackupAsync(Guid backupId, CancellationToken cancellationToken = default)
    {
        var backup = await _backupRepository.GetByIdAsync(backupId, cancellationToken).ConfigureAwait(false);
        if (backup is null)
        {
            return Result.Failure<Stream>(Error.NotFound("Backup", backupId.ToString()));
        }

        if (!_backupFileManager.BackupFileExists(backup.FilePath))
        {
            return Result.Failure<Stream>(Error.NotFound("Backup file", backup.FilePath));
        }

        var stream = await _backupFileManager.OpenBackupStreamAsync(backup.FilePath, cancellationToken)
            .ConfigureAwait(false);

        return stream;
    }

    public async Task<Result<IReadOnlyList<string>>> GetBackupContentsAsync(Guid backupId, CancellationToken cancellationToken = default)
    {
        var backup = await _backupRepository.GetByIdAsync(backupId, cancellationToken).ConfigureAwait(false);
        if (backup is null)
        {
            return Result.Failure<IReadOnlyList<string>>(Error.NotFound("Backup", backupId.ToString()));
        }

        if (!_backupFileManager.BackupFileExists(backup.FilePath))
        {
            return Result.Failure<IReadOnlyList<string>>(Error.NotFound("Backup file", backup.FilePath));
        }

        var contents = await _backupFileManager.GetBackupContentsAsync(backup.FilePath, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(contents);
    }

    #endregion

    #region Retention and Cleanup

    public async Task<Result<int>> ApplyRetentionPolicyAsync(
        Guid serverId,
        int keepCount,
        CancellationToken cancellationToken = default)
    {
        if (keepCount < 1)
        {
            return Result.Failure<int>(Error.Validation("Keep count must be at least 1."));
        }

        _logger.LogInformation(
            "Applying retention policy for server {ServerId}: keeping {KeepCount} backups",
            serverId, keepCount);

        try
        {
            // Get backups to delete
            var backupsToDelete = await _backupRepository
                .GetBackupsToDeleteForRetentionAsync(serverId, keepCount, cancellationToken)
                .ConfigureAwait(false);

            if (backupsToDelete.Count == 0)
            {
                _logger.LogDebug("No backups to delete for retention policy");
                return 0;
            }

            var deletedCount = 0;
            foreach (var backup in backupsToDelete)
            {
                try
                {
                    // Delete file
                    if (_backupFileManager.BackupFileExists(backup.FilePath))
                    {
                        await _backupFileManager.DeleteBackupFileAsync(backup.FilePath, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    // Delete record
                    await _backupRepository.DeleteAsync(backup.Id, cancellationToken).ConfigureAwait(false);
                    deletedCount++;

                    _logger.LogDebug("Deleted old backup: {BackupName}", backup.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete backup {BackupName} during retention cleanup", backup.Name);
                }
            }

            _logger.LogInformation("Retention policy applied: deleted {DeletedCount} backups", deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply retention policy for server {ServerId}", serverId);
            return Result.Failure<int>(Error.FromException(ex));
        }
    }

    #endregion

    #region Scheduled Task Integration

    public async Task<Result> ExecuteScheduledBackupAsync(
        Guid serverId,
        BackupTaskConfig config,
        Guid scheduledTaskId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing scheduled backup for server {ServerId} with scope {Scope}",
            serverId, config.Scope);

        try
        {
            // Create backup based on configuration
            Result<BackupDto> backupResult;

            if (config.Scope == BackupScope.Selective)
            {
                // Get selective paths from config or use game defaults
                var selectivePaths = config.SelectivePaths;

                if (selectivePaths is null || selectivePaths.Length == 0)
                {
                    // Get game-specific default paths
                    var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken)
                        .ConfigureAwait(false);

                    if (server?.Game?.Slug is not null)
                    {
                        selectivePaths = _settings.GetSelectivePathsForGame(server.Game.Slug);
                    }
                }

                if (selectivePaths?.Length > 0)
                {
                    backupResult = await CreateSelectiveBackupAsync(
                        serverId,
                        selectivePaths,
                        type: BackupType.Scheduled,
                        scheduledTaskId: scheduledTaskId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Fallback to full backup if no selective paths available
                    _logger.LogWarning(
                        "No selective paths configured for server {ServerId}, falling back to full backup",
                        serverId);
                    backupResult = await CreateFullBackupAsync(
                        serverId,
                        type: BackupType.Scheduled,
                        scheduledTaskId: scheduledTaskId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                backupResult = await CreateFullBackupAsync(
                    serverId,
                    type: BackupType.Scheduled,
                    scheduledTaskId: scheduledTaskId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            if (backupResult.IsFailure)
            {
                return Result.Failure(backupResult.Error!);
            }

            // Apply retention policy if configured
            if (config.RetentionCount.HasValue && config.RetentionCount.Value > 0)
            {
                await ApplyRetentionPolicyAsync(serverId, config.RetentionCount.Value, cancellationToken)
                    .ConfigureAwait(false);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled backup failed for server {ServerId}", serverId);
            return Result.Failure(Error.FromException(ex));
        }
    }

    #endregion

    #region Utility

    public string[] GetDefaultSelectivePathsForGame(string gameSlug)
    {
        return _settings.GetSelectivePathsForGame(gameSlug);
    }

    #endregion

    #region Private Methods

    private static BackupDto MapToDto(Backup backup)
    {
        string[]? sourcePaths = null;
        if (!string.IsNullOrEmpty(backup.SourcePaths))
        {
            try
            {
                sourcePaths = JsonSerializer.Deserialize<string[]>(backup.SourcePaths);
            }
            catch
            {
                // Ignore deserialization errors
            }
        }

        return new BackupDto(
            backup.Id,
            backup.ServerId,
            backup.Server?.Name ?? "Unknown",
            backup.Name,
            backup.FilePath,
            backup.FileSizeBytes,
            backup.CreatedAt,
            backup.Type,
            backup.Status,
            backup.Scope,
            backup.Notes,
            backup.IncludesWorld,
            backup.IncludesConfig,
            backup.IncludesMods,
            sourcePaths,
            backup.ScheduledTaskId
        );
    }

    private static BackupListItemDto MapToListItemDto(Backup backup) => new(
        backup.Id,
        backup.ServerId,
        backup.Server?.Name ?? "Unknown",
        backup.FileSizeBytes,
        backup.Name,
        backup.CreatedAt,
        backup.Type,
        backup.Status,
        backup.Scope
    );

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return sanitized.Replace(' ', '_');
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    #endregion
}
