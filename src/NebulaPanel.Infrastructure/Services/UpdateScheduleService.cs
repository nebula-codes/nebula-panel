using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Service for scheduling panel updates.
/// </summary>
public class UpdateScheduleService(
    NebulaPanelDbContext dbContext,
    IUpdateService updateService,
    IUpdateHistoryService historyService,
    IUpdateNotifier notifier,
    ILogger<UpdateScheduleService> logger) : IUpdateScheduleService
{
    /// <inheritdoc />
    public async Task<Result<UpdateSchedule>> ScheduleUpdateAsync(
        string version,
        DateTime scheduledAt,
        UpdateOptions options,
        Guid userId,
        string? releaseNotes,
        CancellationToken cancellationToken = default)
    {
        // Validate scheduled time is in the future
        if (scheduledAt <= DateTime.UtcNow)
        {
            return Result.Failure<UpdateSchedule>("Scheduled time must be in the future");
        }

        // Check if there's already a pending schedule
        var existing = await GetPendingScheduleAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Failure<UpdateSchedule>(
                $"An update to version {existing.TargetVersion} is already scheduled for {existing.ScheduledAt:g}");
        }

        // Verify the version exists
        var releaseInfo = await updateService.GetReleaseInfoAsync(version, cancellationToken)
            .ConfigureAwait(false);
        if (releaseInfo is null)
        {
            return Result.Failure<UpdateSchedule>($"Version {version} not found");
        }

        var schedule = new UpdateSchedule
        {
            Id = Guid.NewGuid(),
            TargetVersion = version,
            ScheduledAt = scheduledAt,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
            CreateBackup = options.CreateBackup,
            StopGameServers = options.StopGameServers,
            RestartGameServers = options.RestartGameServers,
            ReleaseNotes = releaseNotes ?? releaseInfo.Body
        };

        dbContext.UpdateSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Scheduled update to version {Version} at {ScheduledAt} by user {UserId}",
            version, scheduledAt, userId);

        return Result.Success(schedule);
    }

    /// <inheritdoc />
    public async Task<Result> CancelScheduledUpdateAsync(
        Guid scheduleId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await dbContext.UpdateSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
            .ConfigureAwait(false);

        if (schedule is null)
        {
            return Result.Failure("Scheduled update not found");
        }

        if (schedule.IsExecuted)
        {
            return Result.Failure("Cannot cancel an update that has already been executed");
        }

        if (schedule.IsCancelled)
        {
            return Result.Failure("Update is already cancelled");
        }

        schedule.IsCancelled = true;
        schedule.CancelledAt = DateTime.UtcNow;
        schedule.CancelledByUserId = userId;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Cancelled scheduled update {ScheduleId} by user {UserId}",
            scheduleId, userId);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<UpdateSchedule?> GetPendingScheduleAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.UpdateSchedules
            .Include(s => s.CreatedByUser)
            .Where(s => !s.IsCancelled && !s.IsExecuted)
            .OrderBy(s => s.ScheduledAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UpdateSchedule>> GetScheduleHistoryAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.UpdateSchedules
            .Include(s => s.CreatedByUser)
            .Include(s => s.CancelledByUser)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UpdateSchedule?> GetByIdAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        return await dbContext.UpdateSchedules
            .Include(s => s.CreatedByUser)
            .Include(s => s.CancelledByUser)
            .FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ProcessDueSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var dueSchedule = await dbContext.UpdateSchedules
            .Where(s => !s.IsCancelled && !s.IsExecuted && s.ScheduledAt <= now)
            .OrderBy(s => s.ScheduledAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (dueSchedule is null)
            return;

        logger.LogInformation(
            "Processing due scheduled update {ScheduleId} to version {Version}",
            dueSchedule.Id, dueSchedule.TargetVersion);

        try
        {
            // Download the update
            var downloadResult = await updateService.DownloadUpdateAsync(
                dueSchedule.TargetVersion,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!downloadResult.Success)
            {
                logger.LogError(
                    "Failed to download scheduled update {ScheduleId}: {Error}",
                    dueSchedule.Id, downloadResult.Error);
                return;
            }

            // Record history
            await historyService.RecordUpdateStartAsync(
                updateService.CurrentVersion.FullVersion,
                dueSchedule.TargetVersion,
                dueSchedule.ReleaseNotes,
                dueSchedule.CreatedByUserId,
                wasScheduled: true,
                dueSchedule.Id,
                cancellationToken).ConfigureAwait(false);

            // Mark as executed
            dueSchedule.IsExecuted = true;
            dueSchedule.ExecutedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Apply the update
            var options = new UpdateOptions
            {
                CreateBackup = dueSchedule.CreateBackup,
                StopGameServers = dueSchedule.StopGameServers,
                RestartGameServers = dueSchedule.RestartGameServers
            };

            var applyResult = await updateService.ApplyUpdateAsync(
                dueSchedule.TargetVersion,
                options,
                cancellationToken).ConfigureAwait(false);

            if (applyResult.IsFailure)
            {
                logger.LogError(
                    "Failed to apply scheduled update {ScheduleId}: {Error}",
                    dueSchedule.Id, applyResult.Error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing scheduled update {ScheduleId}", dueSchedule.Id);
        }
    }

    /// <inheritdoc />
    public async Task<TimeSpan?> GetTimeUntilNextScheduleAsync(CancellationToken cancellationToken = default)
    {
        var pending = await GetPendingScheduleAsync(cancellationToken).ConfigureAwait(false);
        if (pending is null)
            return null;

        var timeUntil = pending.ScheduledAt - DateTime.UtcNow;
        return timeUntil > TimeSpan.Zero ? timeUntil : TimeSpan.Zero;
    }
}
