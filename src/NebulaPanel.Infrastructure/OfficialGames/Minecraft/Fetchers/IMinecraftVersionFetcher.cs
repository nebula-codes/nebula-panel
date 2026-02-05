using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Fetchers;

/// <summary>
/// Interface for fetching Minecraft versions from a specific source.
/// </summary>
public interface IMinecraftVersionFetcher
{
    /// <summary>
    /// Unique identifier for this source (e.g., "vanilla", "paper").
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// The MinecraftLoader type this fetcher provides.
    /// </summary>
    MinecraftLoader LoaderType { get; }

    /// <summary>
    /// Fetches available versions from the source API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available versions, empty list on failure.</returns>
    Task<IReadOnlyList<GameVersionInfo>> FetchVersionsAsync(
        CancellationToken cancellationToken = default);
}
