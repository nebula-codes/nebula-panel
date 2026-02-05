namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Response from the Hytale OAuth2 device authorization endpoint.
/// POST https://oauth.accounts.hytale.com/oauth2/device/auth
/// </summary>
public record DeviceCodeResponse
{
    /// <summary>
    /// The device verification code.
    /// </summary>
    public required string DeviceCode { get; init; }

    /// <summary>
    /// The user code to display to the user.
    /// </summary>
    public required string UserCode { get; init; }

    /// <summary>
    /// The verification URI for the user to visit.
    /// </summary>
    public required string VerificationUri { get; init; }

    /// <summary>
    /// Optional verification URI with user code pre-filled.
    /// </summary>
    public string? VerificationUriComplete { get; init; }

    /// <summary>
    /// Lifetime in seconds of the device_code and user_code.
    /// </summary>
    public int ExpiresIn { get; init; }

    /// <summary>
    /// Minimum interval in seconds between polling requests.
    /// </summary>
    public int Interval { get; init; } = 5;

    /// <summary>
    /// Computed expiration time.
    /// </summary>
    public DateTime ExpiresAt => DateTime.UtcNow.AddSeconds(ExpiresIn);
}

/// <summary>
/// Response from the Hytale OAuth2 token endpoint.
/// POST https://oauth.accounts.hytale.com/oauth2/token
/// </summary>
public record TokenResponse
{
    /// <summary>
    /// The access token issued by the authorization server.
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// The refresh token which can be used to obtain new access tokens.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// The lifetime in seconds of the access token.
    /// </summary>
    public int ExpiresIn { get; init; }

    /// <summary>
    /// The type of token issued (typically "Bearer").
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// The scope of the access token.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Computed expiration time.
    /// </summary>
    public DateTime ExpiresAt => DateTime.UtcNow.AddSeconds(ExpiresIn);
}

/// <summary>
/// Error response from OAuth2 token endpoint during device code polling.
/// </summary>
public record TokenErrorResponse
{
    /// <summary>
    /// Error code.
    /// </summary>
    public required string Error { get; init; }

    /// <summary>
    /// Human-readable error description.
    /// </summary>
    public string? ErrorDescription { get; init; }
}

/// <summary>
/// Response from the Hytale profiles endpoint.
/// GET https://account-data.hytale.com/my-account/get-profiles
/// </summary>
public record ProfilesResponse
{
    /// <summary>
    /// The account owner's UUID.
    /// </summary>
    public required Guid OwnerUuid { get; init; }

    /// <summary>
    /// List of available game profiles for this account.
    /// </summary>
    public required IReadOnlyList<HytaleProfile> Profiles { get; init; }
}

/// <summary>
/// A Hytale game profile.
/// </summary>
public record HytaleProfile
{
    /// <summary>
    /// The unique identifier for this profile.
    /// </summary>
    public required Guid Uuid { get; init; }

    /// <summary>
    /// The display name/username for this profile.
    /// </summary>
    public required string Username { get; init; }
}

/// <summary>
/// Response from creating a new game session.
/// POST https://sessions.hytale.com/game-session/new
/// </summary>
public record GameSessionResponse
{
    /// <summary>
    /// The session token (JWT) used for server authentication.
    /// </summary>
    public required string SessionToken { get; init; }

    /// <summary>
    /// The identity token (JWT) containing player identity information.
    /// </summary>
    public required string IdentityToken { get; init; }

    /// <summary>
    /// When the session tokens expire.
    /// </summary>
    public DateTime ExpiresAt { get; init; }
}

/// <summary>
/// Request body for creating a new game session.
/// </summary>
public record CreateGameSessionRequest
{
    /// <summary>
    /// The profile UUID to create a session for.
    /// </summary>
    public required Guid ProfileUuid { get; init; }
}

/// <summary>
/// Error codes returned during device code polling.
/// </summary>
public static class OAuthDeviceCodeErrors
{
    /// <summary>
    /// The user hasn't yet completed the authorization.
    /// </summary>
    public const string AuthorizationPending = "authorization_pending";

    /// <summary>
    /// Polling too frequently; slow down.
    /// </summary>
    public const string SlowDown = "slow_down";

    /// <summary>
    /// The device_code has expired.
    /// </summary>
    public const string ExpiredToken = "expired_token";

    /// <summary>
    /// The user denied access.
    /// </summary>
    public const string AccessDenied = "access_denied";
}

/// <summary>
/// Combined credentials including OAuth tokens and game session tokens.
/// </summary>
public record HytaleFullCredentials
{
    /// <summary>
    /// OAuth2 access token.
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// OAuth2 refresh token.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// When the OAuth access token expires.
    /// </summary>
    public DateTime AccessTokenExpiresAt { get; init; }

    /// <summary>
    /// Game session token for server authentication.
    /// </summary>
    public string? SessionToken { get; init; }

    /// <summary>
    /// Game identity token for server identity.
    /// </summary>
    public string? IdentityToken { get; init; }

    /// <summary>
    /// When the session tokens expire.
    /// </summary>
    public DateTime? SessionExpiresAt { get; init; }

    /// <summary>
    /// The selected profile UUID.
    /// </summary>
    public Guid? ProfileUuid { get; init; }

    /// <summary>
    /// The account owner UUID.
    /// </summary>
    public Guid? OwnerUuid { get; init; }

    /// <summary>
    /// The username for the selected profile.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Whether the OAuth credentials are still valid.
    /// </summary>
    public bool IsOAuthValid => DateTime.UtcNow < AccessTokenExpiresAt;

    /// <summary>
    /// Whether the session tokens are still valid.
    /// </summary>
    public bool IsSessionValid => SessionExpiresAt.HasValue && DateTime.UtcNow < SessionExpiresAt.Value;
}
