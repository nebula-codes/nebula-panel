namespace NebulaPanel.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for the Modtale API.
/// </summary>
public class ModtaleSettings
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Modtale";

    /// <summary>
    /// Optional Modtale API key. Not required for basic usage but may increase rate limits.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The base URL for the Modtale API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.modtale.net/api/v1/";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
