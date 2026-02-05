namespace NebulaPanel.Domain.Settings;

/// <summary>
/// Configuration settings for Steam Web API integration.
/// </summary>
public class SteamApiSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "SteamApi";

    /// <summary>
    /// Steam Web API key. Required for Workshop API access.
    /// Get yours at: https://steamcommunity.com/dev/apikey
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the Steam Store API.
    /// </summary>
    public string StoreApiBaseUrl { get; set; } = "https://store.steampowered.com/api/";

    /// <summary>
    /// Base URL for the Steam Web API (requires API key).
    /// </summary>
    public string WebApiBaseUrl { get; set; } = "https://api.steampowered.com/";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether the API key is configured.
    /// </summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
