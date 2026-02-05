using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.ModpackProviders;

/// <summary>
/// Registry for managing and accessing modpack providers.
/// </summary>
public sealed class ModpackProviderRegistry : IModpackProviderRegistry
{
    private readonly Dictionary<ModpackProviderType, IModpackProvider> _providers;
    private readonly ILogger<ModpackProviderRegistry> _logger;

    public ModpackProviderRegistry(
        IEnumerable<IModpackProvider> providers,
        ILogger<ModpackProviderRegistry> logger)
    {
        _logger = logger;
        _providers = providers.ToDictionary(p => p.ProviderType);

        _logger.LogInformation(
            "Modpack provider registry initialized with {Count} providers: {Providers}",
            _providers.Count,
            string.Join(", ", _providers.Keys));
    }

    /// <inheritdoc />
    public IReadOnlyList<IModpackProvider> GetAllProviders()
    {
        return _providers.Values.ToList();
    }

    /// <inheritdoc />
    public IModpackProvider? GetProvider(ModpackProviderType type)
    {
        return _providers.GetValueOrDefault(type);
    }

    /// <inheritdoc />
    public IReadOnlyList<IModpackProvider> GetEnabledProviders()
    {
        // For now, all registered providers are considered enabled
        // In the future, this could check a configuration setting
        return _providers.Values.ToList();
    }

    /// <inheritdoc />
    public bool IsProviderAvailable(ModpackProviderType type)
    {
        return _providers.ContainsKey(type);
    }
}
