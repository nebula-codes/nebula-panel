namespace NebulaPanel.Application.Services;

/// <summary>
/// Configuration settings for the Hytale authentication service.
/// </summary>
public class HytaleAuthSettings
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Hytale:Auth";

    /// <summary>
    /// How long before expiration to trigger a token refresh.
    /// Default is 1 hour.
    /// </summary>
    public TimeSpan RefreshBeforeExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Thresholds at which to fire expiration warning events.
    /// Default is 24 hours and 1 hour before expiration.
    /// </summary>
    public List<TimeSpan> ExpirationWarningThresholds { get; set; } =
    [
        TimeSpan.FromHours(24),
        TimeSpan.FromHours(1)
    ];

    /// <summary>
    /// Polling interval when waiting for authentication completion.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan AuthPollInterval { get; set; } = TimeSpan.FromSeconds(5);
}
