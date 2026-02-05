using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

public class ConfigurationSchema
{
    public string FileName { get; set; } = string.Empty;        // "server.properties", "serverconfig.json"
    public string? DisplayName { get; set; }                    // Human-readable name for UI
    public string? Description { get; set; }                    // Schema description
    public ConfigFileType FileType { get; set; }                // Properties, Json, Yaml, Ini, Custom
    public List<ConfigField> Fields { get; set; } = [];
    public List<ConfigSection> Sections { get; set; } = [];     // For grouping fields in UI
    public bool CreateIfMissing { get; set; } = true;           // Create file if not exists
    public string? Template { get; set; }                       // Default file content template
}

public class ConfigSection
{
    public string Key { get; set; } = string.Empty;             // Section identifier
    public string DisplayName { get; set; } = string.Empty;     // Human-readable name
    public string? Description { get; set; }                    // Section description
    public string? Icon { get; set; }                           // Icon name for UI
    public bool DefaultExpanded { get; set; } = true;           // Whether section is expanded by default
    public int Order { get; set; }                              // Display order
}

public class ConfigField
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ConfigFieldType Type { get; set; }                   // String, Int, Bool, Select, MultiSelect, etc.
    public object? DefaultValue { get; set; }
    public List<SelectOption>? Options { get; set; }
    public ValidationRule? Validation { get; set; }
    public string? Category { get; set; }                       // For grouping in UI (maps to Section.Key)

    // Extended properties
    public bool Required { get; set; }                          // Field is required
    public bool Advanced { get; set; }                          // Hide in basic view
    public Dictionary<string, object>? ShowWhen { get; set; }   // Conditional visibility
    public string? JsonPath { get; set; }                       // For nested JSON/YAML (e.g., "server.network.port")
    public string? Placeholder { get; set; }                    // Input placeholder text
    public int Order { get; set; }                              // Display order within section
    public SliderOptions? SliderOptions { get; set; }           // For Slider type
    public string? Unit { get; set; }                           // Display unit (e.g., "MB", "ms")
    public bool ReadOnly { get; set; }                          // Non-editable field
}

public class SelectOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class ValidationRule
{
    public int? MinValue { get; set; }
    public int? MaxValue { get; set; }
    public double? MinValueDouble { get; set; }                 // For Float type
    public double? MaxValueDouble { get; set; }                 // For Float type
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }                        // Regex pattern
    public string? ErrorMessage { get; set; }
    public List<string>? AllowedValues { get; set; }            // Whitelist of allowed values
    public List<string>? ForbiddenValues { get; set; }          // Blacklist of forbidden values
}

public class SliderOptions
{
    public double Min { get; set; }
    public double Max { get; set; }
    public double Step { get; set; } = 1;
    public bool ShowLabels { get; set; } = true;                // Show min/max labels
    public bool ShowValue { get; set; } = true;                 // Show current value
}
