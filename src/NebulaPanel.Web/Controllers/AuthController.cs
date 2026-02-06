namespace NebulaPanel.Web.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;

[ApiController]
[Route("api/[controller]")]
[IgnoreAntiforgeryToken]  // JWT auth provides CSRF protection
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ipAddress = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await authService.LoginAsync(request, ipAddress, userAgent, ct);

        if (result.IsFailure)
        {
            return HandleAuthError(result.ErrorInfo);
        }

        // Set refresh token in HTTP-only cookie
        SetRefreshTokenCookie(result.Value!.AccessToken);

        return Ok(result.Value);
    }

    [Authorize(Policy = "CreateUsers")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken(CancellationToken ct)
    {
        var refreshToken = Request.Cookies["refreshToken"];

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new AuthErrorResponse("Refresh token not found", "TokenExpired"));
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await authService.RefreshTokenAsync(refreshToken, ipAddress, userAgent, ct);

        if (result.IsFailure)
        {
            return HandleAuthError(result.ErrorInfo);
        }

        SetRefreshTokenCookie(result.Value!.AccessToken);

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = GetUserAgent();

        await authService.RevokeRefreshTokenAsync(userId.Value, ipAddress, userAgent, ct);

        // Remove refresh token cookie
        Response.Cookies.Delete("refreshToken");

        return Ok(new { message = "Logged out successfully" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await authService.GetCurrentUserAsync(userId.Value, ct);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await authService.ChangePasswordAsync(userId.Value, request, ipAddress, userAgent, ct);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { message = "Password changed successfully" });
    }

    private IActionResult HandleAuthError(Error? error)
    {
        if (error == null)
        {
            return Unauthorized(new AuthErrorResponse("Authentication failed"));
        }

        return error.Code switch
        {
            ErrorCode.RateLimited => HandleRateLimitError(error),
            ErrorCode.AccountLocked => HandleAccountLockedError(error),
            ErrorCode.AccountDisabled => Unauthorized(new AuthErrorResponse(
                error.Message,
                "AccountDisabled")),
            ErrorCode.InvalidCredentials => Unauthorized(new AuthErrorResponse(
                error.Message,
                "InvalidCredentials")),
            ErrorCode.TokenExpired => Unauthorized(new AuthErrorResponse(
                error.Message,
                "TokenExpired")),
            _ => Unauthorized(new AuthErrorResponse(error.Message, error.Code.ToString()))
        };
    }

    private IActionResult HandleRateLimitError(Error error)
    {
        int? retryAfterSeconds = null;
        if (!string.IsNullOrEmpty(error.Details) && int.TryParse(error.Details, out var seconds))
        {
            retryAfterSeconds = seconds;
            Response.Headers.Append("Retry-After", seconds.ToString());
        }

        return StatusCode(429, new AuthErrorResponse(
            error.Message,
            "RateLimited",
            retryAfterSeconds));
    }

    private IActionResult HandleAccountLockedError(Error error)
    {
        DateTime? lockoutEndTime = null;
        if (!string.IsNullOrEmpty(error.Details) && DateTime.TryParse(error.Details, out var endTime))
        {
            lockoutEndTime = endTime;
        }

        return Unauthorized(new AuthErrorResponse(
            error.Message,
            "AccountLocked",
            LockoutEndTime: lockoutEndTime));
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }

    private string? GetClientIpAddress()
    {
        // Check for forwarded IP (behind proxy/load balancer)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain (original client)
            var ip = forwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        // Check X-Real-IP header
        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to connection remote IP
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return Request.Headers.UserAgent.FirstOrDefault();
    }

    private void SetRefreshTokenCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        };

        Response.Cookies.Append("refreshToken", token, cookieOptions);
    }
}
