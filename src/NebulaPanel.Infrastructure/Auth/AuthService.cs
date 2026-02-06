namespace NebulaPanel.Infrastructure.Auth;

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class AuthService(
    NebulaPanelDbContext context,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IJwtService jwtService,
    IPermissionService permissionService,
    ILoginRateLimiter rateLimiter,
    ISecurityAuditService auditService,
    IOptions<JwtSettings> jwtSettings,
    IOptions<AccountLockoutSettings> lockoutSettings,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;
    private readonly AccountLockoutSettings _lockoutSettings = lockoutSettings.Value;

    public async Task<Result<AuthTokenResult>> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        // Check rate limiting first
        if (!rateLimiter.IsAllowed(ipAddress, request.Username))
        {
            var retryAfter = rateLimiter.GetRetryAfterSeconds(ipAddress, request.Username);
            logger.LogWarning("Login attempt rate limited for user {Username} from IP {IpAddress}", request.Username, ipAddress);

            await auditService.LogLoginRateLimitedAsync(request.Username, ipAddress, userAgent, retryAfter, ct)
                .ConfigureAwait(false);

            return Result.Failure<AuthTokenResult>(Error.RateLimited(retryAfter));
        }

        // Record the attempt for rate limiting
        rateLimiter.RecordAttempt(ipAddress, request.Username);

        var user = await userRepository.GetByUsernameAsync(request.Username, ct).ConfigureAwait(false);

        if (user == null)
        {
            logger.LogWarning("Login attempt failed: user {Username} not found", request.Username);
            await auditService.LogLoginFailedAsync(request.Username, ipAddress, userAgent, "User not found", ct)
                .ConfigureAwait(false);
            return Result.Failure<AuthTokenResult>(Error.InvalidCredentials());
        }

        // Check if account is locked
        if (user.IsLockedOut)
        {
            logger.LogWarning("Login attempt failed: user {Username} is locked out until {LockoutEnd}",
                request.Username, user.LockoutEndTime);

            await auditService.LogEventAsync(
                Domain.Enums.SecurityEventType.LoginFailedAccountLocked,
                user.Id,
                user.Username,
                ipAddress,
                userAgent,
                false,
                $"Account locked until {user.LockoutEndTime:u}",
                ct).ConfigureAwait(false);

            return Result.Failure<AuthTokenResult>(Error.AccountLocked(user.LockoutEndTime));
        }

        // Check if lockout has expired and reset if needed
        if (user.LockoutEndTime.HasValue && user.LockoutEndTime.Value <= DateTime.UtcNow)
        {
            user.LockoutEndTime = null;
            user.FailedLoginAttempts = 0;
        }

        // Reset failed attempts if enough time has passed since last failure
        if (user.LastFailedLoginAt.HasValue &&
            user.LastFailedLoginAt.Value.AddMinutes(_lockoutSettings.FailedAttemptResetMinutes) < DateTime.UtcNow)
        {
            user.FailedLoginAttempts = 0;
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Login attempt failed: user {Username} is inactive", request.Username);
            await auditService.LogLoginFailedAsync(request.Username, ipAddress, userAgent, "Account disabled", ct)
                .ConfigureAwait(false);
            return Result.Failure<AuthTokenResult>(Error.AccountDisabled());
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Login attempt failed: invalid password for user {Username}", request.Username);

            // Increment failed attempts
            user.FailedLoginAttempts++;
            user.LastFailedLoginAt = DateTime.UtcNow;

            // Check if we should lock the account
            if (user.FailedLoginAttempts >= _lockoutSettings.MaxFailedAttempts)
            {
                user.LockoutEndTime = DateTime.UtcNow.AddMinutes(_lockoutSettings.LockoutDurationMinutes);
                await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);

                logger.LogWarning("Account {Username} locked until {LockoutEnd} after {Attempts} failed attempts",
                    request.Username, user.LockoutEndTime, user.FailedLoginAttempts);

                await auditService.LogAccountLockedAsync(user.Id, user.Username, ipAddress, userAgent, user.LockoutEndTime.Value, ct)
                    .ConfigureAwait(false);

                return Result.Failure<AuthTokenResult>(Error.AccountLocked(user.LockoutEndTime));
            }

            await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
            await auditService.LogLoginFailedAsync(request.Username, ipAddress, userAgent, "Invalid password", ct)
                .ConfigureAwait(false);

            return Result.Failure<AuthTokenResult>(Error.InvalidCredentials());
        }

        // Successful login - reset failed attempts and clear rate limit
        user.FailedLoginAttempts = 0;
        user.LastFailedLoginAt = null;
        user.LockoutEndTime = null;
        rateLimiter.ClearAttempts(ipAddress, request.Username);

        // Get user roles and permissions
        var roles = await permissionService.GetUserRolesAsync(user.Id, ct).ConfigureAwait(false);
        var permissions = await permissionService.GetUserPermissionsAsync(user.Id, ct).ConfigureAwait(false);

        // Generate tokens
        var accessToken = jwtService.GenerateAccessToken(user, roles, permissions);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, ct).ConfigureAwait(false);

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);

        logger.LogInformation("User {Username} logged in successfully", request.Username);
        await auditService.LogLoginSuccessAsync(user.Id, user.Username, ipAddress, userAgent, ct)
            .ConfigureAwait(false);

        var expiresInSeconds = _jwtSettings.AccessTokenExpirationMinutes * 60;
        var response = new AuthResponse(
            accessToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            expiresInSeconds,
            ToUserDto(user, roles, permissions)
        );
        return new AuthTokenResult(response, refreshToken);
    }

    public async Task<Result<AuthTokenResult>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // Check if username exists
        if (await userRepository.ExistsByUsernameAsync(request.Username, ct).ConfigureAwait(false))
        {
            return Result.Failure<AuthTokenResult>(Error.AlreadyExists("Username", request.Username));
        }

        // Check if email exists
        if (await userRepository.ExistsByEmailAsync(request.Email, ct).ConfigureAwait(false))
        {
            return Result.Failure<AuthTokenResult>(Error.AlreadyExists("Email", request.Email));
        }

        // Check if this is the first user (will be admin)
        var isFirstUser = await userRepository.CountAsync(ct).ConfigureAwait(false) == 0;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        await userRepository.AddAsync(user, ct).ConfigureAwait(false);

        // Assign default role (or Admin for first user)
        var roleName = isFirstUser ? "Admin" : "User";
        var role = await roleRepository.GetByNameAsync(roleName, ct).ConfigureAwait(false);

        if (role != null)
        {
            var userRole = new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                AssignedAt = DateTime.UtcNow
            };
            context.UserRoles.Add(userRole);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        // Get user roles and permissions
        var roles = await permissionService.GetUserRolesAsync(user.Id, ct).ConfigureAwait(false);
        var permissions = await permissionService.GetUserPermissionsAsync(user.Id, ct).ConfigureAwait(false);

        // Generate tokens
        var accessToken = jwtService.GenerateAccessToken(user, roles, permissions);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, ct).ConfigureAwait(false);

        logger.LogInformation("User {Username} registered successfully (first user: {IsFirstUser})", request.Username, isFirstUser);

        var expiresInSeconds = _jwtSettings.AccessTokenExpirationMinutes * 60;
        var response = new AuthResponse(
            accessToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            expiresInSeconds,
            ToUserDto(user, roles, permissions)
        );
        return new AuthTokenResult(response, refreshToken);
    }

    public async Task<Result<AuthTokenResult>> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        var tokenHash = HashToken(refreshToken);
        var token = await context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct)
            .ConfigureAwait(false);

        if (token == null)
        {
            return Result.Failure<AuthTokenResult>(Error.InvalidCredentials("Invalid refresh token"));
        }

        if (!token.IsActive)
        {
            await auditService.LogTokenRefreshAsync(
                token.UserId,
                token.User.Username,
                ipAddress,
                userAgent,
                success: false,
                "Token expired or revoked",
                ct).ConfigureAwait(false);

            return Result.Failure<AuthTokenResult>(Error.TokenExpired());
        }

        if (!token.User.IsActive)
        {
            await auditService.LogTokenRefreshAsync(
                token.UserId,
                token.User.Username,
                ipAddress,
                userAgent,
                success: false,
                "Account disabled",
                ct).ConfigureAwait(false);

            return Result.Failure<AuthTokenResult>(Error.AccountDisabled());
        }

        // Check if account is locked
        if (token.User.IsLockedOut)
        {
            await auditService.LogTokenRefreshAsync(
                token.UserId,
                token.User.Username,
                ipAddress,
                userAgent,
                success: false,
                "Account locked",
                ct).ConfigureAwait(false);

            return Result.Failure<AuthTokenResult>(Error.AccountLocked(token.User.LockoutEndTime));
        }

        // Revoke current token
        token.RevokedAt = DateTime.UtcNow;

        // Create new refresh token
        var newRefreshToken = await CreateRefreshTokenAsync(token.UserId, ct).ConfigureAwait(false);
        token.ReplacedByTokenHash = HashToken(newRefreshToken);

        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        // Get user roles and permissions
        var roles = await permissionService.GetUserRolesAsync(token.UserId, ct).ConfigureAwait(false);
        var permissions = await permissionService.GetUserPermissionsAsync(token.UserId, ct).ConfigureAwait(false);

        // Generate new access token
        var accessToken = jwtService.GenerateAccessToken(token.User, roles, permissions);

        await auditService.LogTokenRefreshAsync(
            token.UserId,
            token.User.Username,
            ipAddress,
            userAgent,
            success: true,
            ct: ct).ConfigureAwait(false);

        var expiresInSeconds = _jwtSettings.AccessTokenExpirationMinutes * 60;
        var response = new AuthResponse(
            accessToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            expiresInSeconds,
            ToUserDto(token.User, roles, permissions)
        );
        return new AuthTokenResult(response, newRefreshToken);
    }

    public async Task<Result> RevokeRefreshTokenAsync(
        Guid userId,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        var tokens = await context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Revoked all refresh tokens for user {UserId}", userId);

        // Get user for audit logging
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user != null)
        {
            await auditService.LogLogoutAsync(userId, user.Username, ipAddress, userAgent, ct)
                .ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);

        if (user == null)
        {
            return Result.Failure(Error.NotFound("User", userId.ToString()));
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(Error.InvalidCredentials("Current password is incorrect"));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);

        // Revoke all refresh tokens
        await RevokeRefreshTokenAsync(userId, ipAddress, userAgent, ct).ConfigureAwait(false);

        logger.LogInformation("Password changed for user {UserId}", userId);
        await auditService.LogPasswordChangedAsync(userId, user.Username, ipAddress, userAgent, ct)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);

        if (user == null)
        {
            return Result.Failure<UserDto>(Error.NotFound("User", userId.ToString()));
        }

        var roles = await permissionService.GetUserRolesAsync(userId, ct).ConfigureAwait(false);
        var permissions = await permissionService.GetUserPermissionsAsync(userId, ct).ConfigureAwait(false);

        return ToUserDto(user, roles, permissions);
    }

    public Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        var result = jwtService.ValidateAccessToken(token);
        return Task.FromResult(result != null);
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId, CancellationToken ct)
    {
        var rawToken = jwtService.GenerateRefreshToken();
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        return rawToken;
    }

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static UserDto ToUserDto(User user, IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        return new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt,
            roles,
            permissions
        );
    }
}
