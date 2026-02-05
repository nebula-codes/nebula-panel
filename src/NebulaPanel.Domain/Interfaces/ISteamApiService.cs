namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Service for interacting with the Steam Web API to fetch game metadata.
/// </summary>
public interface ISteamApiService
{
    /// <summary>
    /// Looks up a Steam app by its App ID.
    /// </summary>
    /// <param name="appId">The Steam App ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>App info if found, null otherwise.</returns>
    Task<SteamAppDetails?> GetAppDetailsAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for Steam apps by name.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching apps.</returns>
    Task<IReadOnlyList<SteamAppSearchResult>> SearchAppsAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of published workshop items for an app.
    /// Requires a Steam API key.
    /// </summary>
    /// <param name="appId">The Steam App ID.</param>
    /// <param name="query">Optional search query.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Workshop items search result.</returns>
    Task<SteamWorkshopSearchResult> SearchWorkshopItemsAsync(
        string appId,
        string? query = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets details about a specific workshop item.
    /// </summary>
    /// <param name="workshopItemId">The Workshop item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Workshop item details if found.</returns>
    Task<SteamWorkshopItem?> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets details about a Steam Workshop collection including all its items.
    /// </summary>
    /// <param name="collectionId">The Workshop collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection details with items if found.</returns>
    Task<SteamWorkshopCollection?> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets details about multiple workshop items in a single request.
    /// </summary>
    /// <param name="workshopItemIds">The Workshop item IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Workshop items that were found.</returns>
    Task<IReadOnlyList<SteamWorkshopItem>> GetWorkshopItemsAsync(
        IEnumerable<string> workshopItemIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Detailed information about a Steam app.
/// </summary>
public record SteamAppDetails
{
    public string AppId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
    public string? HeaderImageUrl { get; init; }
    public string? IconUrl { get; init; }
    public string? Type { get; init; }
    public bool IsFree { get; init; }
    public IReadOnlyList<string> Developers { get; init; } = [];
    public IReadOnlyList<string> Publishers { get; init; } = [];
    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<string> Genres { get; init; } = [];
    public bool SupportsDedicatedServers { get; init; }
    public string? DedicatedServerAppId { get; init; }
    public DateTime? ReleaseDate { get; init; }
}

/// <summary>
/// Result from searching Steam apps.
/// </summary>
public record SteamAppSearchResult
{
    public string AppId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
}

/// <summary>
/// Result from searching Steam Workshop.
/// </summary>
public record SteamWorkshopSearchResult
{
    public IReadOnlyList<SteamWorkshopItem> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>
/// A Steam Workshop item.
/// </summary>
public record SteamWorkshopItem
{
    public string Id { get; init; } = string.Empty;
    public string AppId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? PreviewUrl { get; init; }
    public string AuthorSteamId { get; init; } = string.Empty;
    public string? AuthorName { get; init; }
    public long FileSizeBytes { get; init; }
    public int Subscriptions { get; init; }
    public int Favorites { get; init; }
    public int Views { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>
/// A Steam Workshop collection containing multiple items.
/// </summary>
public record SteamWorkshopCollection
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? PreviewUrl { get; init; }
    public string AuthorSteamId { get; init; } = string.Empty;
    public string? AuthorName { get; init; }
    public IReadOnlyList<SteamWorkshopItem> Items { get; init; } = [];
    public int ItemCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
