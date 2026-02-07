namespace NebulaPanel.Web.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

public class AuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private Timer? _tokenExpirationTimer;
    private DateTime? _tokenExpiration;
    private TaskCompletionSource<AuthenticationState>? _initialAuthTcs = new();

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

    public AuthStateProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // If JS interop hasn't been confirmed ready yet, return a pending task.
        // This keeps AuthorizeRouteView in the <Authorizing> state (spinner)
        // instead of showing <NotAuthorized> (which triggers RedirectToLogin).
        if (_initialAuthTcs != null)
            return _initialAuthTcs.Task;

        return ResolveAuthStateAsync();
    }

    /// <summary>
    /// Called once JS interop is available (after first interactive render).
    /// Resolves the initial auth state and unblocks AuthorizeRouteView.
    /// </summary>
    public async Task OnCircuitReadyAsync()
    {
        if (_initialAuthTcs == null)
            return;

        var tcs = _initialAuthTcs;
        _initialAuthTcs = null;

        var state = await ResolveAuthStateAsync();
        tcs.TrySetResult(state);
    }

    private async Task<AuthenticationState> ResolveAuthStateAsync()
    {
        try
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            _currentUser = new ClaimsPrincipal(identity);

            // Set up token expiration monitoring
            SetupTokenExpirationMonitoring(token);

            return new AuthenticationState(_currentUser);
        }
        catch
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "accessToken");
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "refreshToken");
        }
        catch
        {
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
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
            return;
        }

        _tokenExpiration = expiration.Value;

        var timeUntilExpiration = expiration.Value - DateTime.UtcNow;

        // If token is already expired, notify immediately
        if (timeUntilExpiration <= TimeSpan.Zero)
        {
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
