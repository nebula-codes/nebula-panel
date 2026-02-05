using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Information about a configuration file for a server.
/// </summary>
public record ConfigFileDto(
    string FileName,
    string? DisplayName,
    string? Description,
    ConfigFileType FileType,
    bool Exists,
    DateTime? LastModified
);

/// <summary>
/// Configuration values along with the schema.
/// </summary>
public record ConfigValuesDto(
    string FileName,
    string? DisplayName,
    ConfigFileType FileType,
    Dictionary<string, object?> Values,
    ConfigurationSchema Schema
);

/// <summary>
/// Request to update configuration values.
/// </summary>
public record UpdateConfigRequest(
    string FileName,
    Dictionary<string, object?> Values
);

/// <summary>
/// Result of configuration validation.
/// </summary>
public record ConfigValidationResult(
    bool IsValid,
    Dictionary<string, List<string>> FieldErrors,
    List<string> GeneralErrors
)
{
    public static ConfigValidationResult Success() => new(true, new(), []);

    public static ConfigValidationResult Failure(Dictionary<string, List<string>> fieldErrors, List<string>? generalErrors = null)
        => new(false, fieldErrors, generalErrors ?? []);

    public static ConfigValidationResult Failure(string field, string error)
        => new(false, new() { [field] = [error] }, []);

    public static ConfigValidationResult GeneralFailure(string error)
        => new(false, new(), [error]);
}

/// <summary>
/// A field value with metadata for display.
/// </summary>
public record ConfigFieldValueDto(
    string Key,
    ConfigField Field,
    object? Value,
    object? DefaultValue,
    bool IsModified,
    List<string>? Errors
);
