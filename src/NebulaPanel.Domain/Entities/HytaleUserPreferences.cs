namespace NebulaPanel.Domain.Entities;

/// <summary>
/// User preferences for Hytale token management and notifications.
/// </summary>
public class HytaleUserPreferences
{
    /// <summary>
    /// Unique identifier for this preferences record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The Nebula Panel user who owns these preferences.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Whether to automatically refresh the token before it expires.
    /// </summary>
    public bool AutoRefreshToken { get; set; } = true;

    /// <summary>
    /// Whether to notify when token expires in less than 24 hours.
    /// </summary>
    public bool NotifyAt24Hours { get; set; } = true;

    /// <summary>
    /// Whether to notify when token expires in less than 1 hour.
    /// </summary>
    public bool NotifyAt1Hour { get; set; } = true;

    /// <summary>
    /// Whether to send email notification when token expires.
    /// </summary>
    public bool EmailOnExpiry { get; set; } = false;

    /// <summary>
    /// Navigation property to the owning user.
    /// </summary>
    private User? _user;
    public User User
    {
        get => _user ?? throw new InvalidOperationException("User navigation property was not loaded. Use .Include(x => x.User) in your query.");
        set => _user = value;
    }
}
