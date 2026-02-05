using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

public record ScheduledTaskDto(
    Guid Id,
    Guid ServerId,
    string ServerName,
    string Name,
    ScheduledTaskType TaskType,
    string? CronExpression,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    bool IsEnabled,
    string? Configuration
);

public record ScheduledTaskListItemDto(
    Guid Id,
    Guid ServerId,
    string ServerName,
    string Name,
    ScheduledTaskType TaskType,
    string? CronExpression,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    bool IsEnabled
);

public record CreateScheduledTaskRequest(
    Guid ServerId,
    string Name,
    ScheduledTaskType TaskType,
    string? CronExpression,
    bool IsEnabled = true,
    string? Configuration = null
);

public record UpdateScheduledTaskRequest(
    string Name,
    ScheduledTaskType TaskType,
    string? CronExpression,
    bool IsEnabled,
    string? Configuration
);

// Configuration models for each task type
public record CommandTaskConfig(string Command);

public record UpdateTaskConfig(
    string? Branch = null,
    string? BetaPassword = null,
    bool RestartAfterUpdate = true
);

// BackupTaskConfig is defined in IBackupService.cs
