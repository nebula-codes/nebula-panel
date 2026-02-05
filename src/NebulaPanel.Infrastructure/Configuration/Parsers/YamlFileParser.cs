using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces.Configuration;
using NebulaPanel.Domain.ValueObjects;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NebulaPanel.Infrastructure.Configuration.Parsers;

/// <summary>
/// Parser for YAML configuration files.
/// Supports nested paths via dot notation (e.g., "server.network.port").
/// </summary>
public class YamlFileParser : IConfigFileParser
{
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public YamlFileParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .Build();
    }

    public ConfigFileType SupportedType => ConfigFileType.Yaml;

    public Task<Dictionary<string, object?>> ParseAsync(string content, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(content))
            return Task.FromResult(result);

        try
        {
            using var reader = new StringReader(content);
            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count > 0 && yaml.Documents[0].RootNode is YamlMappingNode rootMapping)
            {
                FlattenYaml(rootMapping, string.Empty, result);
            }
        }
        catch (YamlDotNet.Core.YamlException)
        {
            // Return empty dictionary on parse error
        }

        return Task.FromResult(result);
    }

    public Task<string> SerializeAsync(
        Dictionary<string, object?> values,
        string? existingContent = null,
        ConfigurationSchema? schema = null,
        CancellationToken cancellationToken = default)
    {
        // Build nested dictionary structure from flat keys
        var nested = BuildNestedDictionary(values);

        if (!string.IsNullOrWhiteSpace(existingContent))
        {
            try
            {
                // Parse existing content and merge
                var existing = _deserializer.Deserialize<Dictionary<object, object>>(existingContent);
                if (existing != null)
                {
                    MergeInto(existing, nested);
                    return Task.FromResult(_serializer.Serialize(existing));
                }
            }
            catch (YamlDotNet.Core.YamlException)
            {
                // Fall through to serialize new content
            }
        }

        return Task.FromResult(_serializer.Serialize(nested));
    }

    public object? GetValue(Dictionary<string, object?> data, string key, string? jsonPath = null)
    {
        var lookupKey = jsonPath ?? key;
        return data.TryGetValue(lookupKey, out var value) ? value : null;
    }

    public void SetValue(Dictionary<string, object?> data, string key, object? value, string? jsonPath = null)
    {
        var lookupKey = jsonPath ?? key;
        data[lookupKey] = value;
    }

    /// <summary>
    /// Flattens a YAML mapping node into a dictionary with dot-notation keys.
    /// </summary>
    private static void FlattenYaml(YamlMappingNode mapping, string prefix, Dictionary<string, object?> result)
    {
        foreach (var entry in mapping.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode)
                continue;

            var keyName = keyNode.Value ?? string.Empty;
            var fullKey = string.IsNullOrEmpty(prefix) ? keyName : $"{prefix}.{keyName}";

            switch (entry.Value)
            {
                case YamlMappingNode nestedMapping:
                    FlattenYaml(nestedMapping, fullKey, result);
                    break;

                case YamlSequenceNode sequence:
                    result[fullKey] = ConvertSequence(sequence);
                    break;

                case YamlScalarNode scalar:
                    result[fullKey] = ConvertScalar(scalar);
                    break;

                default:
                    result[fullKey] = null;
                    break;
            }
        }
    }

    /// <summary>
    /// Converts a YAML sequence to a List.
    /// </summary>
    private static List<object?> ConvertSequence(YamlSequenceNode sequence)
    {
        var list = new List<object?>();

        foreach (var item in sequence)
        {
            switch (item)
            {
                case YamlMappingNode nestedMapping:
                    var dict = new Dictionary<string, object?>();
                    FlattenYaml(nestedMapping, string.Empty, dict);
                    list.Add(dict);
                    break;

                case YamlSequenceNode nestedSequence:
                    list.Add(ConvertSequence(nestedSequence));
                    break;

                case YamlScalarNode scalar:
                    list.Add(ConvertScalar(scalar));
                    break;

                default:
                    list.Add(null);
                    break;
            }
        }

        return list;
    }

    /// <summary>
    /// Converts a YAML scalar node to a .NET type.
    /// </summary>
    private static object? ConvertScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;

        if (value == null)
            return null;

        // Handle null representations
        if (value == "~" || value.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;

        // Handle boolean
        if (bool.TryParse(value, out var boolResult))
            return boolResult;

        // Handle common boolean alternatives
        if (value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("on", StringComparison.OrdinalIgnoreCase))
            return true;

        if (value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("off", StringComparison.OrdinalIgnoreCase))
            return false;

        // Handle integer
        if (int.TryParse(value, out var intResult))
            return intResult;

        // Handle long
        if (long.TryParse(value, out var longResult))
            return longResult;

        // Handle double
        if (double.TryParse(value, out var doubleResult))
            return doubleResult;

        // Return as string
        return value;
    }

    /// <summary>
    /// Builds a nested dictionary from flat dot-notation keys.
    /// </summary>
    private static Dictionary<object, object?> BuildNestedDictionary(Dictionary<string, object?> flat)
    {
        var result = new Dictionary<object, object?>();

        foreach (var kvp in flat)
        {
            var parts = kvp.Key.Split('.');
            var current = result;

            for (var i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];

                if (!current.TryGetValue(part, out var existing) || existing is not Dictionary<object, object?> nested)
                {
                    nested = new Dictionary<object, object?>();
                    current[part] = nested;
                }

                current = nested;
            }

            current[parts[^1]] = kvp.Value;
        }

        return result;
    }

    /// <summary>
    /// Merges source dictionary into target, overwriting values.
    /// </summary>
    private static void MergeInto(Dictionary<object, object> target, Dictionary<object, object?> source)
    {
        foreach (var kvp in source)
        {
            if (kvp.Value is Dictionary<object, object?> sourceNested &&
                target.TryGetValue(kvp.Key, out var existing) &&
                existing is Dictionary<object, object> targetNested)
            {
                MergeInto(targetNested, sourceNested);
            }
            else
            {
                target[kvp.Key] = kvp.Value;
            }
        }
    }
}
