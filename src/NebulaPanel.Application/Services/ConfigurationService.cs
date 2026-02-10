using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Interfaces.Configuration;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for reading and writing game server configuration files.
/// </summary>
public class ConfigurationService(
    IGameServerRepository serverRepository,
    IServerFileManager fileManager,
    IConfigParserFactory parserFactory,
    ILogger<ConfigurationService> logger) : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger = logger;
    public async Task<IReadOnlyList<ConfigFileDto>> GetConfigFilesAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server == null)
            return [];

        var schemas = server.Game.ConfigurationSchemas;
        if (schemas.Count == 0)
            return [];

        var results = new List<ConfigFileDto>(schemas.Count);

        foreach (var (fileName, schema) in schemas)
        {
            var exists = await fileManager.ExistsAsync(server, fileName, cancellationToken).ConfigureAwait(false);
            DateTime? lastModified = null;

            if (exists)
            {
                try
                {
                    var info = await fileManager.GetInfoAsync(server, fileName, cancellationToken).ConfigureAwait(false);
                    lastModified = info.ModifiedAt;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to get file info for {FileName}", fileName);
                }
            }

            results.Add(new ConfigFileDto(
                FileName: fileName,
                DisplayName: schema.DisplayName ?? fileName,
                Description: schema.Description,
                FileType: schema.FileType,
                Exists: exists,
                LastModified: lastModified));
        }

        return results;
    }

    public async Task<Result<ConfigValuesDto>> ReadConfigAsync(
        Guid serverId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server == null)
            return Result.Failure<ConfigValuesDto>(Error.NotFound("Server", serverId.ToString()));

        if (!server.Game.ConfigurationSchemas.TryGetValue(fileName, out var schema))
            return Result.Failure<ConfigValuesDto>(Error.NotFound("ConfigurationSchema", fileName));

        var parser = parserFactory.GetParserOrDefault(schema.FileType);
        if (parser == null)
            return Result.Failure<ConfigValuesDto>(Error.InvalidOperation($"No parser available for file type: {schema.FileType}"));

        Dictionary<string, object?> values;

        var exists = await fileManager.ExistsAsync(server, fileName, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            try
            {
                var content = await fileManager.ReadFileAsync(server, fileName, cancellationToken).ConfigureAwait(false);
                values = await parser.ParseAsync(content, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return Result.Failure<ConfigValuesDto>(Error.ExternalService("ConfigParser", $"Failed to read configuration file: {ex.Message}"));
            }
        }
        else
        {
            values = new Dictionary<string, object?>();
        }

        // Fill in defaults for missing values
        foreach (var field in schema.Fields)
        {
            var key = field.JsonPath ?? field.Key;
            if (!values.ContainsKey(key) && field.DefaultValue != null)
            {
                values[key] = field.DefaultValue;
            }
        }

        return Result.Success(new ConfigValuesDto(
            FileName: fileName,
            DisplayName: schema.DisplayName ?? fileName,
            FileType: schema.FileType,
            Values: values,
            Schema: schema));
    }

    public async Task<Result> WriteConfigAsync(
        Guid serverId,
        UpdateConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server == null)
            return Result.Failure(Error.NotFound("Server", serverId.ToString()));

        if (!server.Game.ConfigurationSchemas.TryGetValue(request.FileName, out var schema))
            return Result.Failure(Error.NotFound("ConfigurationSchema", request.FileName));

        // Validate the values
        var validation = ValidateConfig(schema, request.Values);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.FieldErrors
                .SelectMany(kvp => kvp.Value.Select(e => $"{kvp.Key}: {e}")));
            return Result.Failure(Error.Validation($"Validation failed: {errors}"));
        }

        var parser = parserFactory.GetParserOrDefault(schema.FileType);
        if (parser == null)
            return Result.Failure(Error.InvalidOperation($"No parser available for file type: {schema.FileType}"));

        try
        {
            // Read existing content to preserve formatting
            string? existingContent = null;
            if (await fileManager.ExistsAsync(server, request.FileName, cancellationToken).ConfigureAwait(false))
            {
                existingContent = await fileManager.ReadFileAsync(server, request.FileName, cancellationToken).ConfigureAwait(false);
            }

            // Serialize with the parser
            var content = await parser.SerializeAsync(request.Values, existingContent, schema, cancellationToken).ConfigureAwait(false);

            // Write the file
            await fileManager.WriteFileAsync(server, request.FileName, content, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.ExternalService("FileManager", $"Failed to write configuration file: {ex.Message}"));
        }
    }

    public ConfigValidationResult ValidateConfig(
        ConfigurationSchema schema,
        Dictionary<string, object?> values)
    {
        var fieldErrors = new Dictionary<string, List<string>>();

        foreach (var field in schema.Fields)
        {
            var key = field.JsonPath ?? field.Key;
            values.TryGetValue(key, out var value);

            var errors = ValidateField(field, value);
            if (errors.Count > 0)
            {
                fieldErrors[key] = errors;
            }
        }

        return fieldErrors.Count == 0
            ? ConfigValidationResult.Success()
            : ConfigValidationResult.Failure(fieldErrors);
    }

    public async Task<Result<string>> GetRawConfigAsync(
        Guid serverId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server == null)
            return Result.Failure<string>(Error.NotFound("Server", serverId.ToString()));

        if (!server.Game.ConfigurationSchemas.ContainsKey(fileName))
            return Result.Failure<string>(Error.NotFound("ConfigurationSchema", fileName));

        try
        {
            if (!await fileManager.ExistsAsync(server, fileName, cancellationToken).ConfigureAwait(false))
                return Result.Success(string.Empty);

            var content = await fileManager.ReadFileAsync(server, fileName, cancellationToken).ConfigureAwait(false);
            return Result.Success(content);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(Error.ExternalService("FileManager", $"Failed to read file: {ex.Message}"));
        }
    }

    public async Task<Result> SaveRawConfigAsync(
        Guid serverId,
        string fileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server == null)
            return Result.Failure(Error.NotFound("Server", serverId.ToString()));

        if (!server.Game.ConfigurationSchemas.ContainsKey(fileName))
            return Result.Failure(Error.NotFound("ConfigurationSchema", fileName));

        try
        {
            await fileManager.WriteFileAsync(server, fileName, content, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.ExternalService("FileManager", $"Failed to write file: {ex.Message}"));
        }
    }

    public async Task<Result> ResetConfigToDefaultsAsync(
        Guid serverId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var server = await serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server == null)
            return Result.Failure(Error.NotFound("Server", serverId.ToString()));

        if (!server.Game.ConfigurationSchemas.TryGetValue(fileName, out var schema))
            return Result.Failure(Error.NotFound("ConfigurationSchema", fileName));

        // Build default values from schema
        var defaultValues = new Dictionary<string, object?>();
        foreach (var field in schema.Fields)
        {
            var key = field.JsonPath ?? field.Key;
            if (field.DefaultValue != null)
            {
                defaultValues[key] = field.DefaultValue;
            }
        }

        // Use template if available, otherwise generate from defaults
        if (!string.IsNullOrEmpty(schema.Template))
        {
            try
            {
                await fileManager.WriteFileAsync(server, fileName, schema.Template, cancellationToken).ConfigureAwait(false);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(Error.ExternalService("FileManager", $"Failed to write template: {ex.Message}"));
            }
        }

        // Generate from defaults
        var parser = parserFactory.GetParserOrDefault(schema.FileType);
        if (parser == null)
            return Result.Failure(Error.InvalidOperation($"No parser available for file type: {schema.FileType}"));

        try
        {
            var content = await parser.SerializeAsync(defaultValues, null, schema, cancellationToken).ConfigureAwait(false);
            await fileManager.WriteFileAsync(server, fileName, content, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.ExternalService("FileManager", $"Failed to reset configuration: {ex.Message}"));
        }
    }

    private static List<string> ValidateField(ConfigField field, object? value)
    {
        var errors = new List<string>();

        // Check required
        if (field.Required && IsEmpty(value))
        {
            errors.Add($"{field.DisplayName} is required");
            return errors; // Don't validate further if required and empty
        }

        // Skip validation if empty and not required
        if (IsEmpty(value))
            return errors;

        var validation = field.Validation;
        if (validation == null)
            return errors;

        // Type-specific validation
        switch (field.Type)
        {
            case ConfigFieldType.String:
            case ConfigFieldType.Text:
            case ConfigFieldType.Password:
                ValidateString(field, value?.ToString() ?? string.Empty, validation, errors);
                break;

            case ConfigFieldType.Int:
            case ConfigFieldType.Slider:
            case ConfigFieldType.Port:
                ValidateInt(field, value, validation, errors);
                break;

            case ConfigFieldType.Float:
                ValidateFloat(field, value, validation, errors);
                break;

            case ConfigFieldType.Select:
                ValidateSelect(field, value, errors);
                break;

            case ConfigFieldType.IpAddress:
                ValidateIpAddress(value?.ToString(), errors);
                break;
        }

        return errors;
    }

    private static bool IsEmpty(object? value)
    {
        return value == null || (value is string s && string.IsNullOrWhiteSpace(s));
    }

    private static void ValidateString(ConfigField field, string value, ValidationRule validation, List<string> errors)
    {
        if (validation.MinLength.HasValue && value.Length < validation.MinLength.Value)
        {
            errors.Add($"{field.DisplayName} must be at least {validation.MinLength} characters");
        }

        if (validation.MaxLength.HasValue && value.Length > validation.MaxLength.Value)
        {
            errors.Add($"{field.DisplayName} must be at most {validation.MaxLength} characters");
        }

        if (!string.IsNullOrEmpty(validation.Pattern))
        {
            try
            {
                if (!Regex.IsMatch(value, validation.Pattern))
                {
                    errors.Add(validation.ErrorMessage ?? $"{field.DisplayName} has an invalid format");
                }
            }
            catch (RegexParseException)
            {
                // Invalid regex pattern in schema - skip validation for this field
            }
        }

        if (validation.ForbiddenValues?.Contains(value) == true)
        {
            errors.Add($"{field.DisplayName} contains a forbidden value");
        }
    }

    private static void ValidateInt(ConfigField field, object? value, ValidationRule validation, List<string> errors)
    {
        if (!TryConvertToInt(value, out var intValue))
        {
            errors.Add($"{field.DisplayName} must be a valid integer");
            return;
        }

        if (validation.MinValue.HasValue && intValue < validation.MinValue.Value)
        {
            errors.Add($"{field.DisplayName} must be at least {validation.MinValue}");
        }

        if (validation.MaxValue.HasValue && intValue > validation.MaxValue.Value)
        {
            errors.Add($"{field.DisplayName} must be at most {validation.MaxValue}");
        }

        // Special validation for Port type
        if (field.Type == ConfigFieldType.Port)
        {
            if (intValue < 1 || intValue > 65535)
            {
                errors.Add($"{field.DisplayName} must be between 1 and 65535");
            }
        }
    }

    private static void ValidateFloat(ConfigField field, object? value, ValidationRule validation, List<string> errors)
    {
        if (!TryConvertToDouble(value, out var doubleValue))
        {
            errors.Add($"{field.DisplayName} must be a valid number");
            return;
        }

        var min = validation.MinValueDouble ?? validation.MinValue;
        var max = validation.MaxValueDouble ?? validation.MaxValue;

        if (min.HasValue && doubleValue < min.Value)
        {
            errors.Add($"{field.DisplayName} must be at least {min}");
        }

        if (max.HasValue && doubleValue > max.Value)
        {
            errors.Add($"{field.DisplayName} must be at most {max}");
        }
    }

    private static void ValidateSelect(ConfigField field, object? value, List<string> errors)
    {
        if (field.Options == null || field.Options.Count == 0)
            return;

        var stringValue = value?.ToString();
        if (stringValue == null)
            return;

        var validValues = field.Options.Select(o => o.Value).ToList();
        if (!validValues.Contains(stringValue))
        {
            errors.Add($"{field.DisplayName} must be one of: {string.Join(", ", validValues)}");
        }
    }

    private static void ValidateIpAddress(string? value, List<string> errors)
    {
        if (string.IsNullOrEmpty(value))
            return;

        if (!System.Net.IPAddress.TryParse(value, out _))
        {
            errors.Add("Invalid IP address format");
        }
    }

    private static bool TryConvertToInt(object? value, out int result)
    {
        result = 0;

        return value switch
        {
            int i => (result = i) == i,
            long l when l >= int.MinValue && l <= int.MaxValue => (result = (int)l) == (int)l,
            double d when d >= int.MinValue && d <= int.MaxValue => (result = (int)d) == (int)d,
            string s => int.TryParse(s, out result),
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.TryGetInt32(out result),
            _ => false
        };
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;

        return value switch
        {
            double d => (result = d) == d,
            float f => (result = f) == f,
            int i => (result = i) == i,
            long l => (result = l) == l,
            decimal m => (result = (double)m) == (double)m,
            string s => double.TryParse(s, out result),
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.TryGetDouble(out result),
            _ => false
        };
    }
}
