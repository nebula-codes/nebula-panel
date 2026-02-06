using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Repositories;

/// <summary>
/// Repository for notification management.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Gets a notification by its ID.
    /// </summary>
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets notifications for a specific user, ordered by creation date descending.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="limit">Maximum number of notifications to return.</param>
    /// <param name="includeRead">Whether to include read notifications.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(
        Guid userId,
        int limit = 50,
        bool includeRead = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of unread notifications for a user.
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of notifications for a user.
    /// </summary>
    Task<int> GetCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new notification.
    /// </summary>
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all notifications for a user as read.
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a notification.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all notifications for a user.
    /// </summary>
    Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes notifications older than the specified date.
    /// </summary>
    Task DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}
