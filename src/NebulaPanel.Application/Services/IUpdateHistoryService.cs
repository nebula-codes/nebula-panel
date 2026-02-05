using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for recording and retrieving update history.
/// </summary>
public interface IUpdateHistoryService
{
    /// <summary>
    /// Records the start of an update operation.
    /// </summary>
    Task<UpdateHistory> RecordUpdateStartAsync(
        string fromVersion,
        string toVersion,
        string? releaseNotes,
        Guid? userId,
        bool wasScheduled,
        Guid? scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the completion of an update operation.
    /// </summary>
    Task RecordUpdateCompletionAsync(
        Guid historyId,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that an update was rolled back.
    /// </summary>
    Task RecordRollbackAsync(Guid historyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the update history.
    /// </summary>
    Task<IReadOnlyList<UpdateHistory>> GetHistoryAsync(int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific update history entry by ID.
    /// </summary>
    Task<UpdateHistory?> GetByIdAsync(Guid historyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent incomplete update (started but not completed).
    /// </summary>
    Task<UpdateHistory?> GetIncompleteUpdateAsync(CancellationToken cancellationToken = default);
}
