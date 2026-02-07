using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Infrastructure.Configuration;

namespace NebulaPanel.Infrastructure.Health;

/// <summary>
/// Health check for external mod provider APIs (CurseForge and Modrinth).
/// Results are cached for 5 minutes to avoid excessive API calls.
/// CurseForge is only checked if an API key is configured.
/// </summary>
public class ExternalApiHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExternalApiHealthCheck> _logger;
    private readonly CurseForgeSettings _curseForgeSettings;

    private const string CacheKey = "ExternalApiHealthCheck_Result";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public ExternalApiHealthCheck(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<ExternalApiHealthCheck> logger,
        IOptions<CurseForgeSettings> curseForgeSettings)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
        _curseForgeSettings = curseForgeSettings.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache.TryGetValue(CacheKey, out HealthCheckResult cachedResult))
        {
            return cachedResult;
        }

        var data = new Dictionary<string, object>();
        var failedApis = new List<string>();

        // Check Modrinth (public API, no config gate — failures are informational only)
        var modrinthStatus = await CheckModrinthAsync(cancellationToken).ConfigureAwait(false);
        data["modrinth"] = modrinthStatus.IsHealthy ? "healthy" : "unavailable";
        data["modrinth_message"] = modrinthStatus.Message;

        // Check CurseForge only if API key is configured
        if (!string.IsNullOrEmpty(_curseForgeSettings.ApiKey))
        {
            var curseForgeStatus = await CheckCurseForgeAsync(cancellationToken).ConfigureAwait(false);
            data["curseforge"] = curseForgeStatus.IsHealthy ? "healthy" : "unhealthy";
            data["curseforge_message"] = curseForgeStatus.Message;
            if (!curseForgeStatus.IsHealthy)
            {
                failedApis.Add("CurseForge");
            }
        }
        else
        {
            data["curseforge"] = "not_configured";
            data["curseforge_message"] = "API key not configured";
        }

        HealthCheckResult result;
        if (failedApis.Count == 0)
        {
            result = HealthCheckResult.Healthy("All external APIs are reachable", data);
        }
        else
        {
            // External APIs being unavailable is degraded, not unhealthy
            // The application can still function without mod downloading
            result = HealthCheckResult.Degraded(
                $"Some external APIs are unavailable: {string.Join(", ", failedApis)}",
                data: data);
        }

        // Cache the result
        _cache.Set(CacheKey, result, CacheDuration);

        return result;
    }

    private async Task<(bool IsHealthy, string Message)> CheckModrinthAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Modrinth");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(RequestTimeout);

            // Simple health check - just verify we can reach the API
            using var response = await client.GetAsync("", cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return (true, "API is reachable");
            }

            return (false, $"API returned status {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            return (false, "Request timed out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Modrinth health check failed");
            return (false, ex.Message);
        }
    }

    private async Task<(bool IsHealthy, string Message)> CheckCurseForgeAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CurseForge");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(RequestTimeout);

            // CurseForge requires the API key header
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.curseforge.com/v1/games");
            request.Headers.Add("x-api-key", _curseForgeSettings.ApiKey);

            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return (true, "API is reachable");
            }

            return (false, $"API returned status {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            return (false, "Request timed out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CurseForge health check failed");
            return (false, ex.Message);
        }
    }
}
