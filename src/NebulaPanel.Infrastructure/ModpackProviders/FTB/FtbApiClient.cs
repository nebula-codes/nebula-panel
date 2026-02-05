using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NebulaPanel.Infrastructure.ModpackProviders.FTB;

/// <summary>
/// HTTP client wrapper for the Feed The Beast (FTB) API with retry support.
/// Base URL: https://api.modpacks.ch/public
/// No authentication required.
/// </summary>
public sealed class FtbApiClient
{
    private const int MaxRetries = 3;
    private const int BufferSize = 81920; // 80KB buffer for downloads

    private readonly HttpClient _httpClient;
    private readonly ILogger<FtbApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FtbApiClient(IHttpClientFactory httpClientFactory, ILogger<FtbApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("FTB");
        _logger = logger;
    }

    /// <summary>
    /// Searches for modpacks by term.
    /// GET /modpack/search/{limit}?term={query}
    /// </summary>
    public async Task<FtbSearchResponse?> SearchAsync(
        string query,
        int limit = 50,
        CancellationToken ct = default)
    {
        var url = $"modpack/search/{limit}";
        if (!string.IsNullOrWhiteSpace(query))
        {
            url += $"?term={Uri.EscapeDataString(query)}";
        }

        return await GetWithRetryAsync<FtbSearchResponse>(url, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets popular/featured modpacks.
    /// GET /modpack/popular/{limit}
    /// </summary>
    public async Task<FtbSearchResponse?> GetPopularAsync(
        int limit = 20,
        CancellationToken ct = default)
    {
        var url = $"modpack/popular/{limit}";
        return await GetWithRetryAsync<FtbSearchResponse>(url, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets modpack details by ID.
    /// GET /modpack/{packId}
    /// </summary>
    public async Task<FtbPackDetails?> GetModpackAsync(
        int packId,
        CancellationToken ct = default)
    {
        var url = $"modpack/{packId}";
        return await GetWithRetryAsync<FtbPackDetails>(url, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets version details for a modpack.
    /// GET /modpack/{packId}/{versionId}
    /// </summary>
    public async Task<FtbVersionDetails?> GetVersionAsync(
        int packId,
        int versionId,
        CancellationToken ct = default)
    {
        var url = $"modpack/{packId}/{versionId}";
        return await GetWithRetryAsync<FtbVersionDetails>(url, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets server files/installer for a version.
    /// GET /modpack/{packId}/{versionId}/server/{platform}
    /// Platform: "linux" or "windows"
    /// </summary>
    public async Task<FtbServerFilesResponse?> GetServerFilesAsync(
        int packId,
        int versionId,
        string platform,
        CancellationToken ct = default)
    {
        var url = $"modpack/{packId}/{versionId}/server/{platform}";
        return await GetWithRetryAsync<FtbServerFilesResponse>(url, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file to a stream with optional progress reporting.
    /// </summary>
    public async Task DownloadFileAsync(
        string url,
        Stream destination,
        IProgress<long>? progress = null,
        CancellationToken ct = default)
    {
        // Create a new HttpClient for external downloads (not the base FTB API)
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0 (game-server-panel)");
        client.Timeout = TimeSpan.FromMinutes(30); // Long timeout for large downloads

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        var buffer = new byte[BufferSize];
        long totalBytesRead = 0;
        int bytesRead;
        var lastProgressReport = DateTime.UtcNow;

        while ((bytesRead = await contentStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
            totalBytesRead += bytesRead;

            // Report progress at most every 100ms
            if (progress != null && (DateTime.UtcNow - lastProgressReport).TotalMilliseconds >= 100)
            {
                progress.Report(totalBytesRead);
                lastProgressReport = DateTime.UtcNow;
            }
        }

        // Final progress report
        progress?.Report(totalBytesRead);
    }

    /// <summary>
    /// Downloads a file to a destination path.
    /// </summary>
    public async Task<bool> DownloadFileToPathAsync(
        string url,
        string destinationPath,
        IProgress<long>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var fileStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            await DownloadFileAsync(url, fileStream, progress, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from {Url} to {Path}", url, destinationPath);
            return false;
        }
    }

    private async Task<T?> GetWithRetryAsync<T>(string url, CancellationToken ct) where T : class
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogDebug("FTB API returned 404 for {Url}", url);
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = GetRetryAfterSeconds(response);
                    _logger.LogWarning(
                        "Rate limited by FTB API, waiting {Seconds}s before retry (attempt {Attempt}/{MaxRetries})",
                        retryAfter, attempt, MaxRetries);

                    await Task.Delay(TimeSpan.FromSeconds(retryAfter), ct).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s
                _logger.LogWarning(
                    ex,
                    "HTTP error on FTB API attempt {Attempt}/{MaxRetries}, retrying in {Delay}s",
                    attempt, MaxRetries, delay.TotalSeconds);

                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FTB API response from {Url}", url);
                return null;
            }
        }

        _logger.LogError("Failed to get FTB API {Url} after {MaxRetries} attempts", url, MaxRetries);
        return null;
    }

    private static int GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta != null)
        {
            return (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
        }

        // Default to 10 seconds if header not present
        return 10;
    }
}
