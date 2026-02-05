namespace NebulaPanel.Application.DTOs;

/// <summary>
/// DTO for update schedule display.
/// </summary>
public record UpdateScheduleDto(
    Guid Id,
    string TargetVersion,
    DateTime ScheduledAt,
    DateTime CreatedAt,
    string CreatedByUsername,
    bool CreateBackup,
    bool StopGameServers,
    bool RestartGameServers,
    bool IsCancelled,
    DateTime? CancelledAt,
    string? CancelledByUsername,
    bool IsExecuted,
    DateTime? ExecutedAt,
    string? ReleaseNotes,
    bool CanCancel);

/// <summary>
/// DTO for update history display.
/// </summary>
public record UpdateHistoryDto(
    Guid Id,
    string FromVersion,
    string ToVersion,
    DateTime StartedAt,
    DateTime? CompletedAt,
    bool Success,
    string? ErrorMessage,
    string? InitiatedByUsername,
    bool WasScheduled,
    bool WasRolledBack,
    DateTime? RolledBackAt);

/// <summary>
/// Request to schedule an update.
/// </summary>
public record ScheduleUpdateRequest(
    string Version,
    DateTime ScheduledAt,
    bool CreateBackup = true,
    bool StopGameServers = true,
    bool RestartGameServers = true);

/// <summary>
/// Result of a scheduled update creation.
/// </summary>
public record ScheduleUpdateResult(
    Guid ScheduleId,
    string TargetVersion,
    DateTime ScheduledAt);

/// <summary>
/// Post-update completion information stored in a marker file.
/// </summary>
public record UpdateCompletionMarker(
    Guid HistoryId,
    string FromVersion,
    string ToVersion,
    bool Success,
    string? ErrorMessage,
    string? ReleaseNotes,
    DateTime CompletedAt);
