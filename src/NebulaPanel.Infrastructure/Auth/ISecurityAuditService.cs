namespace NebulaPanel.Infrastructure.Auth;

using NebulaPanel.Domain.Enums;

/// <summary>
/// Service for logging security-related events.
/// </summary>
public interface ISecurityAuditService
{
    /// <summary>
    /// Logs a security event.
    /// </summary>
    /// <param name="eventType">The type of security event.</param>
    /// <param name="userId">The ID of the user involved, if known.</param>
    /// <param name="username">The username involved.</param>
    /// <param name="ipAddress">The IP address of the client.</param>
    /// <param name="userAgent">The User-Agent header.</param>
    /// <param name="success">Whether the operation was successful.</param>
    /// <param name="details">Additional details about the event.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LogEventAsync(
        SecurityEventType eventType,
        Guid? userId,
        string? username,
        string? ipAddress,
        string? userAgent,
        bool success,
        string? details = null,
        CancellationToken ct = default);

    /// <summary>
    /// Logs a successful login event.
    /// </summary>
    Task LogLoginSuccessAsync(Guid userId, string username, string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// Logs a failed login event.
    /// </summary>
    Task LogLoginFailedAsync(string username, string? ipAddress, string? userAgent, string reason, CancellationToken ct = default);

    /// <summary>
    /// Logs a rate-limited login attempt.
    /// </summary>
    Task LogLoginRateLimitedAsync(string username, string? ipAddress, string? userAgent, int retryAfterSeconds, CancellationToken ct = default);

    /// <summary>
    /// Logs an account lockout event.
    /// </summary>
    Task LogAccountLockedAsync(Guid userId, string username, string? ipAddress, string? userAgent, DateTime lockoutEnd, CancellationToken ct = default);

    /// <summary>
    /// Logs a logout event.
    /// </summary>
    Task LogLogoutAsync(Guid userId, string username, string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// Logs a token refresh event.
    /// </summary>
    Task LogTokenRefreshAsync(Guid userId, string username, string? ipAddress, string? userAgent, bool success, string? details = null, CancellationToken ct = default);

    /// <summary>
    /// Logs a password change event.
    /// </summary>
    Task LogPasswordChangedAsync(Guid userId, string username, string? ipAddress, string? userAgent, CancellationToken ct = default);
}
