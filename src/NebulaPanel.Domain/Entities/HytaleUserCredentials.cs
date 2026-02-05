namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Stores Hytale OAuth2 credentials for a Nebula Panel user.
/// Tokens are stored encrypted at rest.
/// </summary>
public class HytaleUserCredentials
{
    /// <summary>
    /// Unique identifier for this credential record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The Nebula Panel user who owns these credentials.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// OAuth2 access token (stored encrypted).
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 refresh token (stored encrypted).
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// When the access token expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When the credentials were first created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the tokens were last refreshed.
    /// </summary>
    public DateTime? LastRefreshedAt { get; set; }

    /// <summary>
    /// When the credentials were last used for a download operation.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// The Hytale account username, if available.
    /// </summary>
    public string? HytaleUsername { get; set; }

    /// <summary>
    /// Game session token for server authentication (stored encrypted).
    /// Obtained via POST sessions.hytale.com/game-session/new
    /// </summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// Game identity token for server identity (stored encrypted).
    /// Obtained via POST sessions.hytale.com/game-session/new
    /// </summary>
    public string? IdentityToken { get; set; }

    /// <summary>
    /// When the session tokens expire (typically 1 hour).
    /// </summary>
    public DateTime? SessionExpiresAt { get; set; }

    /// <summary>
    /// The selected Hytale profile UUID.
    /// </summary>
    public Guid? ProfileUuid { get; set; }

    /// <summary>
    /// The Hytale account owner UUID.
    /// </summary>
    public Guid? OwnerUuid { get; set; }

    /// <summary>
    /// Navigation property to the owning user.
    /// </summary>
    private User? _user;
    public User User
    {
        get => _user ?? throw new InvalidOperationException("User navigation property was not loaded. Use .Include(x => x.User) in your query.");
        set => _user = value;
    }

    /// <summary>
    /// Whether the OAuth credentials are still valid (not expired).
    /// </summary>
    public bool IsValid => DateTime.UtcNow < ExpiresAt;

    /// <summary>
    /// Time remaining until the OAuth access token expires.
    /// </summary>
    public TimeSpan TimeRemaining => ExpiresAt - DateTime.UtcNow;

    /// <summary>
    /// Whether the game session tokens are still valid.
    /// </summary>
    public bool IsSessionValid => SessionExpiresAt.HasValue && DateTime.UtcNow < SessionExpiresAt.Value;

    /// <summary>
    /// Time remaining until the session tokens expire.
    /// </summary>
    public TimeSpan? SessionTimeRemaining => SessionExpiresAt.HasValue
        ? SessionExpiresAt.Value - DateTime.UtcNow
        : null;
}
