namespace NebulaPanel.Infrastructure.Auth;

/// <summary>
/// Service for rate limiting login attempts.
/// </summary>
public interface ILoginRateLimiter
{
    /// <summary>
    /// Checks if a login attempt is allowed for the given IP and username combination.
    /// </summary>
    /// <param name="ipAddress">The IP address of the client.</param>
    /// <param name="username">The username being attempted.</param>
    /// <returns>True if the attempt is allowed, false if rate limited.</returns>
    bool IsAllowed(string? ipAddress, string? username);

    /// <summary>
    /// Records a login attempt for rate limiting purposes.
    /// </summary>
    /// <param name="ipAddress">The IP address of the client.</param>
    /// <param name="username">The username being attempted.</param>
    void RecordAttempt(string? ipAddress, string? username);

    /// <summary>
    /// Gets the number of seconds until the rate limit resets.
    /// </summary>
    /// <param name="ipAddress">The IP address of the client.</param>
    /// <param name="username">The username being attempted.</param>
    /// <returns>The number of seconds until reset, or 0 if not rate limited.</returns>
    int GetRetryAfterSeconds(string? ipAddress, string? username);

    /// <summary>
    /// Clears the rate limit for a specific IP and username combination.
    /// Called after successful login.
    /// </summary>
    /// <param name="ipAddress">The IP address of the client.</param>
    /// <param name="username">The username.</param>
    void ClearAttempts(string? ipAddress, string? username);
}
