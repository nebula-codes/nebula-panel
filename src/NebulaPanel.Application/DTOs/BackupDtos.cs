using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Full backup details DTO.
/// </summary>
public record BackupDto(
    Guid Id,
    Guid ServerId,
    string ServerName,
    string Name,
    string FilePath,
    long FileSizeBytes,
    DateTime CreatedAt,
    BackupType Type,
    BackupStatus Status,
    BackupScope Scope,
    string? Notes,
    bool IncludesWorld,
    bool IncludesConfig,
    bool IncludesMods,
    string[]? SourcePaths,
    Guid? ScheduledTaskId
);

/// <summary>
/// Backup list item DTO for display in lists/grids.
/// </summary>
public record BackupListItemDto(
    Guid Id,
    Guid ServerId,
    string ServerName,
    long FileSizeBytes,
    string Name,
    DateTime CreatedAt,
    BackupType Type,
    BackupStatus Status,
    BackupScope Scope
);

/// <summary>
/// Request DTO for creating a backup.
/// </summary>
public record CreateBackupRequest(
    Guid ServerId,
    BackupScope Scope,
    string? Name = null,
    string? Notes = null,
    string[]? SelectivePaths = null
);

/// <summary>
/// Summary statistics for a server's backups.
/// </summary>
public record BackupSummaryDto(
    int TotalCount,
    long TotalSizeBytes,
    DateTime? LastBackupAt,
    int ScheduledCount,
    int ManualCount,
    int FullCount,
    int SelectiveCount
);

/// <summary>
/// Result of a backup creation operation.
/// </summary>
public record BackupCreationResult(
    Guid BackupId,
    string FilePath,
    long FileSizeBytes,
    TimeSpan Duration
);
