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
    TokenRefreshFailed = 9,

    // Server operations (10-19)
    ServerStarted = 10,
    ServerStopped = 11,
    ServerCreated = 12,
    ServerDeleted = 13,
    ServerUpdated = 14,
    ServerInstalled = 15,

    // Role/permission changes (20-24)
    RoleCreated = 20,
    RoleUpdated = 21,
    RoleDeleted = 22,
    UserRoleAssigned = 23,
    UserRoleRemoved = 24,

    // File operations (30-32)
    FileUploaded = 30,
    FileDeleted = 31,
    FileModified = 32,

    // System/settings (40-44)
    SettingsChanged = 40,
    ApiKeyCreated = 41,
    ApiKeyRevoked = 42,
    BackupCreated = 43,
    BackupRestored = 44
}
