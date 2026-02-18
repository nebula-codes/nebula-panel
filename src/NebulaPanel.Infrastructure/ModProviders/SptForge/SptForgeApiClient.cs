using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.ModProviders.SptForge;

/// <summary>
/// HTTP client wrapper for the SPT Forge API (forge.sp-tarkov.com).
/// </summary>
public sealed class SptForgeApiClient
{
    private const int MaxRetries = 3;
    private const int BufferSize = 81920;

    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IIntegrationSettingsProvider _keyProvider;
    private readonly ILogger<SptForgeApiClient> _logger;

    public SptForgeApiClient(
        IHttpClientFactory httpClientFactory,
        IIntegrationSettingsProvider keyProvider,
        ILogger<SptForgeApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("SptForge");
        _httpClientFactory = httpClientFactory;
        _keyProvider = keyProvider;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_keyProvider.GetSptForgeApiKey());

    /// <summary>
    /// Searches for mods with optional filters.
    /// </summary>
    public async Task<SptForgePaginatedResponse<SptForgeMod>?> SearchModsAsync(
        string? query = null,
        string? sptVersion = null,
        int? categoryId = null,
        int page = 1,
        int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"per_page={Math.Min(perPage, 50)}",
            "include=category"
        };

        if (!string.IsNullOrWhiteSpace(query))
            queryParams.Add($"query={Uri.EscapeDataString(query)}");

        if (!string.IsNullOrWhiteSpace(sptVersion))
            queryParams.Add($"filter[spt_version]={Uri.EscapeDataString(sptVersion)}");

        if (categoryId.HasValue)
            queryParams.Add($"filter[category_id]={categoryId.Value}");

        var url = $"api/v0/mods?{string.Join("&", queryParams)}";
        return await GetWithRetryAsync<SptForgePaginatedResponse<SptForgeMod>>(url, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets detailed information about a specific mod.
    /// </summary>
    public async Task<SptForgeResponse<SptForgeMod>?> GetModAsync(
        string modId,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v0/mod/{Uri.EscapeDataString(modId)}?include=versions,category,source_code_links";
        return await GetWithRetryAsync<SptForgeResponse<SptForgeMod>>(url, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all version history for a mod, fetching all pages.
    /// </summary>
    public async Task<SptForgePaginatedResponse<SptForgeModVersion>?> GetModVersionsAsync(
        string modId,
        CancellationToken cancellationToken = default)
    {
        var escapedId = Uri.EscapeDataString(modId);
        var firstPage = await GetWithRetryAsync<SptForgePaginatedResponse<SptForgeModVersion>>(
            $"api/v0/mod/{escapedId}/versions?per_page=50", cancellationToken).ConfigureAwait(false);

        if (firstPage?.Data is null || firstPage.Meta is null || firstPage.Meta.LastPage <= 1)
            return firstPage;

        // Fetch remaining pages
        var allVersions = new List<SptForgeModVersion>(firstPage.Data);
        for (var page = 2; page <= firstPage.Meta.LastPage; page++)
        {
            var nextPage = await GetWithRetryAsync<SptForgePaginatedResponse<SptForgeModVersion>>(
                $"api/v0/mod/{escapedId}/versions?per_page=50&page={page}", cancellationToken).ConfigureAwait(false);

            if (nextPage?.Data is not null)
                allVersions.AddRange(nextPage.Data);
        }

        return new SptForgePaginatedResponse<SptForgeModVersion>
        {
            Success = firstPage.Success,
            Data = allVersions.ToArray(),
            Meta = firstPage.Meta
        };
    }

    /// <summary>
    /// Gets available SPT versions.
    /// </summary>
    public async Task<SptForgeResponse<SptForgeVersionInfo[]>?> GetSptVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetWithRetryAsync<SptForgeResponse<SptForgeVersionInfo[]>>(
            "api/v0/spt-versions", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets mod categories.
    /// </summary>
    public async Task<SptForgeResponse<SptForgeCategory[]>?> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetWithRetryAsync<SptForgeResponse<SptForgeCategory[]>>(
            "api/v0/mod-categories", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a mod file with progress reporting.
    /// Handles Google Drive links by converting to direct download URLs.
    /// </summary>
    public async Task<(bool Success, string? Error)> DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Google Drive folder links cannot be downloaded programmatically
            if (IsGoogleDriveFolderUrl(url))
            {
                _logger.LogWarning("Download link is a Google Drive folder, cannot download: {Url}", url);
                return (false, "This mod's download link points to a Google Drive folder, which cannot be downloaded automatically. The mod author needs to provide a direct file link.");
            }

            var downloadUrl = ConvertToDirectDownloadUrl(url);
            _logger.LogDebug("Downloading from {Url} (original: {OriginalUrl})", downloadUrl, url);

            // Use a plain HttpClient for external downloads (Google Drive, CDNs, etc.)
            var client = _httpClientFactory.CreateClient();

            using var response = await client.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            // Google Drive large file virus scan confirmation
            if (IsGoogleDriveUrl(url) && IsHtmlResponse(response))
            {
                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var confirmUrl = ExtractGoogleDriveConfirmUrl(html, downloadUrl);

                if (confirmUrl is not null)
                {
                    _logger.LogDebug("Following Google Drive confirmation redirect");
                    return await DownloadToFileAsync(client, confirmUrl, destinationPath, progress, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            response.EnsureSuccessStatusCode();

            // Check if we accidentally downloaded an HTML page
            if (IsHtmlResponse(response))
            {
                return (false, "Download returned an HTML page instead of a file. The download link may be invalid.");
            }

            await SaveResponseToFileAsync(response, destinationPath, progress, cancellationToken)
                .ConfigureAwait(false);

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

    private async Task<(bool Success, string? Error)> DownloadToFileAsync(
        HttpClient client,
        string url,
        string destinationPath,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        if (IsHtmlResponse(response))
            return (false, "Download returned an HTML page instead of a file. The download link may be invalid.");

        await SaveResponseToFileAsync(response, destinationPath, progress, cancellationToken)
            .ConfigureAwait(false);

        return (true, null);
    }

    private async Task SaveResponseToFileAsync(
        HttpResponseMessage response,
        string destinationPath,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var totalBytes = response.Content.Headers.ContentLength ?? 0;

        await using var contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
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

        while ((read = await contentStream.ReadAsync(buffer, cancellationToken)
            .ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
            bytesRead += read;

            if (progress != null && (DateTime.UtcNow - lastProgressReport).TotalMilliseconds >= 100)
            {
                var percentage = totalBytes > 0 ? (double)bytesRead / totalBytes * 100 : 0;
                progress.Report(new DownloadProgress(bytesRead, totalBytes, percentage));
                lastProgressReport = DateTime.UtcNow;
            }
        }

        if (progress != null && totalBytes > 0)
        {
            progress.Report(new DownloadProgress(bytesRead, totalBytes, 100));
        }
    }

    private static string ConvertToDirectDownloadUrl(string url)
    {
        if (!IsGoogleDriveUrl(url))
            return url;

        // Extract file ID from various Google Drive URL formats
        // https://drive.google.com/file/d/{fileId}/view
        // https://drive.google.com/open?id={fileId}
        // https://drive.google.com/uc?id={fileId}
        string? fileId = null;

        var fileMatch = System.Text.RegularExpressions.Regex.Match(url, @"/file/d/([a-zA-Z0-9_-]+)");
        if (fileMatch.Success)
        {
            fileId = fileMatch.Groups[1].Value;
        }
        else
        {
            var idMatch = System.Text.RegularExpressions.Regex.Match(url, @"[?&]id=([a-zA-Z0-9_-]+)");
            if (idMatch.Success)
                fileId = idMatch.Groups[1].Value;
        }

        if (fileId is not null)
            return $"https://drive.google.com/uc?export=download&id={fileId}&confirm=t";

        return url;
    }

    private static bool IsGoogleDriveUrl(string url) =>
        url.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsGoogleDriveFolderUrl(string url) =>
        url.Contains("drive.google.com/drive/folders/", StringComparison.OrdinalIgnoreCase);

    private static bool IsHtmlResponse(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true;

    private static string? ExtractGoogleDriveConfirmUrl(string html, string originalUrl)
    {
        // Look for the confirmation form action URL
        var formMatch = System.Text.RegularExpressions.Regex.Match(html, @"action=""([^""]+)""");
        if (formMatch.Success)
        {
            var action = formMatch.Groups[1].Value
                .Replace("&amp;", "&");
            if (action.Contains("drive.google.com") || action.Contains("drive.usercontent.google.com"))
                return action;
        }

        // Try adding confirm=t parameter if not already present
        if (!originalUrl.Contains("confirm="))
            return originalUrl + (originalUrl.Contains('?') ? "&" : "?") + "confirm=t";

        return null;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        var apiKey = _keyProvider.GetSptForgeApiKey();
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    private async Task<T?> GetWithRetryAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var request = CreateRequest(HttpMethod.Get, url);
                using var response = await _httpClient.SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning(
                        "SPT Forge API returned 401 Unauthorized for {Url}. " +
                        "An API token may be required — configure it in Settings > Integrations.",
                        url);
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("SPT Forge API returned 403 Forbidden for {Url}", url);
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60;
                    _logger.LogWarning(
                        "Rate limited by SPT Forge API, waiting {Seconds}s (attempt {Attempt}/{MaxRetries})",
                        retryAfter, attempt, MaxRetries);

                    await Task.Delay(TimeSpan.FromSeconds(retryAfter + 1), cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(ex,
                    "HTTP error on attempt {Attempt}/{MaxRetries}, retrying in {Delay}s",
                    attempt, MaxRetries, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogError("Failed to get {Url} after {MaxRetries} attempts", url, MaxRetries);
        return null;
    }
}
