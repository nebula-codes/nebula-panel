using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames;

/// <summary>
/// Loads configuration schemas from embedded resources or filesystem.
/// Filesystem takes precedence to allow user customization without code changes.
/// </summary>
public class ConfigurationSchemaLoader : IConfigurationSchemaLoader
{
    private readonly ILogger<ConfigurationSchemaLoader> _logger;
    private readonly string _schemasBasePath;
    private readonly Assembly _resourceAssembly;
    private readonly Dictionary<string, List<string>> _schemaFileCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public ConfigurationSchemaLoader(
        ILogger<ConfigurationSchemaLoader> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _resourceAssembly = typeof(ConfigurationSchemaLoader).Assembly;
        _schemasBasePath = configuration["OfficialGames:SchemasPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "schemas");

        BuildSchemaFileCache();
    }

    /// <summary>
    /// Builds a cache of available schema files from embedded resources.
    /// </summary>
    private void BuildSchemaFileCache()
    {
        var resourceNames = _resourceAssembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Schemas.") && n.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var resourceName in resourceNames)
        {
            // Parse resource name: NebulaPanel.Infrastructure.OfficialGames.Minecraft.Schemas.server.properties.schema.json
            var parts = resourceName.Split('.');
            var schemasIndex = Array.FindIndex(parts, p => p.Equals("Schemas", StringComparison.OrdinalIgnoreCase));

            if (schemasIndex > 0 && schemasIndex < parts.Length - 1)
            {
                // Get game slug from the part before "Schemas"
                var gameSlug = parts[schemasIndex - 1].ToLowerInvariant();

                // Get schema file name (everything after "Schemas" joined back together)
                var schemaFileName = string.Join(".", parts.Skip(schemasIndex + 1));

                if (!_schemaFileCache.TryGetValue(gameSlug, out var files))
                {
                    files = [];
                    _schemaFileCache[gameSlug] = files;
                }

                files.Add(schemaFileName);
                _logger.LogDebug("Discovered schema: {GameSlug}/{SchemaFile}", gameSlug, schemaFileName);
            }
        }

        _logger.LogInformation("Built schema cache with {Count} games", _schemaFileCache.Count);
    }

    public async Task<ConfigurationSchema?> LoadSchemaAsync(
        string gameSlug,
        string schemaFileName,
        CancellationToken cancellationToken = default)
    {
        gameSlug = gameSlug.ToLowerInvariant();

        // 1. Check filesystem override first
        var filePath = Path.Combine(_schemasBasePath, gameSlug, schemaFileName);
        if (File.Exists(filePath))
        {
            _logger.LogDebug("Loading schema from filesystem: {Path}", filePath);
            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                return DeserializeSchema(json, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load schema from filesystem: {Path}", filePath);
            }
        }

        // 2. Fall back to embedded resource
        var resourceName = BuildResourceName(gameSlug, schemaFileName);
        using var stream = _resourceAssembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            _logger.LogWarning("Schema not found: {ResourceName}", resourceName);
            return null;
        }

        _logger.LogDebug("Loading schema from embedded resource: {ResourceName}", resourceName);
        return await JsonSerializer.DeserializeAsync<ConfigurationSchema>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<string, ConfigurationSchema>> LoadAllSchemasAsync(
        string gameSlug,
        CancellationToken cancellationToken = default)
    {
        gameSlug = gameSlug.ToLowerInvariant();
        var schemas = new Dictionary<string, ConfigurationSchema>();

        // Get list of available schema files
        var schemaFiles = GetAvailableSchemaFiles(gameSlug);

        foreach (var schemaFile in schemaFiles)
        {
            var schema = await LoadSchemaAsync(gameSlug, schemaFile, cancellationToken).ConfigureAwait(false);
            if (schema is not null)
            {
                // Use the configured filename as the key
                schemas[schema.FileName] = schema;
            }
        }

        _logger.LogDebug("Loaded {Count} schemas for game {GameSlug}", schemas.Count, gameSlug);
        return schemas;
    }

    public IReadOnlyList<string> GetAvailableSchemaFiles(string gameSlug)
    {
        gameSlug = gameSlug.ToLowerInvariant();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add embedded resource schemas
        if (_schemaFileCache.TryGetValue(gameSlug, out var embeddedFiles))
        {
            foreach (var file in embeddedFiles)
            {
                files.Add(file);
            }
        }

        // Add filesystem schemas
        var gameSchemaPath = Path.Combine(_schemasBasePath, gameSlug);
        if (Directory.Exists(gameSchemaPath))
        {
            foreach (var file in Directory.GetFiles(gameSchemaPath, "*.schema.json"))
            {
                files.Add(Path.GetFileName(file));
            }
        }

        return files.ToList();
    }

    public SchemaValidationResult ValidateSchema(ConfigurationSchema schema)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(schema.FileName))
        {
            errors.Add("Schema fileName is required");
        }

        if (schema.Fields.Count == 0)
        {
            errors.Add("Schema must have at least one field");
        }

        foreach (var field in schema.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key))
            {
                errors.Add($"Field key is required");
            }

            if (string.IsNullOrWhiteSpace(field.DisplayName))
            {
                errors.Add($"Field '{field.Key}' displayName is required");
            }

            if (field.Type == ConfigFieldType.Select && (field.Options is null || field.Options.Count == 0))
            {
                errors.Add($"Field '{field.Key}' is a select but has no options");
            }

            // Validate validation rules if present
            if (field.Validation is not null)
            {
                if (field.Validation.MinValue.HasValue && field.Validation.MaxValue.HasValue
                    && field.Validation.MinValue > field.Validation.MaxValue)
                {
                    errors.Add($"Field '{field.Key}' has minValue > maxValue");
                }

                if (field.Validation.MinLength.HasValue && field.Validation.MaxLength.HasValue
                    && field.Validation.MinLength > field.Validation.MaxLength)
                {
                    errors.Add($"Field '{field.Key}' has minLength > maxLength");
                }
            }
        }

        return errors.Count == 0
            ? SchemaValidationResult.Success()
            : SchemaValidationResult.Failure(errors);
    }

    private string BuildResourceName(string gameSlug, string schemaFileName)
    {
        // Convert game slug to proper casing for resource name
        // e.g., "minecraft-java" -> "Minecraft" (we store under simplified folder names)
        var gameFolder = gameSlug switch
        {
            "minecraft-java" => "Minecraft",
            "minecraft" => "Minecraft",
            _ => char.ToUpper(gameSlug[0]) + gameSlug[1..].Replace("-", "")
        };

        return $"NebulaPanel.Infrastructure.OfficialGames.{gameFolder}.Schemas.{schemaFileName}";
    }

    private ConfigurationSchema? DeserializeSchema(string json, string source)
    {
        try
        {
            var schema = JsonSerializer.Deserialize<ConfigurationSchema>(json, JsonOptions);
            if (schema is not null)
            {
                var validation = ValidateSchema(schema);
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Schema validation failed for {Source}: {Errors}",
                        source, string.Join(", ", validation.Errors));
                }
            }
            return schema;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize schema from {Source}", source);
            return null;
        }
    }
}
