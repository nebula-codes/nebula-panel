namespace NebulaPanel.Domain.Enums;

public enum ConfigFileType
{
    Properties, // Java .properties format (key=value)
    Json,       // JSON format
    Yaml,       // YAML format
    Ini,        // INI format with [sections]
    Xml,        // XML format
    Toml,       // TOML format
    KeyValue,   // Simple key=value or key value (space separated)
    LineBased,  // Each line is a value (e.g., ban lists, whitelists)
    Custom      // Custom format requiring special parser
}
