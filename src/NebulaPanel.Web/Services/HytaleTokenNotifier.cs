using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.Services;
using NebulaPanel.Web.Hubs;

namespace NebulaPanel.Web.Services;

/// <summary>
/// SignalR-based implementation of Hytale token notifications.
/// </summary>
public class HytaleTokenNotifier : IHytaleTokenNotifier
{
    private readonly IHubContext<HytaleAuthHub, IHytaleAuthHubClient> _hubContext;
    private readonly ILogger<HytaleTokenNotifier> _logger;

    public HytaleTokenNotifier(
        IHubContext<HytaleAuthHub, IHytaleAuthHubClient> hubContext,
        ILogger<HytaleTokenNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotifyTokenRefreshedAsync(Guid userId, DateTime newExpiresAt, CancellationToken ct = default)
    {
        try
        {
            var groupName = HytaleAuthHub.GetUserGroupName(userId);
            await _hubContext.Clients.Group(groupName).TokenRefreshed(newExpiresAt).ConfigureAwait(false);
            _logger.LogDebug("Notified user {UserId} of token refresh", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify user {UserId} of token refresh", userId);
        }
    }

    /// <inheritdoc />
    public async Task NotifyTokenRefreshFailedAsync(Guid userId, string message, CancellationToken ct = default)
    {
        try
        {
            var groupName = HytaleAuthHub.GetUserGroupName(userId);
            await _hubContext.Clients.Group(groupName).TokenRefreshFailed(message).ConfigureAwait(false);
            _logger.LogDebug("Notified user {UserId} of token refresh failure", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify user {UserId} of token refresh failure", userId);
        }
    }

    /// <inheritdoc />
    public async Task NotifyTokenExpiredAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var groupName = HytaleAuthHub.GetUserGroupName(userId);
            await _hubContext.Clients.Group(groupName).TokenExpired().ConfigureAwait(false);
            _logger.LogDebug("Notified user {UserId} of token expiration", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify user {UserId} of token expiration", userId);
        }
    }

    /// <inheritdoc />
    public async Task NotifyTokenExpiringWarningAsync(Guid userId, TimeSpan remaining, CancellationToken ct = default)
    {
        try
        {
            var groupName = HytaleAuthHub.GetUserGroupName(userId);
            await _hubContext.Clients.Group(groupName).TokenExpiringWarning(remaining).ConfigureAwait(false);
            _logger.LogDebug("Notified user {UserId} of token expiring in {Remaining}", userId, remaining);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify user {UserId} of token expiring warning", userId);
        }
    }

    /// <inheritdoc />
    public async Task NotifyTokenStatusChangedAsync(Guid userId, HytaleTokenStatusDto status, CancellationToken ct = default)
    {
        try
        {
            var groupName = HytaleAuthHub.GetUserGroupName(userId);
            var hubStatus = new HytaleTokenStatus
            {
                IsAuthenticated = status.IsAuthenticated,
                ExpiresAt = status.ExpiresAt,
                TimeRemaining = status.TimeRemaining,
                HytaleUsername = status.HytaleUsername
            };
            await _hubContext.Clients.Group(groupName).TokenStatusChanged(hubStatus).ConfigureAwait(false);
            _logger.LogDebug("Notified user {UserId} of token status change", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify user {UserId} of token status change", userId);
        }
    }
}
