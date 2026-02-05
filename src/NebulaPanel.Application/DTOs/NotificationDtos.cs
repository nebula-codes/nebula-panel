using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// DTO for notification data.
/// </summary>
public record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string? ActionUrl,
    Guid? RelatedEntityId,
    string? Metadata
);

/// <summary>
/// Request to create a new notification.
/// </summary>
public record CreateNotificationRequest(
    Guid UserId,
    NotificationType Type,
    string Title,
    string Message,
    string? ActionUrl = null,
    Guid? RelatedEntityId = null,
    string? Metadata = null
);

/// <summary>
/// Summary of a user's notification state.
/// </summary>
public record NotificationSummaryDto(
    int UnreadCount,
    int TotalCount,
    IReadOnlyList<NotificationDto> RecentNotifications
);
