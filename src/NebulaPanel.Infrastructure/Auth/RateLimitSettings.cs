namespace NebulaPanel.Infrastructure.Auth;

/// <summary>
/// Configuration settings for login rate limiting.
/// </summary>
public class RateLimitSettings
{
    public const string SectionName = "RateLimit";

    /// <summary>
    /// Maximum number of login attempts allowed per window.
    /// Default: 5 attempts.
    /// </summary>
    public int MaxAttemptsPerWindow { get; set; } = 5;

    /// <summary>
    /// Duration of the sliding window in seconds.
    /// Default: 60 seconds.
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to include the username in the rate limit key.
    /// When true, rate limiting is per IP+username combination.
    /// When false, rate limiting is per IP only.
    /// Default: true.
    /// </summary>
    public bool IncludeUsername { get; set; } = true;
}
