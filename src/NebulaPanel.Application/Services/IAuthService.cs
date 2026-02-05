namespace NebulaPanel.Application.Services;

using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

public interface IAuthService
{
    Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default);

    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    Task<Result<AuthResponse>> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default);

    Task<Result> RevokeRefreshTokenAsync(
        Guid userId,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default);

    Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default);

    Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);

    Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default);
}
