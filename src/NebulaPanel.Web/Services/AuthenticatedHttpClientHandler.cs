namespace NebulaPanel.Web.Services;

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

/// <summary>
/// HTTP client handler that automatically adds authentication headers
/// and handles 401 responses by refreshing the token and retrying.
/// </summary>
public class AuthenticatedHttpClientHandler : DelegatingHandler
{
    private readonly IAuthClientService _authService;
    private readonly AuthStateProvider _authStateProvider;
    private readonly ILogger<AuthenticatedHttpClientHandler> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthenticatedHttpClientHandler(
        IAuthClientService authService,
        AuthStateProvider authStateProvider,
        ILogger<AuthenticatedHttpClientHandler> logger)
    {
        _authService = authService;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Skip auth endpoints to avoid infinite loops
        if (IsAuthEndpoint(request.RequestUri))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Add authorization header if we have a token
        await AddAuthorizationHeaderAsync(request);

        var response = await base.SendAsync(request, cancellationToken);

        // If we get a 401, try to refresh the token and retry once
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogDebug("Received 401, attempting token refresh");

            var refreshResult = await RefreshTokenAsync(cancellationToken);
            if (refreshResult)
            {
                // Clone the request and retry
                var retryRequest = await CloneRequestAsync(request);
                await AddAuthorizationHeaderAsync(retryRequest);

                response.Dispose();
                response = await base.SendAsync(retryRequest, cancellationToken);
            }
        }

        return response;
    }

    private async Task AddAuthorizationHeaderAsync(HttpRequestMessage request)
    {
        var token = await _authStateProvider.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            // Check if token was already refreshed by another request
            if (!_authStateProvider.IsTokenExpiringSoon() && !_authStateProvider.IsTokenExpired())
            {
                return true;
            }

            var result = await _authService.RefreshTokenAsync();
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static bool IsAuthEndpoint(Uri? uri)
    {
        if (uri == null) return false;

        var path = uri.PathAndQuery.ToLowerInvariant();
        return path.Contains("/api/auth/login") ||
               path.Contains("/api/auth/refresh");
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        // Copy content if present
        if (request.Content != null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            // Copy content headers
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Copy request headers (except Authorization which we'll set fresh)
        foreach (var header in request.Headers)
        {
            if (!header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Copy properties
        foreach (var property in request.Options)
        {
            clone.Options.TryAdd(property.Key, property.Value);
        }

        return clone;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshLock.Dispose();
        }

        base.Dispose(disposing);
    }
}
