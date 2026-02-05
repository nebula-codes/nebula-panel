using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Service for recording and retrieving update history.
/// </summary>
public class UpdateHistoryService(
    NebulaPanelDbContext dbContext,
    ILogger<UpdateHistoryService> logger) : IUpdateHistoryService
{
    /// <inheritdoc />
    public async Task<UpdateHistory> RecordUpdateStartAsync(
        string fromVersion,
        string toVersion,
        string? releaseNotes,
        Guid? userId,
        bool wasScheduled,
        Guid? scheduleId,
        CancellationToken cancellationToken = default)
    {
        var history = new UpdateHistory
        {
            Id = Guid.NewGuid(),
            FromVersion = fromVersion,
            ToVersion = toVersion,
            ReleaseNotes = releaseNotes,
            InitiatedByUserId = userId,
            WasScheduled = wasScheduled,
            ScheduleId = scheduleId,
            StartedAt = DateTime.UtcNow,
            Success = false
        };

        dbContext.UpdateHistories.Add(history);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Recorded update start: {FromVersion} -> {ToVersion} (HistoryId: {HistoryId})",
            fromVersion, toVersion, history.Id);

        return history;
    }

    /// <inheritdoc />
    public async Task RecordUpdateCompletionAsync(
        Guid historyId,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        var history = await dbContext.UpdateHistories
            .FirstOrDefaultAsync(h => h.Id == historyId, cancellationToken)
            .ConfigureAwait(false);

        if (history is null)
        {
            logger.LogWarning("Update history entry not found: {HistoryId}", historyId);
            return;
        }

        history.CompletedAt = DateTime.UtcNow;
        history.Success = success;
        history.ErrorMessage = errorMessage;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Recorded update completion: {HistoryId} Success={Success}",
            historyId, success);
    }

    /// <inheritdoc />
    public async Task RecordRollbackAsync(Guid historyId, CancellationToken cancellationToken = default)
    {
        var history = await dbContext.UpdateHistories
            .FirstOrDefaultAsync(h => h.Id == historyId, cancellationToken)
            .ConfigureAwait(false);

        if (history is null)
        {
            logger.LogWarning("Update history entry not found for rollback: {HistoryId}", historyId);
            return;
        }

        history.WasRolledBack = true;
        history.RolledBackAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Recorded rollback for update: {HistoryId}", historyId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UpdateHistory>> GetHistoryAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.UpdateHistories
            .Include(h => h.InitiatedByUser)
            .OrderByDescending(h => h.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UpdateHistory?> GetByIdAsync(Guid historyId, CancellationToken cancellationToken = default)
    {
        return await dbContext.UpdateHistories
            .Include(h => h.InitiatedByUser)
            .FirstOrDefaultAsync(h => h.Id == historyId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UpdateHistory?> GetIncompleteUpdateAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.UpdateHistories
            .Where(h => h.CompletedAt == null)
            .OrderByDescending(h => h.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
