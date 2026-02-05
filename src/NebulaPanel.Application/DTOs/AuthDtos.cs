namespace NebulaPanel.Application.DTOs;

public record LoginRequest(string Username, string Password);

public record RegisterRequest(string Username, string Email, string Password);

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiration,
    int ExpiresInSeconds,
    UserDto User
);

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

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    IReadOnlyList<Guid>? RoleIds
);

public record UpdateUserRequest(
    string? Username,
    string? Email,
    bool? IsActive,
    IReadOnlyList<Guid>? RoleIds
);
