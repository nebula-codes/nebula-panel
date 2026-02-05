namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for handling image downloads and storage.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Downloads an image from a URL and stores it locally.
    /// Returns the local path to the stored image.
    /// If the path is already local, returns it unchanged.
    /// </summary>
    /// <param name="imagePathOrUrl">URL or local path to the image.</param>
    /// <param name="category">Category for organizing (e.g., "games", "avatars").</param>
    /// <param name="fileName">Optional file name. If not provided, generates from URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Local path to the image, or null if download failed.</returns>
    Task<string?> EnsureLocalAsync(
        string imagePathOrUrl,
        string category,
        string? fileName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a path is a remote URL.
    /// </summary>
    bool IsRemoteUrl(string path);
}
