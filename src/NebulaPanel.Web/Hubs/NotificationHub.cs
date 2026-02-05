using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Web.Hubs;

/// <summary>
/// SignalR hub client interface for notification real-time updates.
/// </summary>
public interface INotificationHubClient
{
    /// <summary>
    /// Called when a new notification is received.
    /// </summary>
    Task ReceiveNotification(NotificationDto notification);

    /// <summary>
    /// Called when the unread notification count changes.
    /// </summary>
    Task UnreadCountChanged(int count);
}

/// <summary>
/// SignalR hub for real-time notification delivery.
/// Clients are grouped by user ID for targeted notifications.
/// </summary>
[Authorize]
public class NotificationHub(
    INotificationService notificationService,
    ILogger<NotificationHub> logger) : Hub<INotificationHubClient>
{
    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            // Add user to their personal group for targeted notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId.Value)).ConfigureAwait(false);

            logger.LogDebug(
                "Client {ConnectionId} connected to NotificationHub for user {UserId}",
                Context.ConnectionId, userId.Value);

            // Send current unread count to newly connected client
            try
            {
                var count = await notificationService.GetUnreadCountAsync(userId.Value).ConfigureAwait(false);
                await Clients.Caller.UnreadCountChanged(count).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending initial notification count to client {ConnectionId}", Context.ConnectionId);
            }
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserGroup(userId.Value)).ConfigureAwait(false);

            if (exception is not null)
            {
                logger.LogWarning(exception, "Client {ConnectionId} disconnected from NotificationHub with error",
                    Context.ConnectionId);
            }
            else
            {
                logger.LogDebug("Client {ConnectionId} disconnected from NotificationHub", Context.ConnectionId);
            }
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the user's notifications.
    /// </summary>
    public async Task<IReadOnlyList<NotificationDto>> GetNotifications(int limit = 50, bool includeRead = true)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return [];
        }

        return await notificationService.GetUserNotificationsAsync(userId.Value, limit, includeRead).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the user's notification summary.
    /// </summary>
    public async Task<NotificationSummaryDto?> GetSummary()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return null;
        }

        return await notificationService.GetSummaryAsync(userId.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current unread notification count.
    /// </summary>
    public async Task<int> GetUnreadCount()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return 0;
        }

        return await notificationService.GetUnreadCountAsync(userId.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    public async Task MarkAsRead(Guid notificationId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return;
        }

        await notificationService.MarkAsReadAsync(notificationId).ConfigureAwait(false);

        // Send updated count
        var count = await notificationService.GetUnreadCountAsync(userId.Value).ConfigureAwait(false);
        await Clients.Caller.UnreadCountChanged(count).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    public async Task MarkAllAsRead()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return;
        }

        await notificationService.MarkAllAsReadAsync(userId.Value).ConfigureAwait(false);
        await Clients.Caller.UnreadCountChanged(0).ConfigureAwait(false);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }

    /// <summary>
    /// Gets the SignalR group name for a user.
    /// </summary>
    public static string GetUserGroup(Guid userId) => $"user-{userId}";
}
