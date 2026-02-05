namespace NebulaPanel.Infrastructure.Services;

using System.Collections.Concurrent;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

/// <summary>
/// Service for managing Hytale OAuth2 authentication.
/// Uses Method B: Direct OAuth2 Device Code Flow API calls.
/// </summary>
public class HytaleAuthService : IHytaleAuthService
{
    private readonly IHytaleOAuthClient _oauthClient;
    private readonly IHytaleCredentialsRepository _credentialsRepo;
    private readonly IEncryptionService _encryption;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly HytaleAuthSettings _settings;
    private readonly ILogger<HytaleAuthService> _logger;

    /// <summary>
    /// Tracks in-progress authentication sessions by user ID.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, AuthSession> _pendingSessions = new();

    public event EventHandler<CredentialsExpiringEventArgs>? CredentialsExpiring;

    public HytaleAuthService(
        IHytaleOAuthClient oauthClient,
        IHytaleCredentialsRepository credentialsRepo,
        IEncryptionService encryption,
        IBackgroundJobClient backgroundJobClient,
        IOptions<HytaleAuthSettings> settings,
        ILogger<HytaleAuthService> logger)
    {
        _oauthClient = oauthClient;
        _credentialsRepo = credentialsRepo;
        _encryption = encryption;
        _backgroundJobClient = backgroundJobClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> HasValidCredentialsAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        return credentials?.IsValid == true;
    }

    /// <inheritdoc />
    public async Task<HytaleUserCredentials?> GetCredentialsAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        return credentials?.IsValid == true ? credentials : null;
    }

    /// <inheritdoc />
    public async Task<TimeSpan?> GetTokenTimeRemainingAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        return credentials?.TimeRemaining;
    }

    /// <inheritdoc />
    public async Task<HytaleAuthStartResult> StartAuthenticationAsync(Guid userId, CancellationToken ct = default)
    {
        // Cancel any existing session for this user
        await CancelAuthenticationAsync(userId, ct).ConfigureAwait(false);

        try
        {
            _logger.LogInformation("Starting Hytale OAuth device code flow for user {UserId}", userId);

            // Request device code via direct API call
            var deviceCode = await _oauthClient.RequestDeviceCodeAsync(ct).ConfigureAwait(false);

            // Track the session
            var session = new AuthSession
            {
                UserId = userId,
                DeviceCode = deviceCode.DeviceCode,
                UserCode = deviceCode.UserCode,
                VerificationUri = deviceCode.VerificationUri,
                VerificationUriComplete = deviceCode.VerificationUriComplete,
                ExpiresAt = deviceCode.ExpiresAt,
                PollingInterval = TimeSpan.FromSeconds(deviceCode.Interval),
                StartedAt = DateTime.UtcNow
            };
            _pendingSessions[userId] = session;

            _logger.LogInformation(
                "Started Hytale authentication for user {UserId}. User code: {UserCode}, Verification URI: {VerificationUri}",
                userId,
                deviceCode.UserCode,
                deviceCode.VerificationUri);

            var deviceCodeInfo = new HytaleDeviceCodeInfo
            {
                VerificationUrl = deviceCode.VerificationUriComplete ?? deviceCode.VerificationUri,
                UserCode = deviceCode.UserCode,
                ExpiresAt = deviceCode.ExpiresAt,
                PollingIntervalSeconds = deviceCode.Interval
            };

            return HytaleAuthStartResult.Succeeded(deviceCodeInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Hytale auth for user {UserId}", userId);
            return HytaleAuthStartResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<HytaleAuthPollResult> PollAuthenticationAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_pendingSessions.TryGetValue(userId, out var session))
        {
            return HytaleAuthPollResult.Failed("No pending auth session");
        }

        // Check if device code expired
        if (DateTime.UtcNow > session.ExpiresAt)
        {
            _pendingSessions.TryRemove(userId, out _);
            _logger.LogWarning("Hytale auth device code expired for user {UserId}", userId);
            return HytaleAuthPollResult.Expired();
        }

        try
        {
            // Poll token endpoint via direct API call
            var tokenResponse = await _oauthClient.PollForTokenAsync(session.DeviceCode, ct).ConfigureAwait(false);

            if (tokenResponse is null)
            {
                // Still waiting for user authorization
                return HytaleAuthPollResult.Pending();
            }

            // Auth succeeded! Save credentials
            _pendingSessions.TryRemove(userId, out _);

            // Fetch profile information
            var profiles = await _oauthClient.GetProfilesAsync(tokenResponse.AccessToken, ct).ConfigureAwait(false);

            string? username = null;
            Guid? profileUuid = null;
            Guid? ownerUuid = profiles.OwnerUuid;

            if (profiles.Profiles.Count > 0)
            {
                var profile = profiles.Profiles[0];
                username = profile.Username;
                profileUuid = profile.Uuid;
            }

            var credentials = new HytaleUserCredentials
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccessToken = _encryption.Encrypt(tokenResponse.AccessToken),
                RefreshToken = _encryption.Encrypt(tokenResponse.RefreshToken),
                ExpiresAt = tokenResponse.ExpiresAt,
                CreatedAt = DateTime.UtcNow,
                HytaleUsername = username,
                ProfileUuid = profileUuid,
                OwnerUuid = ownerUuid
            };

            await _credentialsRepo.UpsertAsync(credentials, ct).ConfigureAwait(false);

            // Schedule refresh and warning jobs
            ScheduleBackgroundJobs(credentials);

            _logger.LogInformation(
                "Hytale authentication completed for user {UserId} ({Username}). Expires at {ExpiresAt}",
                userId,
                username ?? "unknown",
                credentials.ExpiresAt);

            return HytaleAuthPollResult.Succeeded(credentials);
        }
        catch (DeviceCodeExpiredException)
        {
            _pendingSessions.TryRemove(userId, out _);
            _logger.LogWarning("Hytale auth device code expired for user {UserId}", userId);
            return HytaleAuthPollResult.Expired();
        }
        catch (AccessDeniedException)
        {
            _pendingSessions.TryRemove(userId, out _);
            _logger.LogWarning("Hytale auth denied by user {UserId}", userId);
            return HytaleAuthPollResult.Failed("Access denied by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling Hytale auth for user {UserId}", userId);
            return HytaleAuthPollResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public Task CancelAuthenticationAsync(Guid userId, CancellationToken ct = default)
    {
        if (_pendingSessions.TryRemove(userId, out _))
        {
            _logger.LogInformation("Cancelled Hytale auth session for user {UserId}", userId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> RefreshCredentialsAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (credentials is null)
        {
            _logger.LogWarning("Cannot refresh Hytale credentials for user {UserId}: no credentials found", userId);
            return false;
        }

        try
        {
            var refreshToken = _encryption.Decrypt(credentials.RefreshToken);

            // Refresh via direct API call
            var tokenResponse = await _oauthClient.RefreshTokenAsync(refreshToken, ct).ConfigureAwait(false);

            credentials.AccessToken = _encryption.Encrypt(tokenResponse.AccessToken);
            credentials.RefreshToken = _encryption.Encrypt(tokenResponse.RefreshToken);
            credentials.ExpiresAt = tokenResponse.ExpiresAt;
            credentials.LastRefreshedAt = DateTime.UtcNow;

            await _credentialsRepo.UpdateAsync(credentials, ct).ConfigureAwait(false);

            // Reschedule background jobs with new expiration
            ScheduleBackgroundJobs(credentials);

            _logger.LogInformation(
                "Refreshed Hytale credentials for user {UserId}. New expiration: {ExpiresAt}",
                userId,
                credentials.ExpiresAt);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh Hytale credentials for user {UserId}", userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RevokeCredentialsAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            await _credentialsRepo.DeleteAsync(userId, ct).ConfigureAwait(false);
            _logger.LogInformation("Revoked Hytale credentials for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke Hytale credentials for user {UserId}", userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<HytaleCredentials?> GetDecryptedCredentialsAsync(Guid userId, CancellationToken ct = default)
    {
        var credentials = await GetCredentialsAsync(userId, ct).ConfigureAwait(false);
        if (credentials is null)
        {
            return null;
        }

        // Update last used timestamp
        credentials.LastUsedAt = DateTime.UtcNow;
        await _credentialsRepo.UpdateAsync(credentials, ct).ConfigureAwait(false);

        return DecryptCredentials(credentials);
    }

    /// <inheritdoc />
    public async Task FireExpirationWarningAsync(Guid userId, TimeSpan warningThreshold, CancellationToken ct = default)
    {
        var credentials = await _credentialsRepo.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (credentials is null || !credentials.IsValid)
        {
            return;
        }

        var args = new CredentialsExpiringEventArgs
        {
            UserId = userId,
            TimeRemaining = credentials.TimeRemaining,
            WarningThreshold = warningThreshold,
            ExpiresAt = credentials.ExpiresAt
        };

        _logger.LogInformation(
            "Firing expiration warning for user {UserId}. Time remaining: {TimeRemaining}",
            userId,
            credentials.TimeRemaining);

        CredentialsExpiring?.Invoke(this, args);
    }

    /// <summary>
    /// Decrypts stored credentials to the DTO format used by the downloader service.
    /// </summary>
    private HytaleCredentials DecryptCredentials(HytaleUserCredentials stored)
    {
        return new HytaleCredentials
        {
            AccessToken = _encryption.Decrypt(stored.AccessToken),
            RefreshToken = _encryption.Decrypt(stored.RefreshToken),
            ExpiresAt = stored.ExpiresAt
        };
    }

    /// <summary>
    /// Schedules background jobs for token refresh and expiration warnings.
    /// </summary>
    private void ScheduleBackgroundJobs(HytaleUserCredentials credentials)
    {
        var now = DateTime.UtcNow;

        // Schedule refresh job
        var refreshAt = credentials.ExpiresAt - _settings.RefreshBeforeExpiration;
        if (refreshAt > now)
        {
            _backgroundJobClient.Schedule<IHytaleAuthService>(
                service => service.RefreshCredentialsAsync(credentials.UserId, CancellationToken.None),
                refreshAt);

            _logger.LogDebug(
                "Scheduled Hytale token refresh for user {UserId} at {RefreshAt}",
                credentials.UserId,
                refreshAt);
        }

        // Schedule warning jobs for each threshold
        foreach (var threshold in _settings.ExpirationWarningThresholds)
        {
            var warningAt = credentials.ExpiresAt - threshold;
            if (warningAt > now)
            {
                _backgroundJobClient.Schedule<IHytaleAuthService>(
                    service => service.FireExpirationWarningAsync(credentials.UserId, threshold, CancellationToken.None),
                    warningAt);

                _logger.LogDebug(
                    "Scheduled Hytale expiration warning for user {UserId} at {WarningAt} ({Threshold} before expiration)",
                    credentials.UserId,
                    warningAt,
                    threshold);
            }
        }
    }

    /// <summary>
    /// Tracks an in-progress authentication session.
    /// </summary>
    private sealed class AuthSession
    {
        public required Guid UserId { get; init; }
        public required string DeviceCode { get; init; }
        public required string UserCode { get; init; }
        public required string VerificationUri { get; init; }
        public string? VerificationUriComplete { get; init; }
        public required DateTime ExpiresAt { get; init; }
        public required TimeSpan PollingInterval { get; init; }
        public required DateTime StartedAt { get; init; }
    }
}
