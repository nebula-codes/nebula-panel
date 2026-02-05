using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Infrastructure.OfficialGames.Hytale;

/// <summary>
/// Implements direct OAuth2 API calls to Hytale authentication endpoints.
/// Uses Method B: Device Code Flow (RFC 8628).
/// </summary>
public class HytaleOAuthClient : IHytaleOAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HytaleOAuthClient> _logger;

    // Hytale OAuth2 endpoints
    private const string DeviceAuthEndpoint = "https://oauth.accounts.hytale.com/oauth2/device/auth";
    private const string TokenEndpoint = "https://oauth.accounts.hytale.com/oauth2/token";
    private const string ProfilesEndpoint = "https://account-data.hytale.com/my-account/get-profiles";
    private const string SessionNewEndpoint = "https://sessions.hytale.com/game-session/new";
    private const string SessionRefreshEndpoint = "https://sessions.hytale.com/game-session/refresh";
    private const string SessionEndEndpoint = "https://sessions.hytale.com/game-session";

    // OAuth2 client credentials (public client for device code flow)
    private const string ClientId = "hytale-server";
    private const string Scope = "openid offline auth:server";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HytaleOAuthClient(
        IHttpClientFactory httpClientFactory,
        ILogger<HytaleOAuthClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("HytaleOAuth");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DeviceCodeResponse> RequestDeviceCodeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Requesting Hytale device code");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["scope"] = Scope
        });

        var response = await _httpClient.PostAsync(DeviceAuthEndpoint, content, ct).ConfigureAwait(false);

        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _logger.LogDebug("Device auth response: {Response}", responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to request device code: {StatusCode} - {Response}",
                response.StatusCode, responseBody);
            throw new HytaleOAuthException("device_auth_failed",
                $"Failed to request device code: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<DeviceCodeApiResponse>(responseBody, JsonOptions)
            ?? throw new HytaleOAuthException("parse_error", "Failed to parse device code response");

        _logger.LogInformation(
            "Received device code. User code: {UserCode}, Verification URI: {VerificationUri}, Expires in: {ExpiresIn}s",
            result.UserCode, result.VerificationUri, result.ExpiresIn);

        return new DeviceCodeResponse
        {
            DeviceCode = result.DeviceCode,
            UserCode = result.UserCode,
            VerificationUri = result.VerificationUri,
            VerificationUriComplete = result.VerificationUriComplete,
            ExpiresIn = result.ExpiresIn,
            Interval = result.Interval > 0 ? result.Interval : 5
        };
    }

    /// <inheritdoc />
    public async Task<TokenResponse?> PollForTokenAsync(string deviceCode, CancellationToken ct = default)
    {
        _logger.LogDebug("Polling for token");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["device_code"] = deviceCode
        });

        var response = await _httpClient.PostAsync(TokenEndpoint, content, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Parse error response
            var errorResponse = JsonSerializer.Deserialize<TokenErrorApiResponse>(responseBody, JsonOptions);

            if (errorResponse?.Error == OAuthDeviceCodeErrors.AuthorizationPending)
            {
                _logger.LogDebug("Authorization still pending");
                return null;
            }

            if (errorResponse?.Error == OAuthDeviceCodeErrors.SlowDown)
            {
                _logger.LogDebug("Slow down requested, waiting longer");
                return null;
            }

            if (errorResponse?.Error == OAuthDeviceCodeErrors.ExpiredToken)
            {
                _logger.LogWarning("Device code expired");
                throw new DeviceCodeExpiredException();
            }

            if (errorResponse?.Error == OAuthDeviceCodeErrors.AccessDenied)
            {
                _logger.LogWarning("User denied access");
                throw new AccessDeniedException();
            }

            _logger.LogError("Token poll failed: {Error} - {Description}",
                errorResponse?.Error, errorResponse?.ErrorDescription);
            throw new HytaleOAuthException(
                errorResponse?.Error ?? "unknown_error",
                errorResponse?.ErrorDescription ?? "Unknown error during token poll");
        }

        var result = JsonSerializer.Deserialize<TokenApiResponse>(responseBody, JsonOptions)
            ?? throw new HytaleOAuthException("parse_error", "Failed to parse token response");

        _logger.LogInformation("Received access token, expires in {ExpiresIn}s", result.ExpiresIn);

        return new TokenResponse
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = result.ExpiresIn,
            TokenType = result.TokenType ?? "Bearer",
            Scope = result.Scope
        };
    }

    /// <inheritdoc />
    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        _logger.LogInformation("Refreshing Hytale access token");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var response = await _httpClient.PostAsync(TokenEndpoint, content, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to refresh token: {StatusCode} - {Response}",
                response.StatusCode, responseBody);
            throw new HytaleOAuthException("refresh_failed",
                $"Failed to refresh token: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<TokenApiResponse>(responseBody, JsonOptions)
            ?? throw new HytaleOAuthException("parse_error", "Failed to parse token response");

        _logger.LogInformation("Refreshed access token, expires in {ExpiresIn}s", result.ExpiresIn);

        return new TokenResponse
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = result.ExpiresIn,
            TokenType = result.TokenType ?? "Bearer",
            Scope = result.Scope
        };
    }

    /// <inheritdoc />
    public async Task<ProfilesResponse> GetProfilesAsync(string accessToken, CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching Hytale profiles");

        using var request = new HttpRequestMessage(HttpMethod.Get, ProfilesEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get profiles: {StatusCode} - {Response}",
                response.StatusCode, responseBody);
            throw new HytaleOAuthException("profiles_failed",
                $"Failed to get profiles: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<ProfilesApiResponse>(responseBody, JsonOptions)
            ?? throw new HytaleOAuthException("parse_error", "Failed to parse profiles response");

        _logger.LogInformation("Found {Count} profile(s) for owner {OwnerUuid}",
            result.Profiles.Count, result.OwnerUuid);

        return new ProfilesResponse
        {
            OwnerUuid = result.OwnerUuid,
            Profiles = result.Profiles.Select(p => new HytaleProfile
            {
                Uuid = p.Uuid,
                Username = p.Username
            }).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<GameSessionResponse> CreateGameSessionAsync(
        string accessToken,
        Guid profileUuid,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating game session for profile {ProfileUuid}", profileUuid);

        var requestBody = new { uuid = profileUuid };
        var json = JsonSerializer.Serialize(requestBody, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, SessionNewEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to create game session: {StatusCode} - {Response}",
                response.StatusCode, responseBody);
            throw new HytaleOAuthException("session_create_failed",
                $"Failed to create game session: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<GameSessionApiResponse>(responseBody, JsonOptions)
            ?? throw new HytaleOAuthException("parse_error", "Failed to parse game session response");

        _logger.LogInformation("Created game session, expires at {ExpiresAt}", result.ExpiresAt);

        return new GameSessionResponse
        {
            SessionToken = result.SessionToken,
            IdentityToken = result.IdentityToken,
            ExpiresAt = result.ExpiresAt
        };
    }

    /// <inheritdoc />
    public async Task<GameSessionResponse> RefreshGameSessionAsync(string sessionToken, CancellationToken ct = default)
    {
        _logger.LogInformation("Refreshing game session");

        using var request = new HttpRequestMessage(HttpMethod.Post, SessionRefreshEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to refresh game session: {StatusCode} - {Response}",
                response.StatusCode, responseBody);
            throw new HytaleOAuthException("session_refresh_failed",
                $"Failed to refresh game session: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<GameSessionApiResponse>(responseBody, JsonOptions)
            ?? throw new HytaleOAuthException("parse_error", "Failed to parse game session response");

        _logger.LogInformation("Refreshed game session, expires at {ExpiresAt}", result.ExpiresAt);

        return new GameSessionResponse
        {
            SessionToken = result.SessionToken,
            IdentityToken = result.IdentityToken,
            ExpiresAt = result.ExpiresAt
        };
    }

    /// <inheritdoc />
    public async Task EndGameSessionAsync(string sessionToken, CancellationToken ct = default)
    {
        _logger.LogInformation("Ending game session");

        using var request = new HttpRequestMessage(HttpMethod.Delete, SessionEndEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogWarning("Failed to end game session: {StatusCode} - {Response}",
                response.StatusCode, responseBody);
            // Don't throw - session might already be expired
        }
        else
        {
            _logger.LogInformation("Game session ended successfully");
        }
    }

    #region API Response DTOs (internal use, snake_case mapping)

    private sealed record DeviceCodeApiResponse
    {
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; init; } = string.Empty;

        [JsonPropertyName("user_code")]
        public string UserCode { get; init; } = string.Empty;

        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; init; } = string.Empty;

        [JsonPropertyName("verification_uri_complete")]
        public string? VerificationUriComplete { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("interval")]
        public int Interval { get; init; }
    }

    private sealed record TokenApiResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }

        [JsonPropertyName("scope")]
        public string? Scope { get; init; }
    }

    private sealed record TokenErrorApiResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; init; }
    }

    private sealed record ProfilesApiResponse
    {
        [JsonPropertyName("owner_uuid")]
        public Guid OwnerUuid { get; init; }

        [JsonPropertyName("profiles")]
        public List<ProfileApiResponse> Profiles { get; init; } = new();
    }

    private sealed record ProfileApiResponse
    {
        [JsonPropertyName("uuid")]
        public Guid Uuid { get; init; }

        [JsonPropertyName("username")]
        public string Username { get; init; } = string.Empty;
    }

    private sealed record GameSessionApiResponse
    {
        [JsonPropertyName("sessionToken")]
        public string SessionToken { get; init; } = string.Empty;

        [JsonPropertyName("identityToken")]
        public string IdentityToken { get; init; } = string.Empty;

        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; init; }
    }

    #endregion
}
