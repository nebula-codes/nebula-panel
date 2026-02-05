namespace NebulaPanel.Application.Services;

/// <summary>
/// Interface for sending real-time Hytale token notifications to users.
/// </summary>
public interface IHytaleTokenNotifier
{
    /// <summary>
    /// Notifies the user that their token has been successfully refreshed.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="newExpiresAt">The new expiration time.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyTokenRefreshedAsync(Guid userId, DateTime newExpiresAt, CancellationToken ct = default);

    /// <summary>
    /// Notifies the user that a token refresh attempt failed.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="message">The error message.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyTokenRefreshFailedAsync(Guid userId, string message, CancellationToken ct = default);

    /// <summary>
    /// Notifies the user that their token has expired.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyTokenExpiredAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Notifies the user that their token is about to expire.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="remaining">Time remaining until expiration.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyTokenExpiringWarningAsync(Guid userId, TimeSpan remaining, CancellationToken ct = default);

    /// <summary>
    /// Notifies the user of their current token status.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="status">The token status.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyTokenStatusChangedAsync(Guid userId, HytaleTokenStatusDto status, CancellationToken ct = default);
}

/// <summary>
/// Token status information for notifications.
/// </summary>
public record HytaleTokenStatusDto
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
