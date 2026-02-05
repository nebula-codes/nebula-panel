namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Types of security events that are logged for audit purposes.
/// </summary>
public enum SecurityEventType
{
    /// <summary>
    /// Successful login attempt.
    /// </summary>
    LoginSuccess = 0,

    /// <summary>
    /// Failed login attempt due to invalid credentials.
    /// </summary>
    LoginFailed = 1,

    /// <summary>
    /// Failed login attempt because the account is locked.
    /// </summary>
    LoginFailedAccountLocked = 2,

    /// <summary>
    /// Failed login attempt due to rate limiting.
    /// </summary>
    LoginFailedRateLimited = 3,

    /// <summary>
    /// User logged out.
    /// </summary>
    Logout = 4,

    /// <summary>
    /// Access token was refreshed.
    /// </summary>
    TokenRefresh = 5,

    /// <summary>
    /// User changed their password.
    /// </summary>
    PasswordChanged = 6,

    /// <summary>
    /// Account was locked due to too many failed login attempts.
    /// </summary>
    AccountLocked = 7,

    /// <summary>
    /// Account lockout expired and account was unlocked.
    /// </summary>
    AccountUnlocked = 8,

    /// <summary>
    /// Token refresh failed.
    /// </summary>
    TokenRefreshFailed = 9
}
