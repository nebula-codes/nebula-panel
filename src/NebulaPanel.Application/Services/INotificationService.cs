using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for managing user notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Gets notifications for a user.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="limit">Maximum number of notifications to return.</param>
    /// <param name="includeRead">Whether to include read notifications.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(
        Guid userId,
        int limit = 50,
        bool includeRead = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a summary of a user's notification state.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="recentCount">Number of recent notifications to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<NotificationSummaryDto> GetSummaryAsync(
        Guid userId,
        int recentCount = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the unread notification count for a user.
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new notification.
    /// </summary>
    /// <param name="request">The notification creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created notification.</returns>
    Task<NotificationDto> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all notifications for a user as read.
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a notification.
    /// </summary>
    Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all notifications for a user.
    /// </summary>
    Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    // Helper methods for common notification types

    /// <summary>
    /// Creates a server status change notification.
    /// </summary>
    Task<NotificationDto> NotifyServerStatusAsync(
        Guid userId,
        Guid serverId,
        string serverName,
        string status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a backup completed notification.
    /// </summary>
    Task<NotificationDto> NotifyBackupCompletedAsync(
        Guid userId,
        Guid serverId,
        string serverName,
        Guid backupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a backup failed notification.
    /// </summary>
    Task<NotificationDto> NotifyBackupFailedAsync(
        Guid userId,
        Guid serverId,
        string serverName,
        string error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an update available notification.
    /// </summary>
    Task<NotificationDto> NotifyUpdateAvailableAsync(
        Guid userId,
        string title,
        string message,
        string? actionUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an installation completed notification.
    /// </summary>
    Task<NotificationDto> NotifyInstallCompletedAsync(
        Guid userId,
        Guid serverId,
        string serverName,
        string what,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an installation failed notification.
    /// </summary>
    Task<NotificationDto> NotifyInstallFailedAsync(
        Guid userId,
        Guid serverId,
        string serverName,
        string what,
        string error,
        CancellationToken cancellationToken = default);
}
