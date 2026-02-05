namespace NebulaPanel.Application.Services;

using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Interfaces;

/// <summary>
/// Service for managing Hytale OAuth2 authentication for Nebula Panel users.
/// </summary>
public interface IHytaleAuthService
{
    /// <summary>
    /// Check if a user has valid (non-expired) Hytale credentials.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the user has valid credentials.</returns>
    Task<bool> HasValidCredentialsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get credentials for a user (null if not authenticated or expired).
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user's Hytale credentials, or null if not available/expired.</returns>
    Task<HytaleUserCredentials?> GetCredentialsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get time remaining until token expires.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Time remaining, or null if no credentials exist.</returns>
    Task<TimeSpan?> GetTokenTimeRemainingAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Start OAuth device code flow for a user.
    /// Returns the device code info to display to the user.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing device code info or error.</returns>
    Task<HytaleAuthStartResult> StartAuthenticationAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Poll for authentication completion.
    /// Call this periodically after StartAuthenticationAsync.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Poll result indicating status.</returns>
    Task<HytaleAuthPollResult> PollAuthenticationAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Cancel an in-progress authentication.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CancelAuthenticationAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Refresh credentials using the refresh token.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if refresh succeeded.</returns>
    Task<bool> RefreshCredentialsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Revoke/disconnect Hytale account.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if revocation succeeded.</returns>
    Task<bool> RevokeCredentialsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the decrypted Hytale credentials DTO for use with the downloader service.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The decrypted credentials DTO, or null if not available.</returns>
    Task<HytaleCredentials?> GetDecryptedCredentialsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Fires an expiration warning for the specified user.
    /// Called by background jobs when a warning threshold is reached.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="warningThreshold">The threshold that triggered this warning.</param>
    /// <param name="ct">Cancellation token.</param>
    Task FireExpirationWarningAsync(Guid userId, TimeSpan warningThreshold, CancellationToken ct = default);

    /// <summary>
    /// Event fired when credentials are about to expire.
    /// </summary>
    event EventHandler<CredentialsExpiringEventArgs>? CredentialsExpiring;
}

/// <summary>
/// Result of starting a Hytale authentication flow.
/// </summary>
public record HytaleAuthStartResult
{
    /// <summary>
    /// Whether the authentication flow started successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the flow failed to start.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Device code information for the user to complete authentication.
    /// </summary>
    public HytaleDeviceCodeInfo? DeviceCode { get; init; }

    public static HytaleAuthStartResult Succeeded(HytaleDeviceCodeInfo deviceCode) =>
        new() { Success = true, DeviceCode = deviceCode };

    public static HytaleAuthStartResult Failed(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Result of polling for authentication completion.
/// </summary>
public record HytaleAuthPollResult
{
    /// <summary>
    /// Current status of the authentication.
    /// </summary>
    public HytaleAuthPollStatus Status { get; init; }

    /// <summary>
    /// The saved credentials if authentication succeeded.
    /// </summary>
    public HytaleUserCredentials? Credentials { get; init; }

    /// <summary>
    /// Error message if authentication failed.
    /// </summary>
    public string? Error { get; init; }

    public static HytaleAuthPollResult Pending() =>
        new() { Status = HytaleAuthPollStatus.Pending };

    public static HytaleAuthPollResult Succeeded(HytaleUserCredentials credentials) =>
        new() { Status = HytaleAuthPollStatus.Success, Credentials = credentials };

    public static HytaleAuthPollResult Expired() =>
        new() { Status = HytaleAuthPollStatus.Expired };

    public static HytaleAuthPollResult Failed(string error) =>
        new() { Status = HytaleAuthPollStatus.Failed, Error = error };
}

/// <summary>
/// Status of an authentication poll.
/// </summary>
public enum HytaleAuthPollStatus
{
    /// <summary>
    /// User hasn't completed auth yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Auth completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Device code expired.
    /// </summary>
    Expired,

    /// <summary>
    /// Auth failed.
    /// </summary>
    Failed
}

/// <summary>
/// Event args for credentials expiring event.
/// </summary>
public class CredentialsExpiringEventArgs : EventArgs
{
    /// <summary>
    /// The user whose credentials are expiring.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Time remaining until expiration.
    /// </summary>
    public required TimeSpan TimeRemaining { get; init; }

    /// <summary>
    /// The warning threshold that triggered this event.
    /// </summary>
    public required TimeSpan WarningThreshold { get; init; }

    /// <summary>
    /// When the credentials expire.
    /// </summary>
    public required DateTime ExpiresAt { get; init; }
}
