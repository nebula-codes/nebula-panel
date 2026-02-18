using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Settings;
using NebulaPanel.Infrastructure.Configuration;

namespace NebulaPanel.Infrastructure.Services;

public class ApiKeyValidator(
    IHttpClientFactory httpClientFactory,
    IOptions<CurseForgeSettings> curseForgeSettings,
    IOptions<SteamApiSettings> steamSettings,
    IOptions<ModtaleSettings> modtaleSettings,
    ILogger<ApiKeyValidator> logger) : IApiKeyValidator
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<(bool Success, string? Message)> ValidateAsync(
        string provider, string apiKey, CancellationToken cancellationToken = default)
    {
        return provider.ToLowerInvariant() switch
        {
            "curseforge" => await ValidateCurseForgeAsync(apiKey, cancellationToken).ConfigureAwait(false),
            "steam" => await ValidateSteamAsync(apiKey, cancellationToken).ConfigureAwait(false),
            "modtale" => await ValidateModtaleAsync(apiKey, cancellationToken).ConfigureAwait(false),
            "sptforge" => await ValidateSptForgeAsync(apiKey, cancellationToken).ConfigureAwait(false),
            _ => (false, $"Unknown provider: {provider}")
        };
    }

    private async Task<(bool Success, string? Message)> ValidateCurseForgeAsync(
        string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("CurseForge");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{curseForgeSettings.Value.BaseUrl}games");
            request.Headers.Add("x-api-key", apiKey);

            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return (true, "API key is valid");

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return (false, "API key is invalid or expired");

            return (false, $"Unexpected response: {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            return (false, "Validation request timed out");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CurseForge API key validation failed");
            return (false, $"Validation failed: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? Message)> ValidateSteamAsync(
        string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("SteamApi");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            var url = $"{steamSettings.Value.WebApiBaseUrl}ISteamWebAPIUtil/GetSupportedAPIList/v1/?key={apiKey}";

            using var response = await client.GetAsync(url, cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return (true, "API key is valid");

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return (false, "API key is invalid");

            return (false, $"Unexpected response: {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            return (false, "Validation request timed out");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Steam API key validation failed");
            return (false, $"Validation failed: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? Message)> ValidateModtaleAsync(
        string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Modtale");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, modtaleSettings.Value.BaseUrl);
            request.Headers.Add("X-MODTALE-KEY", apiKey);

            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return (true, "API key is valid");

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "API key is invalid");

            return (false, $"Unexpected response: {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            return (false, "Validation request timed out");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Modtale API key validation failed");
            return (false, $"Validation failed: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? Message)> ValidateSptForgeAsync(
        string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("SptForge");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v0/mods?per_page=1");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return (true, "API key is valid");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "API key is invalid or expired");

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return (false, "API key does not have sufficient permissions");

            return (false, $"Unexpected response: {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            return (false, "Validation request timed out");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SPT Forge API key validation failed");
            return (false, $"Validation failed: {ex.Message}");
        }
    }
}
