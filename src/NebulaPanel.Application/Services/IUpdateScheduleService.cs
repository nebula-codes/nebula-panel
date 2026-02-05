using NebulaPanel.Application.Common;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for scheduling panel updates.
/// </summary>
public interface IUpdateScheduleService
{
    /// <summary>
    /// Schedules an update for a specific time.
    /// </summary>
    Task<Result<UpdateSchedule>> ScheduleUpdateAsync(
        string version,
        DateTime scheduledAt,
        UpdateOptions options,
        Guid userId,
        string? releaseNotes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a scheduled update.
    /// </summary>
    Task<Result> CancelScheduledUpdateAsync(
        Guid scheduleId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the pending (not cancelled, not executed) schedule.
    /// </summary>
    Task<UpdateSchedule?> GetPendingScheduleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets schedule history including executed and cancelled schedules.
    /// </summary>
    Task<IReadOnlyList<UpdateSchedule>> GetScheduleHistoryAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a schedule by ID.
    /// </summary>
    Task<UpdateSchedule?> GetByIdAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes due schedules. Called by the background service.
    /// </summary>
    Task ProcessDueSchedulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the time until the next scheduled update, if any.
    /// </summary>
    Task<TimeSpan?> GetTimeUntilNextScheduleAsync(CancellationToken cancellationToken = default);
}
