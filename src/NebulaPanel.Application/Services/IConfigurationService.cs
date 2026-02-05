using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for reading and writing game server configuration files.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the list of configuration files available for a server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available configuration files.</returns>
    Task<IReadOnlyList<ConfigFileDto>> GetConfigFilesAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a configuration file and returns values along with schema.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="fileName">The configuration file name (relative path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Configuration values and schema.</returns>
    Task<Result<ConfigValuesDto>> ReadConfigAsync(
        Guid serverId,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes configuration values to a file.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="request">The update request containing file name and values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> WriteConfigAsync(
        Guid serverId,
        UpdateConfigRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates configuration values against a schema.
    /// </summary>
    /// <param name="schema">The configuration schema.</param>
    /// <param name="values">The values to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    ConfigValidationResult ValidateConfig(
        ConfigurationSchema schema,
        Dictionary<string, object?> values);

    /// <summary>
    /// Gets the raw content of a configuration file.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="fileName">The configuration file name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw file content.</returns>
    Task<Result<string>> GetRawConfigAsync(
        Guid serverId,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves raw content to a configuration file.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="fileName">The configuration file name.</param>
    /// <param name="content">The raw content to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> SaveRawConfigAsync(
        Guid serverId,
        string fileName,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a configuration file to default values.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="fileName">The configuration file name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> ResetConfigToDefaultsAsync(
        Guid serverId,
        string fileName,
        CancellationToken cancellationToken = default);
}
