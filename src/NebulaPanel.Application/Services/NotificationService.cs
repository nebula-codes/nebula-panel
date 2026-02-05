using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for managing user notifications.
/// </summary>
public class NotificationService(
    INotificationRepository notificationRepository,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(
        Guid userId,
        int limit = 50,
        bool includeRead = true,
        CancellationToken cancellationToken = default)
    {
        var notifications = await notificationRepository
            .GetByUserIdAsync(userId, limit, includeRead, cancellationToken)
            .ConfigureAwait(false);

        return notifications.Select(MapToDto).ToList();
    }

    public async Task<NotificationSummaryDto> GetSummaryAsync(
        Guid userId,
        int recentCount = 5,
        CancellationToken cancellationToken = default)
    {
        var unreadCount = await notificationRepository
            .GetUnreadCountAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        var recent = await notificationRepository
            .GetByUserIdAsync(userId, recentCount, true, cancellationToken)
            .ConfigureAwait(false);

        var totalCount = await notificationRepository
            .GetByUserIdAsync(userId, int.MaxValue, true, cancellationToken)
            .ConfigureAwait(false);

        return new NotificationSummaryDto(
            unreadCount,
            totalCount.Count,
            recent.Select(MapToDto).ToList()
        );
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await notificationRepository
            .GetUnreadCountAsync(userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<NotificationDto> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Type = request.Type,
            Title = request.Title,
            Message = request.Message,
            ActionUrl = request.ActionUrl,
            RelatedEntityId = request.RelatedEntityId,
            Metadata = request.Metadata,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Created notification {NotificationId} of type {Type} for user {UserId}",
            notification.Id, notification.Type, notification.UserId);

        return MapToDto(notification);
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        await notificationRepository.MarkAsReadAsync(notificationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await notificationRepository.MarkAllAsReadAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        await notificationRepository.DeleteAsync(notificationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await notificationRepository.DeleteAllForUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    // Helper methods for common notification types

    public async Task<NotificationDto> NotifyServerStatusAsync(
        Guid userId,
        Guid serverId,
        string serverName,
        string status,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new CreateNotificationRequest(
            UserId: userId,
            Type: NotificationType.ServerStatus,
            Title: $"Server {status}",
            Message: $"{serverName} is now {status.ToLowerInvariant()}.",
            ActionUrl: $"/servers/{serverId}",
            RelatedEntityId: serverId
        ), cancellationToken).ConfigureAwait(false);
    }

    public async Task<NotificationDto> NotifyBackupCompletedAsync(
        Guid userId,
        Guid serverId,
        string serverName,
        Guid backupId,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new CreateNotificationRequest(
            UserId: userId,
            Type: NotificationType.BackupCompleted,
            Title: "Backup Completed",
            Message: $"Backup for {serverName} completed successfully.",
            ActionUrl: $"/servers/{serverId}/backups",
            RelatedEntityId: backupId
        ), cancellationToken).ConfigureAwait(false);
    }

    public async Task<NotificationDto> NotifyBackupFailedAsync(
        Guid userId,
        Guid serverId,
        string serverName,
        string error,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new CreateNotificationRequest(
            UserId: userId,
            Type: NotificationType.BackupFailed,
            Title: "Backup Failed",
            Message: $"Backup for {serverName} failed: {error}",
            ActionUrl: $"/servers/{serverId}/backups",
            RelatedEntityId: serverId
        ), cancellationToken).ConfigureAwait(false);
    }

    public async Task<NotificationDto> NotifyUpdateAvailableAsync(
        Guid userId,
        string title,
        string message,
        string? actionUrl = null,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new CreateNotificationRequest(
            UserId: userId,
            Type: NotificationType.UpdateAvailable,
            Title: title,
            Message: message,
            ActionUrl: actionUrl
        ), cancellationToken).ConfigureAwait(false);
    }

    public async Task<NotificationDto> NotifyInstallCompletedAsync(
        Guid userId,
        Guid serverId,
        string serverName,
        string what,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new CreateNotificationRequest(
            UserId: userId,
            Type: NotificationType.InstallCompleted,
            Title: "Installation Completed",
            Message: $"{what} installed successfully on {serverName}.",
            ActionUrl: $"/servers/{serverId}",
            RelatedEntityId: serverId
        ), cancellationToken).ConfigureAwait(false);
    }

    public async Task<NotificationDto> NotifyInstallFailedAsync(
        Guid userId,
        Guid serverId,
        string serverName,
        string what,
        string error,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(new CreateNotificationRequest(
            UserId: userId,
            Type: NotificationType.InstallFailed,
            Title: "Installation Failed",
            Message: $"Failed to install {what} on {serverName}: {error}",
            ActionUrl: $"/servers/{serverId}",
            RelatedEntityId: serverId
        ), cancellationToken).ConfigureAwait(false);
    }

    private static NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.IsRead,
            notification.CreatedAt,
            notification.ReadAt,
            notification.ActionUrl,
            notification.RelatedEntityId,
            notification.Metadata
        );
    }
}
