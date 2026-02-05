using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames;

/// <summary>
/// Registry for official game providers. Follows same pattern as ServerExecutorFactory.
/// Supports runtime registration and metadata tracking for admin UI.
/// </summary>
public class OfficialGameRegistry : IOfficialGameRegistry
{
    private readonly ConcurrentDictionary<string, IOfficialGameProvider> _providers;
    private readonly ConcurrentDictionary<string, ProviderMetadataEntry> _metadata;
    private readonly ILogger<OfficialGameRegistry> _logger;

    private record ProviderMetadataEntry(
        int? VersionCount,
        DateTime? LastVersionCheck,
        bool IsUserDefined);

    public OfficialGameRegistry(
        IEnumerable<IOfficialGameProvider> providers,
        ILogger<OfficialGameRegistry> logger)
    {
        _logger = logger;
        _metadata = new ConcurrentDictionary<string, ProviderMetadataEntry>(StringComparer.OrdinalIgnoreCase);

        // Index providers by slug (case-insensitive)
        _providers = new ConcurrentDictionary<string, IOfficialGameProvider>(
            providers.ToDictionary(
                p => p.GameSlug.ToLowerInvariant(),
                p => p,
                StringComparer.OrdinalIgnoreCase));

        // Initialize metadata for DI-registered providers
        foreach (var provider in providers)
        {
            var slug = provider.GameSlug.ToLowerInvariant();
            _metadata[slug] = new ProviderMetadataEntry(null, null, false);
        }

        _logger.LogInformation("Initialized with {Count} providers from DI", _providers.Count);
    }

    public IOfficialGameProvider? GetProvider(string gameSlug)
    {
        _providers.TryGetValue(gameSlug.ToLowerInvariant(), out var provider);
        return provider;
    }

    public IEnumerable<IOfficialGameProvider> GetAllProviders() => _providers.Values;

    public bool IsOfficialGame(string gameSlug) =>
        _providers.ContainsKey(gameSlug.ToLowerInvariant());

    public void RegisterProvider(IOfficialGameProvider provider)
    {
        var slug = provider.GameSlug.ToLowerInvariant();

        if (_providers.TryAdd(slug, provider))
        {
            _metadata[slug] = new ProviderMetadataEntry(null, null, true);
            _logger.LogInformation("Registered new provider: {GameSlug}", slug);
        }
        else
        {
            // Update existing provider
            _providers[slug] = provider;
            _logger.LogInformation("Updated existing provider: {GameSlug}", slug);
        }
    }

    public bool UnregisterProvider(string gameSlug)
    {
        var slug = gameSlug.ToLowerInvariant();
        var removed = _providers.TryRemove(slug, out _);

        if (removed)
        {
            _metadata.TryRemove(slug, out _);
            _logger.LogInformation("Unregistered provider: {GameSlug}", slug);
        }

        return removed;
    }

    public OfficialGameMetadata? GetProviderMetadata(string gameSlug)
    {
        var slug = gameSlug.ToLowerInvariant();

        if (!_providers.TryGetValue(slug, out var provider))
        {
            return null;
        }

        var definition = provider.GetGameDefinition();
        var entry = _metadata.GetValueOrDefault(slug) ?? new ProviderMetadataEntry(null, null, false);

        return new OfficialGameMetadata
        {
            GameSlug = slug,
            Name = definition.Name,
            ProviderType = provider is IJsonGameDefinitionProvider ? "Json" : "Code",
            IsEnabled = true, // Default to enabled; actual status from database
            CachedVersionCount = entry.VersionCount,
            LastVersionCheck = entry.LastVersionCheck,
            IconPath = definition.IconPath,
            IsUserDefined = entry.IsUserDefined,
            SchemaVersion = definition.ConfigurationSchemas.Count > 0 ? "1.0" : null
        };
    }

    public IEnumerable<OfficialGameMetadata> GetAllProviderMetadata()
    {
        foreach (var kvp in _providers)
        {
            var metadata = GetProviderMetadata(kvp.Key);
            if (metadata is not null)
            {
                yield return metadata;
            }
        }
    }

    public void UpdateProviderMetadata(string gameSlug, int versionCount, DateTime lastCheck)
    {
        var slug = gameSlug.ToLowerInvariant();

        if (_metadata.TryGetValue(slug, out var existing))
        {
            _metadata[slug] = existing with
            {
                VersionCount = versionCount,
                LastVersionCheck = lastCheck
            };
        }
        else
        {
            _metadata[slug] = new ProviderMetadataEntry(versionCount, lastCheck, false);
        }

        _logger.LogDebug("Updated metadata for {GameSlug}: {VersionCount} versions", slug, versionCount);
    }

    public async Task<int> RefreshVersionsAsync(string gameSlug, CancellationToken cancellationToken = default)
    {
        var slug = gameSlug.ToLowerInvariant();

        if (!_providers.TryGetValue(slug, out var provider))
        {
            _logger.LogWarning("Cannot refresh versions: provider not found for {GameSlug}", slug);
            return 0;
        }

        try
        {
            _logger.LogInformation("Refreshing versions for {GameSlug}", slug);
            var versions = await provider.GetAvailableVersionsAsync(cancellationToken).ConfigureAwait(false);
            var count = versions.Count;

            UpdateProviderMetadata(slug, count, DateTime.UtcNow);
            _logger.LogInformation("Refreshed {Count} versions for {GameSlug}", count, slug);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh versions for {GameSlug}", slug);
            return 0;
        }
    }
}

/// <summary>
/// Marker interface for JSON-based game providers.
/// </summary>
public interface IJsonGameDefinitionProvider : IOfficialGameProvider
{
    /// <summary>
    /// Path to the JSON definition file (embedded resource or filesystem).
    /// </summary>
    string DefinitionFilePath { get; }

    /// <summary>
    /// Whether this provider was loaded from a user-provided file.
    /// </summary>
    bool IsUserDefined { get; }
}
