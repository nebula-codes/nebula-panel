using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.Configuration;

namespace NebulaPanel.Infrastructure.ModProviders.CurseForge;

/// <summary>
/// HTTP client wrapper for the CurseForge API with retry support.
/// </summary>
public sealed class CurseForgeApiClient
{
    private const int MaxRetries = 3;
    private const int BufferSize = 81920; // 80KB buffer for downloads

    private readonly HttpClient _httpClient;
    private readonly ILogger<CurseForgeApiClient> _logger;
    private readonly IIntegrationSettingsProvider _keyProvider;

    public CurseForgeApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<CurseForgeSettings> settings,
        IIntegrationSettingsProvider keyProvider,
        ILogger<CurseForgeApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("CurseForge");
        _keyProvider = keyProvider;
        _logger = logger;

        // Set base address from settings
        _httpClient.BaseAddress = new Uri(settings.Value.BaseUrl);

        // Debug: Log API key info (masked for security)
        var apiKey = _keyProvider.GetCurseForgeApiKey();
        var keyPreview = string.IsNullOrEmpty(apiKey)
            ? "(empty)"
            : $"{apiKey[..Math.Min(10, apiKey.Length)]}... (length: {apiKey.Length})";
        _logger.LogInformation("CurseForge API client initialized with key: {KeyPreview}", keyPreview);
    }

    /// <summary>
    /// Checks if the API key is configured.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_keyProvider.GetCurseForgeApiKey());

    /// <summary>
    /// Searches for mods on CurseForge.
    /// </summary>
    public async Task<CurseForgeSearchResponse?> SearchAsync(
        int gameId,
        string? searchFilter,
        int? classId,
        int? categoryId,
        string? gameVersion,
        int? modLoaderType,
        int sortField,
        string sortOrder,
        int index,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"gameId={gameId}",
            $"sortField={sortField}",
            $"sortOrder={Uri.EscapeDataString(sortOrder)}",
            $"index={index}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            queryParams.Add($"searchFilter={Uri.EscapeDataString(searchFilter)}");
        }

        if (classId.HasValue)
        {
            queryParams.Add($"classId={classId.Value}");
        }

        if (categoryId.HasValue)
        {
            queryParams.Add($"categoryId={categoryId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            queryParams.Add($"gameVersion={Uri.EscapeDataString(gameVersion)}");
        }

        if (modLoaderType.HasValue && modLoaderType.Value != CurseForgeModLoaderType.Any)
        {
            queryParams.Add($"modLoaderType={modLoaderType.Value}");
        }

        var url = $"mods/search?{string.Join("&", queryParams)}";
        return await GetWithRetryAsync<CurseForgeSearchResponse>(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets detailed information about a mod.
    /// </summary>
    public async Task<CurseForgeMod?> GetModAsync(
        int modId,
        CancellationToken cancellationToken = default)
    {
        var url = $"mods/{modId}";
        var response = await GetWithRetryAsync<CurseForgeModResponse>(url, cancellationToken).ConfigureAwait(false);
        return response?.Data;
    }

    /// <summary>
    /// Gets all files (versions) for a mod, optionally filtered by game version.
    /// </summary>
    public async Task<CurseForgeFile[]> GetFilesAsync(
        int modId,
        string? gameVersion = null,
        int? modLoaderType = null,
        int index = 0,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"index={index}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            queryParams.Add($"gameVersion={Uri.EscapeDataString(gameVersion)}");
        }

        if (modLoaderType.HasValue && modLoaderType.Value != CurseForgeModLoaderType.Any)
        {
            queryParams.Add($"modLoaderType={modLoaderType.Value}");
        }

        var url = $"mods/{modId}/files?{string.Join("&", queryParams)}";
        var response = await GetWithRetryAsync<CurseForgeFilesResponse>(url, cancellationToken).ConfigureAwait(false);
        return response?.Data ?? [];
    }

    /// <summary>
    /// Gets a specific file by ID.
    /// </summary>
    public async Task<CurseForgeFile?> GetFileAsync(
        int modId,
        int fileId,
        CancellationToken cancellationToken = default)
    {
        var url = $"mods/{modId}/files/{fileId}";
        var response = await GetWithRetryAsync<CurseForgeFileResponse>(url, cancellationToken).ConfigureAwait(false);
        return response?.Data;
    }

    /// <summary>
    /// Gets multiple mods by their IDs in a single batch request.
    /// </summary>
    /// <param name="modIds">The mod IDs to fetch (max 50 per request).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of mods, or empty array on failure.</returns>
    public async Task<CurseForgeMod[]> GetModsBatchAsync(
        int[] modIds,
        CancellationToken cancellationToken = default)
    {
        if (modIds.Length == 0)
        {
            return [];
        }

        var response = await PostWithRetryAsync<CurseForgeGetModsRequest, CurseForgeGetModsResponse>(
            "mods",
            new CurseForgeGetModsRequest(modIds),
            cancellationToken).ConfigureAwait(false);

        return response?.Data ?? [];
    }

    /// <summary>
    /// Gets multiple files by their IDs in a single batch request.
    /// </summary>
    /// <param name="fileIds">The file IDs to fetch (max 50 per request).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of files, or empty array on failure.</returns>
    public async Task<CurseForgeFile[]> GetFilesBatchAsync(
        int[] fileIds,
        CancellationToken cancellationToken = default)
    {
        if (fileIds.Length == 0)
        {
            return [];
        }

        var response = await PostWithRetryAsync<CurseForgeGetFilesRequest, CurseForgeGetFilesResponse>(
            "mods/files",
            new CurseForgeGetFilesRequest(fileIds),
            cancellationToken).ConfigureAwait(false);

        return response?.Data ?? [];
    }

    /// <summary>
    /// Gets the download URL for a file. Some mods have restricted downloads
    /// and don't include the URL directly in the file response.
    /// </summary>
    /// <returns>The download URL, or null if the mod has distribution restrictions.</returns>
    public async Task<string?> GetDownloadUrlAsync(
        int modId,
        int fileId,
        CancellationToken cancellationToken = default)
    {
        // This endpoint returns 403 when the mod author has disabled third-party distribution,
        // which is different from an invalid API key. Handle it separately.
        var url = $"mods/{modId}/files/{fileId}/download-url";

        if (!IsConfigured)
        {
            _logger.LogWarning("CurseForge API key is not configured");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-api-key", _keyProvider.GetCurseForgeApiKey());

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                // 403 on download-url endpoint means distribution is restricted by the mod author
                _logger.LogDebug(
                    "Mod {ModId} file {FileId} has third-party distribution disabled by the author",
                    modId, fileId);
                return null;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CurseForgeDownloadUrlResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return result?.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get download URL for mod {ModId} file {FileId}", modId, fileId);
            return null;
        }
    }

    /// <summary>
    /// Downloads a file from CurseForge with progress reporting.
    /// </summary>
    public async Task<(bool Success, string? Error)> DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // For downloads, we use a separate request without the API key
            // since CurseForge CDN URLs don't require authentication
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

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
        if (!IsConfigured)
        {
            _logger.LogWarning("CurseForge API key is not configured");
            return null;
        }

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("x-api-key", _keyProvider.GetCurseForgeApiKey());

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogError(
                        "CurseForge API returned 403 Forbidden - API key is invalid or expired. " +
                        "Please obtain a valid API key from https://console.curseforge.com/ and " +
                        "update the 'CurseForge:ApiKey' setting in appsettings.json");
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = GetRetryAfterSeconds(response);
                    _logger.LogWarning(
                        "Rate limited by CurseForge API, waiting {Seconds}s before retry (attempt {Attempt}/{MaxRetries})",
                        retryAfter, attempt, MaxRetries);

                    await Task.Delay(TimeSpan.FromSeconds(retryAfter + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogError(
                        "CurseForge API error: Status={StatusCode}, URL={Url}, Body={Body}",
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

    private async Task<TResponse?> PostWithRetryAsync<TRequest, TResponse>(
        string url,
        TRequest body,
        CancellationToken cancellationToken)
        where TRequest : class
        where TResponse : class
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("CurseForge API key is not configured");
            return null;
        }

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-api-key", _keyProvider.GetCurseForgeApiKey());
                request.Content = JsonContent.Create(body);

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogError(
                        "CurseForge API returned 403 Forbidden - API key is invalid or expired. " +
                        "Please obtain a valid API key from https://console.curseforge.com/ and " +
                        "update the 'CurseForge:ApiKey' setting in appsettings.json");
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = GetRetryAfterSeconds(response);
                    _logger.LogWarning(
                        "Rate limited by CurseForge API, waiting {Seconds}s before retry (attempt {Attempt}/{MaxRetries})",
                        retryAfter, attempt, MaxRetries);

                    await Task.Delay(TimeSpan.FromSeconds(retryAfter + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogError(
                        "CurseForge API error: Status={StatusCode}, URL={Url}, Body={Body}",
                        response.StatusCode, url, errorBody);
                    response.EnsureSuccessStatusCode();
                }

                return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
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

        _logger.LogError("Failed to POST {Url} after {MaxRetries} attempts", url, MaxRetries);
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
