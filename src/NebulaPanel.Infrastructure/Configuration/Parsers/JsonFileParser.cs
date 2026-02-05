using System.Text.Json;
using System.Text.Json.Nodes;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces.Configuration;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Configuration.Parsers;

/// <summary>
/// Parser for JSON configuration files.
/// Supports nested paths via JsonPath notation (e.g., "server.network.port").
/// </summary>
public class JsonFileParser : IConfigFileParser
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null, // Preserve original casing
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ConfigFileType SupportedType => ConfigFileType.Json;

    public Task<Dictionary<string, object?>> ParseAsync(string content, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(content))
            return Task.FromResult(result);

        try
        {
            var jsonNode = JsonNode.Parse(content, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (jsonNode is JsonObject rootObject)
            {
                FlattenJson(rootObject, string.Empty, result);
            }
        }
        catch (JsonException)
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
        JsonObject root;

        if (!string.IsNullOrWhiteSpace(existingContent))
        {
            // Parse existing content to preserve structure
            try
            {
                var parsed = JsonNode.Parse(existingContent, documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
                root = parsed as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        // Apply values
        foreach (var kvp in values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if there's a schema field with JsonPath
            var field = schema?.Fields.FirstOrDefault(f => f.Key == kvp.Key);
            var path = field?.JsonPath ?? kvp.Key;

            SetNestedValue(root, path, kvp.Value);
        }

        return Task.FromResult(root.ToJsonString(DefaultOptions));
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
    /// Flattens a JSON object into a dictionary with dot-notation keys.
    /// </summary>
    private static void FlattenJson(JsonObject obj, string prefix, Dictionary<string, object?> result)
    {
        foreach (var property in obj)
        {
            var key = string.IsNullOrEmpty(prefix) ? property.Key : $"{prefix}.{property.Key}";

            switch (property.Value)
            {
                case JsonObject nested:
                    FlattenJson(nested, key, result);
                    break;

                case JsonArray array:
                    result[key] = ConvertJsonArray(array);
                    break;

                case JsonValue jsonValue:
                    result[key] = ConvertJsonValue(jsonValue);
                    break;

                case null:
                    result[key] = null;
                    break;
            }
        }
    }

    /// <summary>
    /// Converts a JsonValue to a .NET type.
    /// </summary>
    private static object? ConvertJsonValue(JsonValue value)
    {
        var element = value.GetValue<JsonElement>();

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Converts a JsonArray to a List.
    /// </summary>
    private static List<object?> ConvertJsonArray(JsonArray array)
    {
        var list = new List<object?>(array.Count);

        foreach (var item in array)
        {
            switch (item)
            {
                case JsonObject nested:
                    var dict = new Dictionary<string, object?>();
                    FlattenJson(nested, string.Empty, dict);
                    list.Add(dict);
                    break;

                case JsonArray nestedArray:
                    list.Add(ConvertJsonArray(nestedArray));
                    break;

                case JsonValue jsonValue:
                    list.Add(ConvertJsonValue(jsonValue));
                    break;

                case null:
                    list.Add(null);
                    break;
            }
        }

        return list;
    }

    /// <summary>
    /// Sets a value at a nested path in a JsonObject.
    /// </summary>
    private static void SetNestedValue(JsonObject root, string path, object? value)
    {
        var parts = path.Split('.');
        var current = root;

        // Navigate/create nested objects
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];

            if (current.TryGetPropertyValue(part, out var existing) && existing is JsonObject existingObj)
            {
                current = existingObj;
            }
            else
            {
                var newObj = new JsonObject();
                current[part] = newObj;
                current = newObj;
            }
        }

        // Set the final value
        var finalKey = parts[^1];
        current[finalKey] = ConvertToJsonNode(value);
    }

    /// <summary>
    /// Converts a .NET value to a JsonNode.
    /// </summary>
    private static JsonNode? ConvertToJsonNode(object? value)
    {
        return value switch
        {
            null => null,
            JsonNode node => node,
            string s => JsonValue.Create(s),
            bool b => JsonValue.Create(b),
            int i => JsonValue.Create(i),
            long l => JsonValue.Create(l),
            float f => JsonValue.Create(f),
            double d => JsonValue.Create(d),
            decimal dec => JsonValue.Create(dec),
            IEnumerable<object?> list => CreateJsonArray(list),
            IDictionary<string, object?> dict => CreateJsonObject(dict),
            _ => JsonValue.Create(value.ToString())
        };
    }

    private static JsonArray CreateJsonArray(IEnumerable<object?> items)
    {
        var array = new JsonArray();
        foreach (var item in items)
        {
            array.Add(ConvertToJsonNode(item));
        }
        return array;
    }

    private static JsonObject CreateJsonObject(IDictionary<string, object?> dict)
    {
        var obj = new JsonObject();
        foreach (var kvp in dict)
        {
            obj[kvp.Key] = ConvertToJsonNode(kvp.Value);
        }
        return obj;
    }
}
