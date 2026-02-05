namespace NebulaPanel.Domain.Enums;

public enum ConfigFieldType
{
    String,
    Int,
    Float,
    Bool,
    Select,
    MultiSelect,
    Path,
    Port,
    Text,           // Multi-line text
    Password,       // Hidden password input
    Color,          // Color picker
    DateTime,       // Date/time picker
    Duration,       // Time span (e.g., "2h30m")
    ByteSize,       // Size with units (e.g., "4GB")
    IpAddress,      // IP address validation
    Slider,         // Range slider with min/max
    Toggle,         // Boolean toggle (alias for Bool with different UI)
    List,           // Array of values
    KeyValuePairs   // Dictionary/map editor
}
