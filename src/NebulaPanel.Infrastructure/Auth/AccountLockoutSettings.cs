namespace NebulaPanel.Infrastructure.Auth;

/// <summary>
/// Configuration settings for account lockout behavior.
/// </summary>
public class AccountLockoutSettings
{
    public const string SectionName = "AccountLockout";

    /// <summary>
    /// Maximum number of failed login attempts before the account is locked.
    /// Default: 5 attempts.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Duration in minutes that an account remains locked after exceeding max failed attempts.
    /// Default: 15 minutes.
    /// </summary>
    public int LockoutDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Duration in minutes after which the failed attempt counter resets if no new failures occur.
    /// Default: 30 minutes.
    /// </summary>
    public int FailedAttemptResetMinutes { get; set; } = 30;
}
