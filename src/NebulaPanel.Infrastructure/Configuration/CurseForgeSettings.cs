namespace NebulaPanel.Infrastructure.Configuration;

/// <summary>
/// Configuration for a specific game on CurseForge.
/// </summary>
public class CurseForgeGameConfig
{
    /// <summary>
    /// The CurseForge game ID. Use 0 if the game is not yet available on CurseForge.
    /// </summary>
    public int GameId { get; set; }

    /// <summary>
    /// Whether this game is enabled for CurseForge searches.
    /// Set to false for games not yet available on CurseForge.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// CurseForge class IDs for different content types (Mod, Modpack, ResourcePack, etc.).
    /// Keys should match ModContentType enum names.
    /// </summary>
    public Dictionary<string, int> ClassIds { get; set; } = new();
}

/// <summary>
/// Configuration settings for the CurseForge API.
/// </summary>
public class CurseForgeSettings
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "CurseForge";

    /// <summary>
    /// The CurseForge API key. Required for all API requests.
    /// Obtain from https://console.curseforge.com/
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The base URL for the CurseForge API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.curseforge.com/v1/";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Game-specific configuration. Keys are game slugs (e.g., "minecraft", "hytale").
    /// If empty or null, falls back to hardcoded defaults for backward compatibility.
    /// </summary>
    public Dictionary<string, CurseForgeGameConfig>? Games { get; set; }

    // Default game IDs for backward compatibility
    private static readonly Dictionary<string, int> DefaultGameIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["minecraft"] = 432,
        ["minecraft-java"] = 432,
        ["ark-survival-ascended"] = 83374,
        ["wow"] = 1,
        ["sims4"] = 78062
    };

    // Default Minecraft class IDs for backward compatibility
    private static readonly Dictionary<string, int> DefaultMinecraftClassIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mod"] = 6,
        ["Modpack"] = 4471,
        ["ResourcePack"] = 12,
        ["Shader"] = 6552,
        ["World"] = 17
    };

    // Default ASA class IDs
    private static readonly Dictionary<string, int> DefaultAsaClassIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mod"] = 6945
    };

    /// <summary>
    /// Gets the game configuration for a given slug, falling back to defaults if not configured.
    /// </summary>
    /// <param name="gameSlug">The game's unique slug identifier.</param>
    /// <returns>The game configuration, or null if the game is not supported.</returns>
    public CurseForgeGameConfig? GetGameConfig(string gameSlug)
    {
        // First check configured games (case-insensitive)
        if (Games != null)
        {
            foreach (var kvp in Games)
            {
                if (kvp.Key.Equals(gameSlug, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }

        // Fall back to defaults for backward compatibility
        if (DefaultGameIds.TryGetValue(gameSlug, out var gameId))
        {
            var isMinecraft = gameSlug.Equals("minecraft", StringComparison.OrdinalIgnoreCase)
                || gameSlug.Equals("minecraft-java", StringComparison.OrdinalIgnoreCase);
            var isAsa = gameSlug.Equals("ark-survival-ascended", StringComparison.OrdinalIgnoreCase);

            var classIds = isMinecraft
                ? new Dictionary<string, int>(DefaultMinecraftClassIds, StringComparer.OrdinalIgnoreCase)
                : isAsa
                    ? new Dictionary<string, int>(DefaultAsaClassIds, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            return new CurseForgeGameConfig
            {
                GameId = gameId,
                Enabled = true,
                ClassIds = classIds
            };
        }

        return null;
    }

    /// <summary>
    /// Checks if a game is supported (exists in config or defaults and is enabled with valid GameId).
    /// </summary>
    /// <param name="gameSlug">The game's unique slug identifier.</param>
    /// <returns>True if the game is supported on CurseForge.</returns>
    public bool IsGameSupported(string gameSlug)
    {
        var config = GetGameConfig(gameSlug);
        return config != null && config.Enabled && config.GameId > 0;
    }
}
