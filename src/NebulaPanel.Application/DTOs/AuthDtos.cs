using System.ComponentModel.DataAnnotations;

namespace NebulaPanel.Application.DTOs;

public record LoginRequest(
    [Required, StringLength(50)] string Username,
    [Required, StringLength(128)] string Password);

public record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password);

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiration,
    int ExpiresInSeconds,
    UserDto User
);

/// <summary>
/// Internal result carrying both the client-facing auth response and the raw refresh token for cookie storage.
/// The refresh token must NOT be serialized to the client — it belongs in an HTTP-only cookie.
/// </summary>
public record AuthTokenResult(AuthResponse Response, string RefreshToken);

public record RefreshTokenRequest(string RefreshToken);

/// <summary>
/// Response returned when an authentication error occurs with additional context.
/// </summary>
public record AuthErrorResponse(
    string Error,
    string? ErrorCode = null,
    int? RetryAfterSeconds = null,
    DateTime? LockoutEndTime = null
);

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions
);

public record ChangePasswordRequest(
    [Required, StringLength(128)] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = 8)] string NewPassword);

public record CreateUserRequest(
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    IReadOnlyList<Guid>? RoleIds
);

public record UpdateUserRequest(
    string? Username,
    string? Email,
    bool? IsActive,
    IReadOnlyList<Guid>? RoleIds
);
