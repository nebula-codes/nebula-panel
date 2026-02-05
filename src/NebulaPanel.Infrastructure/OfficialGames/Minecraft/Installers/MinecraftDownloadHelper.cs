using System.Security.Cryptography;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Helper methods for downloading Minecraft server files with progress reporting.
/// </summary>
public static class MinecraftDownloadHelper
{
    private const int BufferSize = 81920; // 80KB buffer
    private const int MaxRetries = 3;

    /// <summary>
    /// Downloads a file with progress reporting and retry logic.
    /// </summary>
    /// <param name="httpClient">HTTP client to use.</param>
    /// <param name="url">URL to download from.</param>
    /// <param name="destinationPath">Local path to save the file.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="progressBasePercent">Base percentage to start from.</param>
    /// <param name="progressTargetPercent">Target percentage when complete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task DownloadFileAsync(
        HttpClient httpClient,
        string url,
        string destinationPath,
        IProgress<OfficialInstallProgress>? progress = null,
        double progressBasePercent = 0,
        double progressTargetPercent = 100,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await DownloadFileInternalAsync(
                    httpClient, url, destinationPath, progress,
                    progressBasePercent, progressTargetPercent, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(attempt * 2);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex) when (attempt < MaxRetries)
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(attempt * 2);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            $"Failed to download file after {MaxRetries} attempts: {url}",
            lastException);
    }

    private static async Task DownloadFileInternalAsync(
        HttpClient httpClient,
        string url,
        string destinationPath,
        IProgress<OfficialInstallProgress>? progress,
        double progressBasePercent,
        double progressTargetPercent,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var fileStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            useAsync: true);

        var buffer = new byte[BufferSize];
        int bytesRead;
        var lastProgressReport = DateTime.UtcNow;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                .ConfigureAwait(false);

            downloadedBytes += bytesRead;

            // Report progress every 100ms to avoid flooding
            if (progress is not null && DateTime.UtcNow - lastProgressReport > TimeSpan.FromMilliseconds(100))
            {
                ReportDownloadProgress(progress, downloadedBytes, totalBytes, progressBasePercent, progressTargetPercent);
                lastProgressReport = DateTime.UtcNow;
            }
        }

        // Final progress report
        if (progress is not null)
        {
            ReportDownloadProgress(progress, downloadedBytes, totalBytes > 0 ? totalBytes : downloadedBytes, progressBasePercent, progressTargetPercent);
        }
    }

    /// <summary>
    /// Downloads a file and verifies its SHA1 checksum.
    /// </summary>
    public static async Task DownloadAndVerifySha1Async(
        HttpClient httpClient,
        string url,
        string destinationPath,
        string expectedSha1,
        IProgress<OfficialInstallProgress>? progress = null,
        double progressBasePercent = 0,
        double progressTargetPercent = 100,
        CancellationToken cancellationToken = default)
    {
        await DownloadFileAsync(
            httpClient, url, destinationPath, progress,
            progressBasePercent, progressTargetPercent, cancellationToken)
            .ConfigureAwait(false);

        var actualSha1 = await ComputeSha1Async(destinationPath, cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destinationPath);
            throw new InvalidOperationException(
                $"SHA1 checksum mismatch. Expected: {expectedSha1}, Actual: {actualSha1}");
        }
    }

    /// <summary>
    /// Downloads a file and verifies its SHA256 checksum.
    /// </summary>
    public static async Task DownloadAndVerifySha256Async(
        HttpClient httpClient,
        string url,
        string destinationPath,
        string expectedSha256,
        IProgress<OfficialInstallProgress>? progress = null,
        double progressBasePercent = 0,
        double progressTargetPercent = 100,
        CancellationToken cancellationToken = default)
    {
        await DownloadFileAsync(
            httpClient, url, destinationPath, progress,
            progressBasePercent, progressTargetPercent, cancellationToken)
            .ConfigureAwait(false);

        var actualSha256 = await ComputeSha256Async(destinationPath, cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destinationPath);
            throw new InvalidOperationException(
                $"SHA256 checksum mismatch. Expected: {expectedSha256}, Actual: {actualSha256}");
        }
    }

    /// <summary>
    /// Computes SHA1 hash of a file.
    /// </summary>
    public static async Task<string> ComputeSha1Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA256 hash of a file.
    /// </summary>
    public static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ReportDownloadProgress(
        IProgress<OfficialInstallProgress> progress,
        long downloadedBytes,
        long totalBytes,
        double basePercent,
        double targetPercent)
    {
        var downloadProgress = totalBytes > 0 ? (double)downloadedBytes / totalBytes : 0;
        var scaledProgress = basePercent + (downloadProgress * (targetPercent - basePercent));

        var downloadedMb = downloadedBytes / 1024.0 / 1024.0;
        var totalMb = totalBytes / 1024.0 / 1024.0;

        progress.Report(new OfficialInstallProgress
        {
            Phase = "Downloading",
            Message = totalBytes > 0
                ? $"Downloaded {downloadedMb:F1}MB of {totalMb:F1}MB"
                : $"Downloaded {downloadedMb:F1}MB",
            Percentage = scaledProgress,
            BytesDownloaded = downloadedBytes,
            TotalBytes = totalBytes > 0 ? totalBytes : null
        });
    }
}
