using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Service for loading configuration schemas from various sources.
/// Supports loading from embedded resources and filesystem with filesystem taking precedence.
/// </summary>
public interface IConfigurationSchemaLoader
{
    /// <summary>
    /// Loads a configuration schema by game slug and schema file name.
    /// First checks filesystem overrides, then falls back to embedded resources.
    /// </summary>
    /// <param name="gameSlug">The game's slug (e.g., "minecraft-java")</param>
    /// <param name="schemaFileName">The schema file name (e.g., "server.properties.schema.json")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The loaded schema, or null if not found</returns>
    Task<ConfigurationSchema?> LoadSchemaAsync(
        string gameSlug,
        string schemaFileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all configuration schemas for a game.
    /// </summary>
    /// <param name="gameSlug">The game's slug</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of file name to schema</returns>
    Task<Dictionary<string, ConfigurationSchema>> LoadAllSchemasAsync(
        string gameSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of available schema file names for a game.
    /// </summary>
    /// <param name="gameSlug">The game's slug</param>
    /// <returns>List of schema file names</returns>
    IReadOnlyList<string> GetAvailableSchemaFiles(string gameSlug);

    /// <summary>
    /// Validates a schema for correctness.
    /// </summary>
    /// <param name="schema">The schema to validate</param>
    /// <returns>Validation result with any errors</returns>
    SchemaValidationResult ValidateSchema(ConfigurationSchema schema);
}

/// <summary>
/// Result of schema validation.
/// </summary>
public record SchemaValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static SchemaValidationResult Success() => new() { IsValid = true };
    public static SchemaValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors };
    public static SchemaValidationResult Failure(IEnumerable<string> errors) => new() { IsValid = false, Errors = errors.ToList() };
}
