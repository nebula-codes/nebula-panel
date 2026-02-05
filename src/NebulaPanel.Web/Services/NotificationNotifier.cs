using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Web.Hubs;

namespace NebulaPanel.Web.Services;

/// <summary>
/// Interface for pushing notifications to users via SignalR.
/// </summary>
public interface INotificationNotifier
{
    /// <summary>
    /// Sends a notification to a specific user.
    /// </summary>
    Task SendNotificationAsync(Guid userId, NotificationDto notification);

    /// <summary>
    /// Updates the unread count for a specific user.
    /// </summary>
    Task UpdateUnreadCountAsync(Guid userId, int count);

    /// <summary>
    /// Sends a notification to all connected users.
    /// </summary>
    Task BroadcastNotificationAsync(NotificationDto notification);
}

/// <summary>
/// Service for pushing notifications to users via SignalR.
/// </summary>
public class NotificationNotifier(IHubContext<NotificationHub, INotificationHubClient> hubContext) : INotificationNotifier
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext = hubContext;

    /// <inheritdoc />
    public async Task SendNotificationAsync(Guid userId, NotificationDto notification)
    {
        var groupName = NotificationHub.GetUserGroup(userId);
        await _hubContext.Clients.Group(groupName).ReceiveNotification(notification).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateUnreadCountAsync(Guid userId, int count)
    {
        var groupName = NotificationHub.GetUserGroup(userId);
        await _hubContext.Clients.Group(groupName).UnreadCountChanged(count).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task BroadcastNotificationAsync(NotificationDto notification)
    {
        await _hubContext.Clients.All.ReceiveNotification(notification).ConfigureAwait(false);
    }
}
