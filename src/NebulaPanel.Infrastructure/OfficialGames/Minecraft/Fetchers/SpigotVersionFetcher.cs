using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Fetchers;

/// <summary>
/// Provides known Spigot versions. Spigot requires BuildTools compilation,
/// so this fetcher returns a static list of commonly used versions.
/// </summary>
public class SpigotVersionFetcher(
    ILogger<SpigotVersionFetcher> logger) : IMinecraftVersionFetcher
{
    private readonly ILogger<SpigotVersionFetcher> _logger = logger;

    public string SourceName => "spigot";
    public MinecraftLoader LoaderType => MinecraftLoader.Spigot;

    // Known Spigot-supported versions (BuildTools required)
    // Updated periodically - latest versions at the top
    private static readonly string[] KnownVersions =
    [
        "1.21.4",
        "1.21.3",
        "1.21.2",
        "1.21.1",
        "1.21",
        "1.20.6",
        "1.20.4",
        "1.20.2",
        "1.20.1",
        "1.20",
        "1.19.4",
        "1.19.3",
        "1.19.2",
        "1.19.1",
        "1.19",
        "1.18.2",
        "1.18.1",
        "1.18",
        "1.17.1",
        "1.17",
        "1.16.5",
        "1.16.4",
        "1.16.3",
        "1.16.2",
        "1.16.1"
    ];

    public Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Returning known Spigot versions (BuildTools required)");

        var versions = KnownVersions
            .Select((v, index) => new GameVersionInfo
            {
                Version = $"spigot-{v}",
                DisplayName = $"Spigot {v} (BuildTools required)",
                VersionType = GameVersionType.Release,
                LoaderType = MinecraftLoader.Spigot,
                MinecraftVersion = v,
                IsRecommended = index == 0 // First version is recommended
            })
            .ToList();

        _logger.LogDebug("Returned {Count} Spigot versions", versions.Count);
        return Task.FromResult<IReadOnlyList<GameVersionInfo>>(versions);
    }
}
