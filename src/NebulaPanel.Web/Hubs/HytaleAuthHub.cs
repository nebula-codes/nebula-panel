using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Web.Hubs;

/// <summary>
/// Token status information sent to clients.
/// </summary>
public record HytaleTokenStatus
{
    /// <summary>
    /// Whether the user has valid Hytale credentials.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// When the access token expires.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Time remaining until expiration.
    /// </summary>
    public TimeSpan? TimeRemaining { get; init; }

    /// <summary>
    /// The Hytale account username, if available.
    /// </summary>
    public string? HytaleUsername { get; init; }
}

/// <summary>
/// SignalR hub client interface for Hytale authentication status updates.
/// </summary>
public interface IHytaleAuthHubClient
{
    /// <summary>
    /// Called when the token status changes.
    /// </summary>
    Task TokenStatusChanged(HytaleTokenStatus status);

    /// <summary>
    /// Called when a token has been successfully refreshed.
    /// </summary>
    Task TokenRefreshed(DateTime newExpiresAt);

    /// <summary>
    /// Called when a token refresh attempt failed.
    /// </summary>
    Task TokenRefreshFailed(string message);

    /// <summary>
    /// Called when the token has expired.
    /// </summary>
    Task TokenExpired();

    /// <summary>
    /// Called when a token is about to expire.
    /// </summary>
    Task TokenExpiringWarning(TimeSpan remaining);
}

/// <summary>
/// SignalR hub for real-time Hytale authentication status updates.
/// </summary>
[Authorize]
public class HytaleAuthHub : Hub<IHytaleAuthHubClient>
{
    private readonly IHytaleAuthService _authService;
    private readonly ILogger<HytaleAuthHub> _logger;

    public HytaleAuthHub(
        IHytaleAuthService authService,
        ILogger<HytaleAuthHub> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current token status for the connected user.
    /// </summary>
    public async Task<HytaleTokenStatus> GetTokenStatus()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return new HytaleTokenStatus { IsAuthenticated = false };
        }

        var credentials = await _authService.GetCredentialsAsync(userId.Value).ConfigureAwait(false);

        if (credentials == null || !credentials.IsValid)
        {
            return new HytaleTokenStatus { IsAuthenticated = false };
        }

        return new HytaleTokenStatus
        {
            IsAuthenticated = true,
            ExpiresAt = credentials.ExpiresAt,
            TimeRemaining = credentials.TimeRemaining,
            HytaleUsername = credentials.HytaleUsername
        };
    }

    /// <summary>
    /// Called when a client connects. Sends initial token status.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId != null)
        {
            // Add user to their personal group for targeted updates
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId.Value)).ConfigureAwait(false);

            // Send initial token status
            var status = await GetTokenStatus().ConfigureAwait(false);
            await Clients.Caller.TokenStatusChanged(status).ConfigureAwait(false);

            _logger.LogDebug(
                "Client {ConnectionId} connected to HytaleAuthHub for user {UserId}",
                Context.ConnectionId,
                userId);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserGroupName(userId.Value)).ConfigureAwait(false);

            if (exception != null)
            {
                _logger.LogWarning(
                    exception,
                    "Client {ConnectionId} disconnected with error for user {UserId}",
                    Context.ConnectionId,
                    userId);
            }
            else
            {
                _logger.LogDebug(
                    "Client {ConnectionId} disconnected from HytaleAuthHub for user {UserId}",
                    Context.ConnectionId,
                    userId);
            }
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the user ID from the connection context.
    /// </summary>
    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    /// <summary>
    /// Gets the SignalR group name for a specific user.
    /// </summary>
    internal static string GetUserGroupName(Guid userId) => $"hytale-auth:{userId}";
}
