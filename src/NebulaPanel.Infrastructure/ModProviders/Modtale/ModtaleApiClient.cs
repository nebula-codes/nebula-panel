using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.Configuration;

namespace NebulaPanel.Infrastructure.ModProviders.Modtale;

/// <summary>
/// HTTP client wrapper for the Modtale API with retry support.
/// </summary>
public sealed class ModtaleApiClient
{
    private const int MaxRetries = 3;
    private const int BufferSize = 81920; // 80KB buffer for downloads

    private readonly HttpClient _httpClient;
    private readonly ILogger<ModtaleApiClient> _logger;
    private readonly IIntegrationSettingsProvider _keyProvider;

    public ModtaleApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<ModtaleSettings> settings,
        IIntegrationSettingsProvider keyProvider,
        ILogger<ModtaleApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Modtale");
        _keyProvider = keyProvider;
        _logger = logger;

        // Set base address from settings
        _httpClient.BaseAddress = new Uri(settings.Value.BaseUrl);
    }

    /// <summary>
    /// Checks if an API key is configured (optional for Modtale).
    /// </summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(_keyProvider.GetModtaleApiKey());

    /// <summary>
    /// Searches for projects on Modtale.
    /// </summary>
    public async Task<ModtaleSearchResponse?> SearchAsync(
        string? searchQuery,
        string? classification,
        string? gameVersion,
        string? tags,
        string sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"size={Math.Min(pageSize, 100)}",
            $"sort={Uri.EscapeDataString(sort)}"
        };

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            queryParams.Add($"search={Uri.EscapeDataString(searchQuery)}");
        }

        if (!string.IsNullOrWhiteSpace(classification))
        {
            queryParams.Add($"classification={Uri.EscapeDataString(classification)}");
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            queryParams.Add($"gameVersion={Uri.EscapeDataString(gameVersion)}");
        }

        if (!string.IsNullOrWhiteSpace(tags))
        {
            queryParams.Add($"tags={Uri.EscapeDataString(tags)}");
        }

        var url = $"projects?{string.Join("&", queryParams)}";
        return await GetWithRetryAsync<ModtaleSearchResponse>(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets detailed information about a project.
    /// </summary>
    public async Task<ModtaleProjectDetails?> GetProjectAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var url = $"projects/{Uri.EscapeDataString(projectId)}";
        return await GetWithRetryAsync<ModtaleProjectDetails>(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets available tags for filtering.
    /// </summary>
    public async Task<string[]> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetWithRetryAsync<ModtaleTagsResponse>("tags", cancellationToken).ConfigureAwait(false);
        return response?.Tags ?? [];
    }

    /// <summary>
    /// Gets available game versions.
    /// </summary>
    public async Task<string[]> GetGameVersionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetWithRetryAsync<ModtaleGameVersionsResponse>("meta/game-versions", cancellationToken).ConfigureAwait(false);
        return response?.GameVersions ?? [];
    }

    /// <summary>
    /// Downloads a file from Modtale with progress reporting.
    /// </summary>
    public async Task<(bool Success, string? Error)> DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Add API key if configured
            if (HasApiKey)
            {
                request.Headers.Add("X-MODTALE-KEY", _keyProvider.GetModtaleApiKey());
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            var buffer = new byte[BufferSize];
            long bytesRead = 0;
            int read;
            var lastProgressReport = DateTime.UtcNow;

            while ((read = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                bytesRead += read;

                // Report progress at most every 100ms to avoid flooding
                if (progress != null && (DateTime.UtcNow - lastProgressReport).TotalMilliseconds >= 100)
                {
                    var percentage = totalBytes > 0 ? (double)bytesRead / totalBytes * 100 : 0;
                    progress.Report(new DownloadProgress(bytesRead, totalBytes, percentage));
                    lastProgressReport = DateTime.UtcNow;
                }
            }

            // Final progress report
            if (progress != null && totalBytes > 0)
            {
                progress.Report(new DownloadProgress(bytesRead, totalBytes, 100));
            }

            return (true, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to download file from {Url}", url);
            return (false, $"Download failed: {ex.Message}");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error downloading file to {Path}", destinationPath);
            return (false, $"IO error: {ex.Message}");
        }
    }

    private async Task<T?> GetWithRetryAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Add API key if configured (optional for Modtale)
                if (HasApiKey)
                {
                    request.Headers.Add("X-MODTALE-KEY", _keyProvider.GetModtaleApiKey());
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = GetRetryAfterSeconds(response);
                    _logger.LogWarning(
                        "Rate limited by Modtale API, waiting {Seconds}s before retry (attempt {Attempt}/{MaxRetries})",
                        retryAfter, attempt, MaxRetries);

                    await Task.Delay(TimeSpan.FromSeconds(retryAfter + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogError(
                        "Modtale API error: Status={StatusCode}, URL={Url}, Body={Body}",
                        response.StatusCode, url, errorBody);
                    response.EnsureSuccessStatusCode();
                }

                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s
                _logger.LogWarning(
                    ex,
                    "HTTP error on attempt {Attempt}/{MaxRetries}, retrying in {Delay}s",
                    attempt, MaxRetries, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize response from {Url}", url);
                return null;
            }
        }

        _logger.LogError("Failed to get {Url} after {MaxRetries} attempts", url, MaxRetries);
        return null;
    }

    private static int GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            if (int.TryParse(values.FirstOrDefault(), out var seconds))
            {
                return seconds;
            }
        }

        // Default to 60 seconds if header not present
        return 60;
    }
}
