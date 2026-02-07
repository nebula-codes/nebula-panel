namespace NebulaPanel.Web.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

public class AuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AuthStateProvider> _logger;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private Timer? _tokenExpirationTimer;
    private DateTime? _tokenExpiration;

    /// <summary>
    /// Number of seconds before token expiration to trigger the OnTokenExpiring event.
    /// </summary>
    private const int TokenExpirationWarningSeconds = 60;

    /// <summary>
    /// Event raised when the token is about to expire (within TokenExpirationWarningSeconds).
    /// </summary>
    public event EventHandler? OnTokenExpiring;

    /// <summary>
    /// Event raised when the session has expired.
    /// </summary>
    public event EventHandler? OnSessionExpired;

    public AuthStateProvider(IJSRuntime jsRuntime, ILogger<AuthStateProvider> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        _logger.LogDebug("[AuthState] GetAuthenticationStateAsync called. JSRuntime type: {JSRuntimeType}",
            _jsRuntime.GetType().Name);

        try
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("[AuthState] GetAuthenticationStateAsync: token is null/empty — returning anonymous");
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            _logger.LogDebug("[AuthState] GetAuthenticationStateAsync: token retrieved (length={TokenLength})", token.Length);

            var claims = ParseClaimsFromJwt(token);
            var claimsList = claims.ToList();
            var identity = new ClaimsIdentity(claimsList, "jwt");
            _currentUser = new ClaimsPrincipal(identity);

            var sub = claimsList.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
            _logger.LogInformation("[AuthState] GetAuthenticationStateAsync: authenticated as sub={Sub}, claims={ClaimCount}",
                sub, claimsList.Count);

            // Set up token expiration monitoring
            SetupTokenExpirationMonitoring(token);

            return new AuthenticationState(_currentUser);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("prerender", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("JavaScript interop calls cannot be issued", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[AuthState] GetAuthenticationStateAsync: JS interop unavailable (prerender). Returning anonymous. Message: {Message}", ex.Message);
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthState] GetAuthenticationStateAsync: unexpected exception — returning anonymous");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    /// <summary>
    /// Re-checks the auth state now that JS interop is available.
    /// Returns true if user is authenticated (and notifies the auth system).
    /// </summary>
    public async Task<bool> TryRefreshAuthStateAsync()
    {
        _logger.LogDebug("[AuthState] TryRefreshAuthStateAsync called");
        var state = await GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("[AuthState] TryRefreshAuthStateAsync: user IS authenticated — notifying");
            NotifyAuthenticationStateChanged(Task.FromResult(state));
            return true;
        }
        _logger.LogWarning("[AuthState] TryRefreshAuthStateAsync: user is NOT authenticated — will redirect");
        return false;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "accessToken");
            _logger.LogDebug("[AuthState] GetTokenAsync: {Result}",
                string.IsNullOrEmpty(token) ? "null/empty" : $"OK (length={token.Length})");
            return token;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("prerender", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("JavaScript interop calls cannot be issued", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[AuthState] GetTokenAsync: JS interop unavailable (prerender)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthState] GetTokenAsync: exception reading localStorage");
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "refreshToken");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthState] GetRefreshTokenAsync: exception");
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        _logger.LogInformation("[AuthState] SetTokenAsync: storing token (length={Length})", token.Length);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "accessToken", token);

        // Set up token expiration monitoring
        SetupTokenExpirationMonitoring(token);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SetRefreshTokenAsync(string refreshToken)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "refreshToken", refreshToken);
    }

    public async Task ClearTokenAsync()
    {
        _logger.LogInformation("[AuthState] ClearTokenAsync: clearing all tokens");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "accessToken");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "refreshToken");
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _tokenExpiration = null;
        StopTokenExpirationTimer();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public void NotifyUserAuthentication(string token)
    {
        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        _currentUser = new ClaimsPrincipal(identity);

        // Set up token expiration monitoring
        SetupTokenExpirationMonitoring(token);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public void NotifyUserLogout()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _tokenExpiration = null;
        StopTokenExpirationTimer();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    /// <summary>
    /// Checks if the current token is expiring soon (within the warning threshold).
    /// </summary>
    public bool IsTokenExpiringSoon()
    {
        if (!_tokenExpiration.HasValue)
        {
            return false;
        }

        var timeUntilExpiration = _tokenExpiration.Value - DateTime.UtcNow;
        return timeUntilExpiration.TotalSeconds <= TokenExpirationWarningSeconds;
    }

    /// <summary>
    /// Checks if the current token has expired.
    /// </summary>
    public bool IsTokenExpired()
    {
        if (!_tokenExpiration.HasValue)
        {
            return true;
        }

        return _tokenExpiration.Value <= DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the token expiration time, if known.
    /// </summary>
    public DateTime? GetTokenExpiration() => _tokenExpiration;

    /// <summary>
    /// Gets the number of seconds until the token expires, or null if unknown.
    /// </summary>
    public int? GetSecondsUntilExpiration()
    {
        if (!_tokenExpiration.HasValue)
        {
            return null;
        }

        var seconds = (int)(_tokenExpiration.Value - DateTime.UtcNow).TotalSeconds;
        return Math.Max(0, seconds);
    }

    private void SetupTokenExpirationMonitoring(string token)
    {
        StopTokenExpirationTimer();

        var expiration = GetTokenExpirationFromJwt(token);
        if (!expiration.HasValue)
        {
            _logger.LogWarning("[AuthState] SetupTokenExpirationMonitoring: could not read expiration from token");
            return;
        }

        _tokenExpiration = expiration.Value;

        var timeUntilExpiration = expiration.Value - DateTime.UtcNow;
        _logger.LogDebug("[AuthState] Token expires at {Expiration} UTC ({SecondsLeft}s from now)",
            expiration.Value.ToString("HH:mm:ss"), (int)timeUntilExpiration.TotalSeconds);

        // If token is already expired, notify immediately
        if (timeUntilExpiration <= TimeSpan.Zero)
        {
            _logger.LogWarning("[AuthState] Token is ALREADY EXPIRED — firing OnSessionExpired");
            OnSessionExpired?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Calculate when to fire the warning (60 seconds before expiration)
        var warningTime = timeUntilExpiration - TimeSpan.FromSeconds(TokenExpirationWarningSeconds);

        if (warningTime <= TimeSpan.Zero)
        {
            // Token expires within the warning window, fire immediately
            OnTokenExpiring?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // Set up timer to fire warning before expiration
            _tokenExpirationTimer = new Timer(
                _ => OnTokenExpiring?.Invoke(this, EventArgs.Empty),
                null,
                warningTime,
                Timeout.InfiniteTimeSpan);
        }
    }

    private void StopTokenExpirationTimer()
    {
        _tokenExpirationTimer?.Dispose();
        _tokenExpirationTimer = null;
    }

    private static DateTime? GetTokenExpirationFromJwt(string jwt)
    {
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var token = handler.ReadJwtToken(jwt);
            var expClaim = token.Claims.FirstOrDefault(c => c.Type == "exp");

            if (expClaim != null && long.TryParse(expClaim.Value, out var expUnix))
            {
                return DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            }
        }
        catch
        {
            // Invalid token
        }

        return null;
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var token = handler.ReadJwtToken(jwt);

            foreach (var claim in token.Claims)
            {
                // Map standard JWT claims to ClaimTypes
                var type = claim.Type switch
                {
                    "sub" => ClaimTypes.NameIdentifier,
                    "email" => ClaimTypes.Email,
                    "role" => ClaimTypes.Role,
                    _ => claim.Type
                };

                claims.Add(new Claim(type, claim.Value));
            }
        }
        catch
        {
            // Invalid token
        }

        return claims;
    }

    public void Dispose()
    {
        StopTokenExpirationTimer();
        GC.SuppressFinalize(this);
    }
}
