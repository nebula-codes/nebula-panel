using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Interfaces.Configuration;

/// <summary>
/// Interface for parsing and serializing configuration files of various formats.
/// </summary>
public interface IConfigFileParser
{
    /// <summary>
    /// The file type this parser supports.
    /// </summary>
    ConfigFileType SupportedType { get; }

    /// <summary>
    /// Parses the content of a configuration file into a dictionary of key-value pairs.
    /// </summary>
    /// <param name="content">The raw file content to parse.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of configuration values.</returns>
    Task<Dictionary<string, object?>> ParseAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes configuration values back to file content.
    /// </summary>
    /// <param name="values">The configuration values to serialize.</param>
    /// <param name="existingContent">Optional existing file content to preserve formatting/comments.</param>
    /// <param name="schema">Optional schema for ordering and formatting hints.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The serialized file content.</returns>
    Task<string> SerializeAsync(
        Dictionary<string, object?> values,
        string? existingContent = null,
        ConfigurationSchema? schema = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value from the parsed data, supporting nested paths for hierarchical formats.
    /// </summary>
    /// <param name="data">The parsed configuration data.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="jsonPath">Optional path for nested values (e.g., "server.network.port").</param>
    /// <returns>The value, or null if not found.</returns>
    object? GetValue(Dictionary<string, object?> data, string key, string? jsonPath = null);

    /// <summary>
    /// Sets a value in the configuration data, supporting nested paths for hierarchical formats.
    /// </summary>
    /// <param name="data">The configuration data to modify.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="jsonPath">Optional path for nested values.</param>
    void SetValue(Dictionary<string, object?> data, string key, object? value, string? jsonPath = null);
}

/// <summary>
/// Interface for the configuration parser factory.
/// </summary>
public interface IConfigParserFactory
{
    /// <summary>
    /// Gets the parser for the specified file type.
    /// </summary>
    /// <param name="fileType">The configuration file type.</param>
    /// <returns>The appropriate parser.</returns>
    /// <exception cref="NotSupportedException">Thrown when no parser is available for the file type.</exception>
    IConfigFileParser GetParser(ConfigFileType fileType);

    /// <summary>
    /// Attempts to get the parser for the specified file type.
    /// </summary>
    /// <param name="fileType">The configuration file type.</param>
    /// <returns>The parser if found, null otherwise.</returns>
    IConfigFileParser? GetParserOrDefault(ConfigFileType fileType);

    /// <summary>
    /// Checks if a parser is available for the specified file type.
    /// </summary>
    /// <param name="fileType">The configuration file type.</param>
    /// <returns>True if a parser is available, false otherwise.</returns>
    bool HasParser(ConfigFileType fileType);
}
