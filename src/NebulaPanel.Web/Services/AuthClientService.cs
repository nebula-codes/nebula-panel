namespace NebulaPanel.Web.Services;

using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;

public class AuthClientService : IAuthClientService, IDisposable
{
    private readonly IAuthService _authService;
    private readonly AuthStateProvider _authStateProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthClientService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _isRefreshing;

    public event EventHandler? OnTokenExpiring;
    public event EventHandler? OnSessionExpired;

    public AuthClientService(
        IAuthService authService,
        AuthStateProvider authStateProvider,
        IHttpContextAccessor httpContextAccessor,
        HttpClient httpClient,
        ILogger<AuthClientService> logger)
    {
        _authService = authService;
        _authStateProvider = authStateProvider;
        _httpContextAccessor = httpContextAccessor;
        _httpClient = httpClient;
        _logger = logger;

        // Subscribe to token expiring event from AuthStateProvider
        _authStateProvider.OnTokenExpiring += HandleTokenExpiring;
        _authStateProvider.OnSessionExpired += HandleSessionExpired;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            var ipAddress = GetClientIpAddress();
            var userAgent = GetUserAgent();

            var result = await _authService.LoginAsync(
                new LoginRequest(username, password), ipAddress, userAgent);

            if (result.IsSuccess)
            {
                await _authStateProvider.SetTokenAsync(result.Value!.AccessToken);
                return new AuthResult(true, User: result.Value.User);
            }

            return MapAuthError(result.ErrorInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return new AuthResult(false, ex.Message);
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var token = await _authStateProvider.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                // Call the logout endpoint to revoke refresh tokens
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    await _httpClient.SendAsync(request);
                }
                catch
                {
                    // Ignore errors during logout API call
                }
            }
        }
        catch
        {
            // Ignore errors during logout
        }
        finally
        {
            await _authStateProvider.ClearTokenAsync();
        }
    }

    public async Task<AuthResult> RefreshTokenAsync()
    {
        // Prevent concurrent refresh attempts
        if (_isRefreshing)
        {
            // Wait for the current refresh to complete
            await _refreshLock.WaitAsync();
            _refreshLock.Release();

            // Check if refresh was successful
            var token = await _authStateProvider.GetTokenAsync();
            if (!string.IsNullOrEmpty(token) && !_authStateProvider.IsTokenExpired())
            {
                return new AuthResult(true);
            }

            return new AuthResult(false, "Token refresh failed", "SessionExpired");
        }

        await _refreshLock.WaitAsync();
        _isRefreshing = true;

        try
        {
            _logger.LogDebug("Attempting to refresh access token");

            var response = await _httpClient.PostAsync("api/auth/refresh", null);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (authResponse != null)
                {
                    await _authStateProvider.SetTokenAsync(authResponse.AccessToken);
                    _logger.LogDebug("Token refresh successful");
                    return new AuthResult(true, User: authResponse.User);
                }
            }

            // Handle error response
            var errorResponse = await TryParseErrorResponse(response);
            _logger.LogWarning("Token refresh failed: {Error}", errorResponse?.Error ?? "Unknown error");

            return new AuthResult(
                false,
                errorResponse?.Error ?? "Token refresh failed",
                errorResponse?.ErrorCode ?? "SessionExpired");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return new AuthResult(false, "Token refresh failed", "SessionExpired");
        }
        finally
        {
            _isRefreshing = false;
            _refreshLock.Release();
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        try
        {
            var token = await _authStateProvider.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            // Parse user ID from token claims
            var claims = ParseClaimsFromToken(token);
            var userIdClaim = claims.FirstOrDefault(c => c.Type == "sub" || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            var result = await _authService.GetCurrentUserAsync(userId);
            return result.IsSuccess ? result.Value : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _authStateProvider.GetTokenAsync();
        return !string.IsNullOrEmpty(token) && !_authStateProvider.IsTokenExpired();
    }

    private async void HandleTokenExpiring(object? sender, EventArgs e)
    {
        _logger.LogDebug("Token expiring, attempting automatic refresh");

        var result = await RefreshTokenAsync();

        if (result.Success)
        {
            _logger.LogDebug("Automatic token refresh successful");
        }
        else
        {
            _logger.LogWarning("Automatic token refresh failed");
            OnTokenExpiring?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandleSessionExpired(object? sender, EventArgs e)
    {
        _logger.LogInformation("Session expired");
        OnSessionExpired?.Invoke(this, EventArgs.Empty);
    }

    private static async Task<AuthErrorResponse?> TryParseErrorResponse(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<AuthErrorResponse>();
        }
        catch
        {
            return null;
        }
    }

    private static AuthResult MapAuthError(Error? error)
    {
        if (error is null)
            return new AuthResult(false, "Login failed");

        return error.Code switch
        {
            ErrorCode.RateLimited => new AuthResult(
                false,
                error.Message,
                "RateLimited",
                RetryAfterSeconds: int.TryParse(error.Details, out var seconds) ? seconds : null),

            ErrorCode.AccountLocked => new AuthResult(
                false,
                error.Message,
                "AccountLocked",
                LockoutEndTime: DateTime.TryParse(error.Details, out var endTime) ? endTime : null),

            ErrorCode.AccountDisabled => new AuthResult(false, error.Message, "AccountDisabled"),
            ErrorCode.InvalidCredentials => new AuthResult(false, error.Message, "InvalidCredentials"),
            _ => new AuthResult(false, error.Message, error.Code.ToString())
        };
    }

    private string? GetClientIpAddress()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null) return null;

        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ip = forwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(ip)) return ip;
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp)) return realIp;

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.FirstOrDefault();
    }

    private static IEnumerable<System.Security.Claims.Claim> ParseClaimsFromToken(string jwt)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        try
        {
            var token = handler.ReadJwtToken(jwt);
            return token.Claims;
        }
        catch
        {
            return [];
        }
    }

    public void Dispose()
    {
        _authStateProvider.OnTokenExpiring -= HandleTokenExpiring;
        _authStateProvider.OnSessionExpired -= HandleSessionExpired;
        _refreshLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
