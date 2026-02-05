using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Settings;

namespace NebulaPanel.Infrastructure.Steam;

/// <summary>
/// Service for interacting with the Steam Web API.
/// </summary>
public class SteamApiService : ISteamApiService
{
    private readonly HttpClient _httpClient;
    private readonly SteamApiSettings _settings;
    private readonly ILogger<SteamApiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public SteamApiService(
        IHttpClientFactory httpClientFactory,
        IOptions<SteamApiSettings> settings,
        ILogger<SteamApiService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("SteamApi");
        _settings = settings.Value;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    /// <inheritdoc />
    public async Task<SteamAppDetails?> GetAppDetailsAsync(string appId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_settings.StoreApiBaseUrl}appdetails?appids={appId}";
            _logger.LogDebug("Fetching Steam app details for {AppId}", appId);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(appId, out var appElement))
            {
                _logger.LogWarning("Steam app {AppId} not found in response", appId);
                return null;
            }

            if (!appElement.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            {
                _logger.LogWarning("Steam app {AppId} lookup failed", appId);
                return null;
            }

            if (!appElement.TryGetProperty("data", out var dataElement))
            {
                return null;
            }

            return ParseAppDetails(appId, dataElement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Steam app details for {AppId}", appId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SteamAppSearchResult>> SearchAppsAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the Steam search suggest API
            var url = $"https://store.steampowered.com/search/suggest?term={Uri.EscapeDataString(query)}&f=games&cc=US&realm=1&l=english&v=22461604&excluded_content_descriptors[]=3&excluded_content_descriptors[]=4";

            _logger.LogDebug("Searching Steam for '{Query}'", query);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseSearchResults(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Steam apps for '{Query}'", query);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<SteamWorkshopSearchResult> SearchWorkshopItemsAsync(
        string appId,
        string? query = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.HasApiKey)
        {
            _logger.LogWarning("Steam API key not configured, cannot search Workshop");
            return new SteamWorkshopSearchResult { Page = page, PageSize = pageSize };
        }

        try
        {
            var url = $"{_settings.WebApiBaseUrl}IPublishedFileService/QueryFiles/v1/?key={_settings.ApiKey}" +
                      $"&appid={appId}" +
                      $"&numperpage={pageSize}" +
                      $"&page={page}" +
                      "&return_metadata=true" +
                      "&return_vote_data=true" +
                      "&return_tags=true";

            if (!string.IsNullOrWhiteSpace(query))
            {
                url += $"&search_text={Uri.EscapeDataString(query)}";
            }

            _logger.LogDebug("Searching Steam Workshop for app {AppId}, query: '{Query}'", appId, query);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("response", out var responseElement))
            {
                return new SteamWorkshopSearchResult { Page = page, PageSize = pageSize };
            }

            var items = new List<SteamWorkshopItem>();
            var totalCount = responseElement.TryGetProperty("total", out var totalProp) ? totalProp.GetInt32() : 0;

            if (responseElement.TryGetProperty("publishedfiledetails", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in filesElement.EnumerateArray())
                {
                    var item = ParseWorkshopItem(file);
                    if (item is not null)
                    {
                        items.Add(item);
                    }
                }
            }

            return new SteamWorkshopSearchResult
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Steam Workshop for app {AppId}", appId);
            return new SteamWorkshopSearchResult { Page = page, PageSize = pageSize };
        }
    }

    /// <inheritdoc />
    public async Task<SteamWorkshopItem?> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default)
    {
        if (!_settings.HasApiKey)
        {
            _logger.LogWarning("Steam API key not configured, cannot get Workshop item");
            return null;
        }

        try
        {
            var url = $"{_settings.WebApiBaseUrl}IPublishedFileService/GetDetails/v1/?key={_settings.ApiKey}" +
                      $"&publishedfileids[0]={workshopItemId}" +
                      "&includetags=true" +
                      "&includeadditionalpreviews=true" +
                      "&includevotes=true";

            _logger.LogDebug("Fetching Steam Workshop item {ItemId}", workshopItemId);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("response", out var responseElement))
            {
                return null;
            }

            if (!responseElement.TryGetProperty("publishedfiledetails", out var filesElement) ||
                filesElement.ValueKind != JsonValueKind.Array ||
                filesElement.GetArrayLength() == 0)
            {
                return null;
            }

            return ParseWorkshopItem(filesElement[0]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Steam Workshop item {ItemId}", workshopItemId);
            return null;
        }
    }

    private SteamAppDetails ParseAppDetails(string appId, JsonElement data)
    {
        var name = data.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
        var shortDesc = data.TryGetProperty("short_description", out var descProp) ? descProp.GetString() : null;
        var headerImage = data.TryGetProperty("header_image", out var headerProp) ? headerProp.GetString() : null;
        var type = data.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
        var isFree = data.TryGetProperty("is_free", out var freeProp) && freeProp.GetBoolean();

        var developers = new List<string>();
        if (data.TryGetProperty("developers", out var devsProp) && devsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var dev in devsProp.EnumerateArray())
            {
                var devName = dev.GetString();
                if (!string.IsNullOrEmpty(devName))
                {
                    developers.Add(devName);
                }
            }
        }

        var publishers = new List<string>();
        if (data.TryGetProperty("publishers", out var pubsProp) && pubsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var pub in pubsProp.EnumerateArray())
            {
                var pubName = pub.GetString();
                if (!string.IsNullOrEmpty(pubName))
                {
                    publishers.Add(pubName);
                }
            }
        }

        var categories = new List<string>();
        if (data.TryGetProperty("categories", out var catsProp) && catsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var cat in catsProp.EnumerateArray())
            {
                if (cat.TryGetProperty("description", out var catDescProp))
                {
                    var catDesc = catDescProp.GetString();
                    if (!string.IsNullOrEmpty(catDesc))
                    {
                        categories.Add(catDesc);
                    }
                }
            }
        }

        var genres = new List<string>();
        if (data.TryGetProperty("genres", out var genresProp) && genresProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var genre in genresProp.EnumerateArray())
            {
                if (genre.TryGetProperty("description", out var genreDescProp))
                {
                    var genreDesc = genreDescProp.GetString();
                    if (!string.IsNullOrEmpty(genreDesc))
                    {
                        genres.Add(genreDesc);
                    }
                }
            }
        }

        var supportsDedicatedServers = categories.Any(c => c.Contains("Dedicated Server", StringComparison.OrdinalIgnoreCase));

        DateTime? releaseDate = null;
        if (data.TryGetProperty("release_date", out var releaseProp) &&
            releaseProp.TryGetProperty("date", out var dateProp))
        {
            var dateStr = dateProp.GetString();
            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsed))
            {
                releaseDate = parsed;
            }
        }

        // Try to get icon URL from Steam CDN
        var iconUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/capsule_231x87.jpg";

        return new SteamAppDetails
        {
            AppId = appId,
            Name = name,
            ShortDescription = shortDesc,
            HeaderImageUrl = headerImage,
            IconUrl = iconUrl,
            Type = type,
            IsFree = isFree,
            Developers = developers,
            Publishers = publishers,
            Categories = categories,
            Genres = genres,
            SupportsDedicatedServers = supportsDedicatedServers,
            ReleaseDate = releaseDate
        };
    }

    private IReadOnlyList<SteamAppSearchResult> ParseSearchResults(string html)
    {
        var results = new List<SteamAppSearchResult>();

        // Parse the Steam suggest HTML response
        // Format: <a class="match" data-ds-appid="...">...<div class="match_name">Name</div>...</a>
        var appIdPattern = @"data-ds-appid=""(\d+)""";
        var namePattern = @"<div class=""match_name"">([^<]+)</div>";

        var appIdMatches = System.Text.RegularExpressions.Regex.Matches(html, appIdPattern);
        var nameMatches = System.Text.RegularExpressions.Regex.Matches(html, namePattern);

        for (int i = 0; i < Math.Min(appIdMatches.Count, nameMatches.Count); i++)
        {
            var appId = appIdMatches[i].Groups[1].Value;
            var name = System.Net.WebUtility.HtmlDecode(nameMatches[i].Groups[1].Value);

            results.Add(new SteamAppSearchResult
            {
                AppId = appId,
                Name = name,
                IconUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/capsule_231x87.jpg"
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<SteamWorkshopCollection?> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        if (!_settings.HasApiKey)
        {
            _logger.LogWarning("Steam API key not configured, cannot get Workshop collection");
            return null;
        }

        try
        {
            // First, get the collection details with children IDs
            var url = $"{_settings.WebApiBaseUrl}IPublishedFileService/GetDetails/v1/?key={_settings.ApiKey}" +
                      $"&publishedfileids[0]={collectionId}" +
                      "&includechildren=true" +
                      "&includetags=true";

            _logger.LogDebug("Fetching Steam Workshop collection {CollectionId}", collectionId);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("response", out var responseElement))
            {
                return null;
            }

            if (!responseElement.TryGetProperty("publishedfiledetails", out var filesElement) ||
                filesElement.ValueKind != JsonValueKind.Array ||
                filesElement.GetArrayLength() == 0)
            {
                return null;
            }

            var collectionFile = filesElement[0];

            // Check if this is actually a collection (file_type = 2)
            var fileType = collectionFile.TryGetProperty("file_type", out var fileTypeProp) ? fileTypeProp.GetInt32() : 0;
            if (fileType != 2)
            {
                _logger.LogWarning("Workshop item {CollectionId} is not a collection (file_type={FileType})", collectionId, fileType);
                return null;
            }

            // Extract children IDs
            var childrenIds = new List<string>();
            if (collectionFile.TryGetProperty("children", out var childrenElement) && childrenElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in childrenElement.EnumerateArray())
                {
                    if (child.TryGetProperty("publishedfileid", out var childIdProp))
                    {
                        var childId = childIdProp.GetString() ?? childIdProp.GetInt64().ToString();
                        childrenIds.Add(childId);
                    }
                }
            }

            // Now fetch all children items
            var childItems = await GetWorkshopItemsAsync(childrenIds, cancellationToken).ConfigureAwait(false);

            // Parse collection metadata
            var id = collectionFile.TryGetProperty("publishedfileid", out var idProp) ? idProp.GetString() ?? idProp.GetInt64().ToString() : collectionId;
            var title = collectionFile.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
            var description = collectionFile.TryGetProperty("file_description", out var descProp) ? descProp.GetString() : null;
            var previewUrl = collectionFile.TryGetProperty("preview_url", out var previewProp) ? previewProp.GetString() : null;
            var authorSteamId = collectionFile.TryGetProperty("creator", out var creatorProp) ? creatorProp.GetString() ?? "" : "";

            var createdAt = collectionFile.TryGetProperty("time_created", out var createdProp)
                ? DateTimeOffset.FromUnixTimeSeconds(createdProp.GetInt64()).UtcDateTime
                : DateTime.UtcNow;
            var updatedAt = collectionFile.TryGetProperty("time_updated", out var updatedProp)
                ? DateTimeOffset.FromUnixTimeSeconds(updatedProp.GetInt64()).UtcDateTime
                : DateTime.UtcNow;

            return new SteamWorkshopCollection
            {
                Id = id,
                Title = title,
                Description = description,
                PreviewUrl = previewUrl,
                AuthorSteamId = authorSteamId,
                Items = childItems,
                ItemCount = childItems.Count,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Steam Workshop collection {CollectionId}", collectionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SteamWorkshopItem>> GetWorkshopItemsAsync(
        IEnumerable<string> workshopItemIds,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.HasApiKey)
        {
            _logger.LogWarning("Steam API key not configured, cannot get Workshop items");
            return [];
        }

        var idList = workshopItemIds.ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        try
        {
            // Build the URL with multiple IDs
            var url = $"{_settings.WebApiBaseUrl}IPublishedFileService/GetDetails/v1/?key={_settings.ApiKey}" +
                      "&includetags=true" +
                      "&includeadditionalpreviews=true" +
                      "&includevotes=true";

            for (int i = 0; i < idList.Count; i++)
            {
                url += $"&publishedfileids[{i}]={idList[i]}";
            }

            _logger.LogDebug("Fetching {Count} Steam Workshop items", idList.Count);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("response", out var responseElement))
            {
                return [];
            }

            if (!responseElement.TryGetProperty("publishedfiledetails", out var filesElement) ||
                filesElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var items = new List<SteamWorkshopItem>();
            foreach (var file in filesElement.EnumerateArray())
            {
                var item = ParseWorkshopItem(file);
                if (item is not null)
                {
                    items.Add(item);
                }
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Steam Workshop items");
            return [];
        }
    }

    private SteamWorkshopItem? ParseWorkshopItem(JsonElement file)
    {
        if (!file.TryGetProperty("publishedfileid", out var idProp))
        {
            return null;
        }

        var id = idProp.GetString() ?? idProp.GetInt64().ToString();
        var appId = file.TryGetProperty("consumer_appid", out var appIdProp) ? appIdProp.GetInt32().ToString() : "";
        var title = file.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
        var description = file.TryGetProperty("short_description", out var descProp)
            ? descProp.GetString()
            : (file.TryGetProperty("file_description", out var fdProp) ? fdProp.GetString() : null);
        var previewUrl = file.TryGetProperty("preview_url", out var previewProp) ? previewProp.GetString() : null;
        var authorSteamId = file.TryGetProperty("creator", out var creatorProp) ? creatorProp.GetString() ?? "" : "";
        var fileSize = file.TryGetProperty("file_size", out var sizeProp) ? ParseLong(sizeProp) : 0;
        var subscriptions = file.TryGetProperty("subscriptions", out var subsProp) ? ParseInt(subsProp) : 0;
        var favorites = file.TryGetProperty("favorited", out var favProp) ? ParseInt(favProp) : 0;
        var views = file.TryGetProperty("views", out var viewsProp) ? ParseInt(viewsProp) : 0;

        var createdAt = file.TryGetProperty("time_created", out var createdProp)
            ? DateTimeOffset.FromUnixTimeSeconds(createdProp.GetInt64()).UtcDateTime
            : DateTime.UtcNow;
        var updatedAt = file.TryGetProperty("time_updated", out var updatedProp)
            ? DateTimeOffset.FromUnixTimeSeconds(updatedProp.GetInt64()).UtcDateTime
            : DateTime.UtcNow;

        var tags = new List<string>();
        if (file.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsProp.EnumerateArray())
            {
                if (tag.TryGetProperty("tag", out var tagNameProp) ||
                    tag.TryGetProperty("display_name", out tagNameProp))
                {
                    var tagName = tagNameProp.GetString();
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        tags.Add(tagName);
                    }
                }
            }
        }

        return new SteamWorkshopItem
        {
            Id = id,
            AppId = appId,
            Title = title,
            Description = description,
            PreviewUrl = previewUrl,
            AuthorSteamId = authorSteamId,
            FileSizeBytes = fileSize,
            Subscriptions = subscriptions,
            Favorites = favorites,
            Views = views,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Tags = tags
        };
    }

    /// <summary>
    /// Parses a JSON element as long, handling both string and number types.
    /// </summary>
    private static long ParseLong(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetInt64();
        }
        if (element.ValueKind == JsonValueKind.String)
        {
            return long.TryParse(element.GetString(), out var result) ? result : 0;
        }
        return 0;
    }

    /// <summary>
    /// Parses a JSON element as int, handling both string and number types.
    /// </summary>
    private static int ParseInt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetInt32();
        }
        if (element.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(element.GetString(), out var result) ? result : 0;
        }
        return 0;
    }
}
