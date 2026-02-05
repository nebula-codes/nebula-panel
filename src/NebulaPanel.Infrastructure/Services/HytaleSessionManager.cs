using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Manages Hytale game session lifecycle (create/refresh/end).
/// Sessions are required for authenticating dedicated servers.
/// </summary>
public class HytaleSessionManager : IHytaleSessionManager
{
    private readonly IHytaleOAuthClient _oauthClient;
    private readonly IHytaleCredentialsRepository _credentialsRepo;
    private readonly IHytaleAuthService _authService;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<HytaleSessionManager> _logger;

    /// <summary>
    /// Refresh session if it expires within this threshold.
    /// </summary>
    private static readonly TimeSpan SessionRefreshThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Refresh OAuth access token if it expires within this threshold.
    /// </summary>
    private static readonly TimeSpan AccessTokenRefreshThreshold = TimeSpan.FromMinutes(5);

    public HytaleSessionManager(
        IHytaleOAuthClient oauthClient,
        IHytaleCredentialsRepository credentialsRepo,
        IHytaleAuthService authService,
        IEncryptionService encryption,
        ILogger<HytaleSessionManager> logger)
    {
        _oauthClient = oauthClient;
        _credentialsRepo = credentialsRepo;
        _authService = authService;
        _encryption = encryption;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HytaleFullCredentials?> EnsureSessionAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (credentials is null)
        {
            _logger.LogWarning("No Hytale credentials found for user {UserId}", userId);
            return null;
        }

        // If access token is expired or expiring soon, try to refresh it
        if (!credentials.IsValid || credentials.TimeRemaining < AccessTokenRefreshThreshold)
        {
            _logger.LogInformation(
                "OAuth access token expired or expiring soon for user {UserId}, attempting refresh",
                userId);

            var refreshSuccess = await _authService.RefreshCredentialsAsync(userId, ct).ConfigureAwait(false);
            if (!refreshSuccess)
            {
                _logger.LogWarning(
                    "Failed to refresh OAuth credentials for user {UserId}. User may need to re-authenticate.",
                    userId);
                return null;
            }

            // Reload credentials after refresh
            credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
            if (credentials is null || !credentials.IsValid)
            {
                _logger.LogWarning("Credentials still invalid after refresh for user {UserId}", userId);
                return null;
            }

            _logger.LogInformation(
                "Successfully refreshed OAuth access token for user {UserId}, expires at {ExpiresAt}",
                userId, credentials.ExpiresAt);
        }

        // Check if we need to create or refresh game session
        var needsNewSession = !credentials.IsSessionValid;
        var needsRefresh = credentials.IsSessionValid &&
                          credentials.SessionTimeRemaining < SessionRefreshThreshold;

        if (needsNewSession)
        {
            _logger.LogInformation("Creating new game session for user {UserId}", userId);
            if (!await CreateSessionAsync(userId, ct).ConfigureAwait(false))
            {
                return null;
            }
            credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        }
        else if (needsRefresh)
        {
            _logger.LogInformation("Refreshing game session for user {UserId} (expires in {TimeRemaining})",
                userId, credentials.SessionTimeRemaining);
            await RefreshSessionAsync(userId, ct).ConfigureAwait(false);
            credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        }

        if (credentials is null)
        {
            return null;
        }

        return new HytaleFullCredentials
        {
            AccessToken = _encryption.Decrypt(credentials.AccessToken),
            RefreshToken = _encryption.Decrypt(credentials.RefreshToken),
            AccessTokenExpiresAt = credentials.ExpiresAt,
            SessionToken = credentials.SessionToken is not null
                ? _encryption.Decrypt(credentials.SessionToken)
                : null,
            IdentityToken = credentials.IdentityToken is not null
                ? _encryption.Decrypt(credentials.IdentityToken)
                : null,
            SessionExpiresAt = credentials.SessionExpiresAt,
            ProfileUuid = credentials.ProfileUuid,
            OwnerUuid = credentials.OwnerUuid,
            Username = credentials.HytaleUsername
        };
    }

    /// <inheritdoc />
    public async Task<bool> CreateSessionAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (credentials is null || !credentials.IsValid)
        {
            _logger.LogWarning("Cannot create session: no valid OAuth credentials for user {UserId}", userId);
            return false;
        }

        try
        {
            var accessToken = _encryption.Decrypt(credentials.AccessToken);

            // If we don't have a profile yet, fetch profiles and use the first one
            if (!credentials.ProfileUuid.HasValue)
            {
                _logger.LogInformation("No profile selected for user {UserId}, fetching profiles", userId);
                var profiles = await _oauthClient.GetProfilesAsync(accessToken, ct).ConfigureAwait(false);

                if (profiles.Profiles.Count == 0)
                {
                    _logger.LogError("No profiles available for user {UserId}", userId);
                    return false;
                }

                var profile = profiles.Profiles[0];
                credentials.ProfileUuid = profile.Uuid;
                credentials.OwnerUuid = profiles.OwnerUuid;
                credentials.HytaleUsername = profile.Username;

                _logger.LogInformation("Selected profile {Username} ({ProfileUuid}) for user {UserId}",
                    profile.Username, profile.Uuid, userId);
            }

            // Create the game session
            var session = await _oauthClient.CreateGameSessionAsync(
                accessToken,
                credentials.ProfileUuid.Value,
                ct).ConfigureAwait(false);

            // Store encrypted session tokens
            credentials.SessionToken = _encryption.Encrypt(session.SessionToken);
            credentials.IdentityToken = _encryption.Encrypt(session.IdentityToken);
            credentials.SessionExpiresAt = session.ExpiresAt;

            await _credentialsRepo.UpdateAsync(credentials, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Created game session for user {UserId}, expires at {ExpiresAt}",
                userId, session.ExpiresAt);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create game session for user {UserId}", userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RefreshSessionAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (credentials?.SessionToken is null)
        {
            _logger.LogWarning("Cannot refresh session: no session exists for user {UserId}", userId);
            return false;
        }

        try
        {
            var sessionToken = _encryption.Decrypt(credentials.SessionToken);
            var session = await _oauthClient.RefreshGameSessionAsync(sessionToken, ct).ConfigureAwait(false);

            // Store updated session tokens
            credentials.SessionToken = _encryption.Encrypt(session.SessionToken);
            credentials.IdentityToken = _encryption.Encrypt(session.IdentityToken);
            credentials.SessionExpiresAt = session.ExpiresAt;

            await _credentialsRepo.UpdateAsync(credentials, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Refreshed game session for user {UserId}, expires at {ExpiresAt}",
                userId, session.ExpiresAt);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh game session for user {UserId}", userId);

            // If refresh fails, try to create a new session
            _logger.LogInformation("Attempting to create new session after refresh failure");
            return await CreateSessionAsync(userId, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task EndSessionAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (credentials?.SessionToken is null)
        {
            _logger.LogDebug("No session to end for user {UserId}", userId);
            return;
        }

        try
        {
            var sessionToken = _encryption.Decrypt(credentials.SessionToken);
            await _oauthClient.EndGameSessionAsync(sessionToken, ct).ConfigureAwait(false);

            // Clear session tokens from storage
            credentials.SessionToken = null;
            credentials.IdentityToken = null;
            credentials.SessionExpiresAt = null;

            await _credentialsRepo.UpdateAsync(credentials, ct).ConfigureAwait(false);

            _logger.LogInformation("Ended game session for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error ending game session for user {UserId}", userId);

            // Still clear local session data even if API call fails
            credentials.SessionToken = null;
            credentials.IdentityToken = null;
            credentials.SessionExpiresAt = null;
            await _credentialsRepo.UpdateAsync(credentials, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<(string SessionToken, string IdentityToken)?> GetSessionTokensAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (credentials?.SessionToken is null ||
            credentials.IdentityToken is null ||
            !credentials.IsSessionValid)
        {
            return null;
        }

        return (
            _encryption.Decrypt(credentials.SessionToken),
            _encryption.Decrypt(credentials.IdentityToken)
        );
    }

    /// <inheritdoc />
    public async Task<bool> HasValidSessionAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        return credentials?.IsSessionValid == true;
    }
}
