using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Registry for managing and accessing modpack providers.
/// </summary>
public interface IModpackProviderRegistry
{
    /// <summary>
    /// Gets all registered modpack providers.
    /// </summary>
    /// <returns>Read-only list of all available providers.</returns>
    IReadOnlyList<IModpackProvider> GetAllProviders();

    /// <summary>
    /// Gets a specific modpack provider by type.
    /// </summary>
    /// <param name="type">The type of provider to retrieve.</param>
    /// <returns>The provider instance, or null if not registered.</returns>
    IModpackProvider? GetProvider(ModpackProviderType type);

    /// <summary>
    /// Gets all enabled modpack providers.
    /// </summary>
    /// <returns>Read-only list of enabled providers.</returns>
    IReadOnlyList<IModpackProvider> GetEnabledProviders();

    /// <summary>
    /// Checks if a specific provider type is registered and available.
    /// </summary>
    /// <param name="type">The provider type to check.</param>
    /// <returns>True if the provider is registered.</returns>
    bool IsProviderAvailable(ModpackProviderType type);
}
