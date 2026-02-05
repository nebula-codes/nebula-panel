using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Infrastructure.Services;

public class ImageService(
    IHttpClientFactory httpClientFactory,
    ILogger<ImageService> logger) : IImageService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<ImageService> _logger = logger;

    private static readonly HashSet<string> AllowedExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".ico"];
    private const string ImagesDirectory = "wwwroot/images";

    public bool IsRemoteUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> EnsureLocalAsync(
        string imagePathOrUrl,
        string category,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePathOrUrl))
            return null;

        if (!IsRemoteUrl(imagePathOrUrl))
        {
            // Already a local path
            return imagePathOrUrl;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("ImageDownload");
            client.Timeout = TimeSpan.FromSeconds(30);

            using var response = await client.GetAsync(imagePathOrUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Determine file extension
            var extension = GetExtensionFromUrl(imagePathOrUrl);
            if (string.IsNullOrEmpty(extension))
            {
                extension = GetExtensionFromContentType(response.Content.Headers.ContentType?.MediaType);
            }

            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension.ToLowerInvariant()))
            {
                _logger.LogWarning("Unsupported image format for URL: {Url}", imagePathOrUrl);
                return null;
            }

            // Generate file name if not provided
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"{Guid.NewGuid():N}{extension}";
            }
            else if (!Path.HasExtension(fileName))
            {
                fileName = $"{fileName}{extension}";
            }

            // Create category directory
            var categoryPath = Path.Combine(ImagesDirectory, category);
            Directory.CreateDirectory(categoryPath);

            var localPath = Path.Combine(categoryPath, fileName);

            // Download and save
            await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

            // Return web-accessible path (relative to wwwroot)
            var webPath = $"/images/{category}/{fileName}";
            _logger.LogInformation("Downloaded image from {Url} to {LocalPath}", imagePathOrUrl, webPath);

            return webPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download image from {Url}", imagePathOrUrl);
            return null;
        }
    }

    private static string? GetExtensionFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var extension = Path.GetExtension(path);

            if (!string.IsNullOrEmpty(extension) && AllowedExtensions.Contains(extension.ToLowerInvariant()))
            {
                return extension;
            }
        }
        catch
        {
            // Ignore URL parsing errors
        }

        return null;
    }

    private static string? GetExtensionFromContentType(string? contentType)
    {
        return contentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            "image/x-icon" => ".ico",
            "image/vnd.microsoft.icon" => ".ico",
            _ => null
        };
    }
}
