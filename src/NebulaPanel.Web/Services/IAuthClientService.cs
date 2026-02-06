namespace NebulaPanel.Web.Services;

using NebulaPanel.Application.DTOs;

public interface IAuthClientService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task LogoutAsync();
    Task<UserDto?> GetCurrentUserAsync();
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Attempts to refresh the access token using the refresh token cookie.
    /// </summary>
    Task<AuthResult> RefreshTokenAsync();

    /// <summary>
    /// Event raised when the token is about to expire.
    /// </summary>
    event EventHandler? OnTokenExpiring;

    /// <summary>
    /// Event raised when the session has expired and cannot be refreshed.
    /// </summary>
    event EventHandler? OnSessionExpired;
}

public record AuthResult(
    bool Success,
    string? Error = null,
    string? ErrorCode = null,
    UserDto? User = null,
    int? RetryAfterSeconds = null,
    DateTime? LockoutEndTime = null
)
{
    /// <summary>
    /// Returns true if the error is due to rate limiting.
    /// </summary>
    public bool IsRateLimited => ErrorCode == "RateLimited";

    /// <summary>
    /// Returns true if the error is due to account lockout.
    /// </summary>
    public bool IsAccountLocked => ErrorCode == "AccountLocked";
}
