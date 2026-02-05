using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Registry for official game providers. Similar pattern to IServerExecutorFactory.
/// Supports runtime registration and metadata tracking for admin UI.
/// </summary>
public interface IOfficialGameRegistry
{
    /// <summary>
    /// Gets a provider by game slug.
    /// </summary>
    IOfficialGameProvider? GetProvider(string gameSlug);

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    IEnumerable<IOfficialGameProvider> GetAllProviders();

    /// <summary>
    /// Checks if a game slug has an official provider.
    /// </summary>
    bool IsOfficialGame(string gameSlug);

    /// <summary>
    /// Registers a new provider at runtime (e.g., JSON-based providers).
    /// </summary>
    /// <param name="provider">The provider to register</param>
    void RegisterProvider(IOfficialGameProvider provider);

    /// <summary>
    /// Unregisters a provider by game slug.
    /// </summary>
    /// <param name="gameSlug">The game slug to unregister</param>
    /// <returns>True if provider was found and removed</returns>
    bool UnregisterProvider(string gameSlug);

    /// <summary>
    /// Gets metadata for a specific provider including version count and last check time.
    /// </summary>
    /// <param name="gameSlug">The game slug</param>
    /// <returns>Provider metadata or null if not found</returns>
    OfficialGameMetadata? GetProviderMetadata(string gameSlug);

    /// <summary>
    /// Gets metadata for all registered providers.
    /// </summary>
    IEnumerable<OfficialGameMetadata> GetAllProviderMetadata();

    /// <summary>
    /// Updates cached metadata for a provider (version count, last check time).
    /// </summary>
    /// <param name="gameSlug">The game slug</param>
    /// <param name="versionCount">Number of available versions</param>
    /// <param name="lastCheck">When versions were last fetched</param>
    void UpdateProviderMetadata(string gameSlug, int versionCount, DateTime lastCheck);

    /// <summary>
    /// Forces a version refresh for a specific provider.
    /// </summary>
    /// <param name="gameSlug">The game slug</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of versions fetched</returns>
    Task<int> RefreshVersionsAsync(string gameSlug, CancellationToken cancellationToken = default);
}
