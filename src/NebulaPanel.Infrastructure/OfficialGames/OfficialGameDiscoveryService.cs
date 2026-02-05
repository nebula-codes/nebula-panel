using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.OfficialGames;

/// <summary>
/// Hosted service that discovers official game providers on startup.
/// - Finds all IOfficialGameProvider implementations registered via DI
/// - Discovers JSON-based game definitions from embedded resources and filesystem
/// - Validates providers and their configuration schemas
/// - Seeds/updates official games in the database
/// </summary>
public class OfficialGameDiscoveryService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OfficialGameDiscoveryService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Assembly _resourceAssembly;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public OfficialGameDiscoveryService(
        IServiceProvider serviceProvider,
        ILogger<OfficialGameDiscoveryService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _resourceAssembly = typeof(OfficialGameDiscoveryService).Assembly;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Discovering official game providers...");

        using var scope = _serviceProvider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IOfficialGameRegistry>();
        var schemaLoader = scope.ServiceProvider.GetRequiredService<IConfigurationSchemaLoader>();

        // 1. Code-based providers are already registered via DI
        var codeProviders = registry.GetAllProviders()
            .Where(p => p is not IJsonGameDefinitionProvider)
            .ToList();
        _logger.LogInformation("Found {Count} code-based providers", codeProviders.Count);

        // 2. Discover JSON-based providers from embedded resources
        var jsonProviders = await DiscoverJsonProvidersAsync(scope.ServiceProvider, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Found {Count} JSON-based providers", jsonProviders.Count);

        // 3. Register JSON providers with the registry
        foreach (var provider in jsonProviders)
        {
            registry.RegisterProvider(provider);
        }

        // 4. Validate all providers
        var allProviders = registry.GetAllProviders().ToList();
        foreach (var provider in allProviders)
        {
            await ValidateProviderAsync(provider, schemaLoader, cancellationToken).ConfigureAwait(false);
        }

        // 5. Seed/update database
        await OfficialGameSeeder.SeedOfficialGamesAsync(_serviceProvider).ConfigureAwait(false);

        _logger.LogInformation("Official game discovery complete. Total providers: {Count}", allProviders.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<List<IJsonGameDefinitionProvider>> DiscoverJsonProvidersAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var providers = new List<IJsonGameDefinitionProvider>();

        // Scan embedded resources for game.json files
        var resourceNames = _resourceAssembly.GetManifestResourceNames()
            .Where(n => n.EndsWith("game.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var resourceName in resourceNames)
        {
            try
            {
                using var stream = _resourceAssembly.GetManifestResourceStream(resourceName);
                if (stream is null) continue;

                var definition = await JsonSerializer.DeserializeAsync<JsonGameDefinition>(
                    stream, JsonOptions, cancellationToken).ConfigureAwait(false);

                if (definition is not null)
                {
                    var provider = CreateJsonProvider(services, definition, resourceName, isUserDefined: false);
                    providers.Add(provider);
                    _logger.LogDebug("Discovered embedded JSON game: {Slug}", definition.Slug);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load JSON game definition from resource: {ResourceName}", resourceName);
            }
        }

        // Scan filesystem for user-defined games
        var userGamesPath = _configuration["OfficialGames:UserGamesPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "games");

        if (Directory.Exists(userGamesPath))
        {
            foreach (var file in Directory.GetFiles(userGamesPath, "game.json", SearchOption.AllDirectories))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                    var definition = JsonSerializer.Deserialize<JsonGameDefinition>(json, JsonOptions);

                    if (definition is not null)
                    {
                        var provider = CreateJsonProvider(services, definition, file, isUserDefined: true);
                        providers.Add(provider);
                        _logger.LogDebug("Discovered user JSON game: {Slug} from {Path}", definition.Slug, file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load JSON game definition from file: {Path}", file);
                }
            }
        }

        return providers;
    }

    private JsonGameProvider CreateJsonProvider(
        IServiceProvider services,
        JsonGameDefinition definition,
        string definitionPath,
        bool isUserDefined)
    {
        var schemaLoader = services.GetRequiredService<IConfigurationSchemaLoader>();
        var steamCmd = services.GetService<ISteamCmdService>();
        var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();

        return new JsonGameProvider(
            definition,
            definitionPath,
            isUserDefined,
            schemaLoader,
            steamCmd,
            httpClientFactory,
            loggerFactory.CreateLogger<JsonGameProvider>());
    }

    private async Task ValidateProviderAsync(
        IOfficialGameProvider provider,
        IConfigurationSchemaLoader schemaLoader,
        CancellationToken cancellationToken)
    {
        var slug = provider.GameSlug;
        _logger.LogDebug("Validating provider: {GameSlug}", slug);

        try
        {
            // Validate game definition
            var definition = provider.GetGameDefinition();
            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                _logger.LogWarning("Provider {GameSlug} has empty name", slug);
            }

            // Validate configuration schemas
            var schemaFiles = schemaLoader.GetAvailableSchemaFiles(slug);
            foreach (var schemaFile in schemaFiles)
            {
                var schema = await schemaLoader.LoadSchemaAsync(slug, schemaFile, cancellationToken)
                    .ConfigureAwait(false);

                if (schema is not null)
                {
                    var validation = schemaLoader.ValidateSchema(schema);
                    if (!validation.IsValid)
                    {
                        _logger.LogWarning(
                            "Schema validation failed for {GameSlug}/{SchemaFile}: {Errors}",
                            slug, schemaFile, string.Join(", ", validation.Errors));
                    }
                }
            }

            _logger.LogDebug("Provider {GameSlug} validated successfully", slug);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate provider: {GameSlug}", slug);
        }
    }
}
