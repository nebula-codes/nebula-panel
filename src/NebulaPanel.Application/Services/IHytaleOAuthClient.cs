namespace NebulaPanel.Application.Services;

using NebulaPanel.Application.DTOs;

/// <summary>
/// Client for direct OAuth2 API calls to Hytale authentication endpoints.
/// Implements Method B: Device Code Flow (RFC 8628).
/// </summary>
public interface IHytaleOAuthClient
{
    /// <summary>
    /// Requests a device code for OAuth2 device authorization.
    /// POST https://oauth.accounts.hytale.com/oauth2/device/auth
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Device code response containing user_code and verification_uri.</returns>
    Task<DeviceCodeResponse> RequestDeviceCodeAsync(CancellationToken ct = default);

    /// <summary>
    /// Polls the token endpoint for access/refresh tokens.
    /// POST https://oauth.accounts.hytale.com/oauth2/token
    /// Returns null if authorization is still pending.
    /// </summary>
    /// <param name="deviceCode">The device_code from RequestDeviceCodeAsync.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Token response if authorized, null if pending, throws on error.</returns>
    Task<TokenResponse?> PollForTokenAsync(string deviceCode, CancellationToken ct = default);

    /// <summary>
    /// Refreshes the access token using a refresh token.
    /// POST https://oauth.accounts.hytale.com/oauth2/token
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>New token response with refreshed access token.</returns>
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Gets the available profiles for the authenticated account.
    /// GET https://account-data.hytale.com/my-account/get-profiles
    /// </summary>
    /// <param name="accessToken">The OAuth access token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Profiles response containing owner UUID and available profiles.</returns>
    Task<ProfilesResponse> GetProfilesAsync(string accessToken, CancellationToken ct = default);

    /// <summary>
    /// Creates a new game session for server authentication.
    /// POST https://sessions.hytale.com/game-session/new
    /// </summary>
    /// <param name="accessToken">The OAuth access token.</param>
    /// <param name="profileUuid">The profile UUID to create a session for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Game session response with sessionToken and identityToken.</returns>
    Task<GameSessionResponse> CreateGameSessionAsync(string accessToken, Guid profileUuid, CancellationToken ct = default);

    /// <summary>
    /// Refreshes an existing game session before it expires.
    /// POST https://sessions.hytale.com/game-session/refresh
    /// </summary>
    /// <param name="sessionToken">The current session token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated game session response with new tokens.</returns>
    Task<GameSessionResponse> RefreshGameSessionAsync(string sessionToken, CancellationToken ct = default);

    /// <summary>
    /// Ends a game session when the server stops.
    /// DELETE https://sessions.hytale.com/game-session
    /// </summary>
    /// <param name="sessionToken">The session token to end.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EndGameSessionAsync(string sessionToken, CancellationToken ct = default);
}

/// <summary>
/// Exception thrown when OAuth2 device code polling encounters an error.
/// </summary>
public class HytaleOAuthException : Exception
{
    /// <summary>
    /// The OAuth error code (e.g., "expired_token", "access_denied").
    /// </summary>
    public string ErrorCode { get; }

    public HytaleOAuthException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public HytaleOAuthException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when the device code has expired.
/// </summary>
public class DeviceCodeExpiredException : HytaleOAuthException
{
    public DeviceCodeExpiredException()
        : base(OAuthDeviceCodeErrors.ExpiredToken, "The device code has expired. Please start a new authentication flow.")
    {
    }
}

/// <summary>
/// Exception thrown when the user denied access.
/// </summary>
public class AccessDeniedException : HytaleOAuthException
{
    public AccessDeniedException()
        : base(OAuthDeviceCodeErrors.AccessDenied, "The user denied access to the application.")
    {
    }
}
