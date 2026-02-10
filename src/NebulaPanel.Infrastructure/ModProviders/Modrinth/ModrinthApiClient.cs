using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.ModProviders.Modrinth;

/// <summary>
/// HTTP client wrapper for the Modrinth API with rate limiting and retry support.
/// </summary>
public sealed class ModrinthApiClient
{
    private const int MaxRetries = 3;
    private const int RateLimitBuffer = 10;
    private const int BufferSize = 81920; // 80KB buffer for downloads

    private readonly HttpClient _httpClient;
    private readonly ILogger<ModrinthApiClient> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);

    private volatile int _rateLimitRemaining = 300;
    private long _rateLimitResetTicks = DateTime.UtcNow.Ticks;

    public ModrinthApiClient(IHttpClientFactory httpClientFactory, ILogger<ModrinthApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Modrinth");
        _logger = logger;
    }

    /// <summary>
    /// Searches for projects on Modrinth.
    /// </summary>
    public async Task<ModrinthSearchResponse?> SearchAsync(
        string? query,
        IReadOnlyList<string[]>? facets,
        string index,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"index={Uri.EscapeDataString(index)}",
            $"offset={offset}",
            $"limit={limit}"
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            queryParams.Add($"query={Uri.EscapeDataString(query)}");
        }

        if (facets is { Count: > 0 })
        {
            var facetJson = JsonSerializer.Serialize(facets);
            queryParams.Add($"facets={Uri.EscapeDataString(facetJson)}");
        }

        var url = $"search?{string.Join("&", queryParams)}";
        return await GetWithRetryAsync<ModrinthSearchResponse>(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets detailed information about a project.
    /// </summary>
    public async Task<ModrinthProject?> GetProjectAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default)
    {
        var url = $"project/{Uri.EscapeDataString(idOrSlug)}";
        return await GetWithRetryAsync<ModrinthProject>(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all versions for a project, optionally filtered by loaders and game versions.
    /// </summary>
    public async Task<ModrinthVersion[]> GetProjectVersionsAsync(
        string projectId,
        IReadOnlyList<string>? loaders = null,
        IReadOnlyList<string>? gameVersions = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();

        if (loaders is { Count: > 0 })
        {
            var loadersJson = JsonSerializer.Serialize(loaders);
            queryParams.Add($"loaders={Uri.EscapeDataString(loadersJson)}");
        }

        if (gameVersions is { Count: > 0 })
        {
            var versionsJson = JsonSerializer.Serialize(gameVersions);
            queryParams.Add($"game_versions={Uri.EscapeDataString(versionsJson)}");
        }

        var url = $"project/{Uri.EscapeDataString(projectId)}/version";
        if (queryParams.Count > 0)
        {
            url += $"?{string.Join("&", queryParams)}";
        }

        return await GetWithRetryAsync<ModrinthVersion[]>(url, cancellationToken).ConfigureAwait(false) ?? [];
    }

    /// <summary>
    /// Gets a specific version by ID.
    /// </summary>
    public async Task<ModrinthVersion?> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        var url = $"version/{Uri.EscapeDataString(versionId)}";
        return await GetWithRetryAsync<ModrinthVersion>(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets team members for a project (to fetch author information).
    /// </summary>
    public async Task<ModrinthTeamMember[]> GetProjectTeamAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var url = $"project/{Uri.EscapeDataString(projectId)}/members";
        return await GetWithRetryAsync<ModrinthTeamMember[]>(url, cancellationToken).ConfigureAwait(false) ?? [];
    }

    /// <summary>
    /// Downloads a file from Modrinth CDN with progress reporting.
    /// </summary>
    public async Task<(bool Success, string? Error)> DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                url,
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
                await WaitForRateLimitAsync(cancellationToken).ConfigureAwait(false);

                using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                UpdateRateLimitInfo(response);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = GetRetryAfterSeconds(response);
                    _logger.LogWarning(
                        "Rate limited by Modrinth API, waiting {Seconds}s before retry (attempt {Attempt}/{MaxRetries})",
                        retryAfter, attempt, MaxRetries);

                    await Task.Delay(TimeSpan.FromSeconds(retryAfter + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

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

    private async Task WaitForRateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_rateLimitRemaining < RateLimitBuffer)
            {
                var waitTime = new DateTime(Interlocked.Read(ref _rateLimitResetTicks), DateTimeKind.Utc) - DateTime.UtcNow;
                if (waitTime > TimeSpan.Zero)
                {
                    _logger.LogDebug(
                        "Rate limit buffer reached ({Remaining} remaining), waiting {Seconds}s",
                        _rateLimitRemaining, waitTime.TotalSeconds);

                    await Task.Delay(waitTime + TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private void UpdateRateLimitInfo(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-Ratelimit-Remaining", out var remainingValues))
        {
            if (int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
            {
                Volatile.Write(ref _rateLimitRemaining, remaining);
            }
        }

        if (response.Headers.TryGetValues("X-Ratelimit-Reset", out var resetValues))
        {
            if (int.TryParse(resetValues.FirstOrDefault(), out var resetSeconds))
            {
                Interlocked.Exchange(ref _rateLimitResetTicks, DateTime.UtcNow.AddSeconds(resetSeconds).Ticks);
            }
        }
    }

    private static int GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-Ratelimit-Reset", out var values))
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
