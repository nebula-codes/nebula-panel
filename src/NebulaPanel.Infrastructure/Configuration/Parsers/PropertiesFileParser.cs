using System.Text;
using System.Text.RegularExpressions;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces.Configuration;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Configuration.Parsers;

/// <summary>
/// Parser for Java .properties format files (key=value pairs).
/// Preserves comments and formatting when writing.
/// </summary>
public partial class PropertiesFileParser : IConfigFileParser
{
    public ConfigFileType SupportedType => ConfigFileType.Properties;

    public Task<Dictionary<string, object?>> ParseAsync(string content, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(content))
            return Task.FromResult(result);

        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);
        var continuationBuilder = new StringBuilder();
        string? currentKey = null;

        foreach (var rawLine in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = rawLine;

            // Handle line continuation (backslash at end of line)
            if (currentKey != null)
            {
                continuationBuilder.Append(line.TrimStart());
                if (!line.EndsWith('\\'))
                {
                    result[currentKey] = UnescapeValue(continuationBuilder.ToString());
                    currentKey = null;
                    continuationBuilder.Clear();
                }
                else
                {
                    continuationBuilder.Length--; // Remove trailing backslash
                }
                continue;
            }

            // Skip empty lines and comments
            var trimmed = line.TrimStart();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith('!'))
                continue;

            // Parse key=value or key:value or key value
            var match = KeyValuePattern().Match(line);
            if (!match.Success)
                continue;

            var key = UnescapeKey(match.Groups["key"].Value);
            var value = match.Groups["value"].Value;

            // Check for line continuation
            if (value.EndsWith('\\'))
            {
                currentKey = key;
                continuationBuilder.Append(value[..^1]); // Remove trailing backslash
                continue;
            }

            result[key] = UnescapeValue(value);
        }

        return Task.FromResult(result);
    }

    public Task<string> SerializeAsync(
        Dictionary<string, object?> values,
        string? existingContent = null,
        ConfigurationSchema? schema = null,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrEmpty(existingContent))
        {
            // Preserve existing file structure, updating values
            var lines = existingContent.Split(["\r\n", "\n"], StringSplitOptions.None);
            var writtenKeys = new HashSet<string>(StringComparer.Ordinal);
            var isFirst = true;
            var useCrLf = existingContent.Contains("\r\n");

            foreach (var rawLine in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!isFirst)
                    builder.Append(useCrLf ? "\r\n" : "\n");
                isFirst = false;

                var trimmed = rawLine.TrimStart();

                // Preserve comments and empty lines
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith('!'))
                {
                    builder.Append(rawLine);
                    continue;
                }

                // Try to parse as key=value
                var match = KeyValuePattern().Match(rawLine);
                if (!match.Success)
                {
                    builder.Append(rawLine);
                    continue;
                }

                var key = UnescapeKey(match.Groups["key"].Value);
                writtenKeys.Add(key);

                if (values.TryGetValue(key, out var value))
                {
                    // Preserve leading whitespace
                    var leadingWhitespace = rawLine[..^rawLine.TrimStart().Length];
                    builder.Append(leadingWhitespace);
                    builder.Append(EscapeKey(key));
                    builder.Append('=');
                    builder.Append(EscapeValue(ConvertToString(value)));
                }
                else
                {
                    // Keep original line if key not in values
                    builder.Append(rawLine);
                }
            }

            // Append any new keys not in original file
            foreach (var kvp in values)
            {
                if (!writtenKeys.Contains(kvp.Key))
                {
                    builder.Append(useCrLf ? "\r\n" : "\n");
                    builder.Append(EscapeKey(kvp.Key));
                    builder.Append('=');
                    builder.Append(EscapeValue(ConvertToString(kvp.Value)));
                }
            }
        }
        else
        {
            // Generate new file content
            var orderedKeys = schema?.Fields
                .OrderBy(f => f.Order)
                .ThenBy(f => f.Category)
                .Select(f => f.Key)
                .ToList() ?? values.Keys.OrderBy(k => k).ToList();

            var writtenKeys = new HashSet<string>(StringComparer.Ordinal);
            string? currentCategory = null;

            foreach (var key in orderedKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!values.TryGetValue(key, out var value))
                    continue;

                writtenKeys.Add(key);

                // Add category comment if schema provides it
                var field = schema?.Fields.FirstOrDefault(f => f.Key == key);
                if (field?.Category != null && field.Category != currentCategory)
                {
                    if (builder.Length > 0)
                        builder.AppendLine();
                    builder.AppendLine($"# {field.Category}");
                    currentCategory = field.Category;
                }

                builder.Append(EscapeKey(key));
                builder.Append('=');
                builder.AppendLine(EscapeValue(ConvertToString(value)));
            }

            // Write any remaining values not in schema
            foreach (var kvp in values.Where(kv => !writtenKeys.Contains(kv.Key)))
            {
                builder.Append(EscapeKey(kvp.Key));
                builder.Append('=');
                builder.AppendLine(EscapeValue(ConvertToString(kvp.Value)));
            }
        }

        return Task.FromResult(builder.ToString());
    }

    public object? GetValue(Dictionary<string, object?> data, string key, string? jsonPath = null)
    {
        // Properties files don't support nested paths, ignore jsonPath
        return data.TryGetValue(key, out var value) ? value : null;
    }

    public void SetValue(Dictionary<string, object?> data, string key, object? value, string? jsonPath = null)
    {
        // Properties files don't support nested paths, ignore jsonPath
        data[key] = value;
    }

    // Regex pattern for parsing key=value, key:value, or key value
    [GeneratedRegex(@"^(?<key>[^=:\s]+)\s*[=:]\s*(?<value>.*)$|^(?<key>\S+)\s+(?<value>.+)$")]
    private static partial Regex KeyValuePattern();

    private static string UnescapeKey(string key)
    {
        return Unescape(key);
    }

    private static string UnescapeValue(string value)
    {
        return Unescape(value);
    }

    private static string Unescape(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder(input.Length);
        var i = 0;

        while (i < input.Length)
        {
            var c = input[i];
            if (c == '\\' && i + 1 < input.Length)
            {
                var next = input[i + 1];
                switch (next)
                {
                    case 'n':
                        result.Append('\n');
                        i += 2;
                        break;
                    case 'r':
                        result.Append('\r');
                        i += 2;
                        break;
                    case 't':
                        result.Append('\t');
                        i += 2;
                        break;
                    case '\\':
                        result.Append('\\');
                        i += 2;
                        break;
                    case 'u' when i + 5 < input.Length:
                        // Unicode escape \uXXXX
                        var hex = input.Substring(i + 2, 4);
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var code))
                        {
                            result.Append((char)code);
                            i += 6;
                        }
                        else
                        {
                            result.Append(c);
                            i++;
                        }
                        break;
                    default:
                        // Remove backslash for escaped =, :, #, !, space
                        result.Append(next);
                        i += 2;
                        break;
                }
            }
            else
            {
                result.Append(c);
                i++;
            }
        }

        return result.ToString();
    }

    private static string EscapeKey(string key)
    {
        // Escape special characters in keys
        return key
            .Replace("\\", "\\\\")
            .Replace("=", "\\=")
            .Replace(":", "\\:")
            .Replace(" ", "\\ ")
            .Replace("#", "\\#")
            .Replace("!", "\\!");
    }

    private static string EscapeValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var result = new StringBuilder(value.Length + 10);

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '\\':
                    result.Append("\\\\");
                    break;
                case '\n':
                    result.Append("\\n");
                    break;
                case '\r':
                    result.Append("\\r");
                    break;
                case '\t':
                    result.Append("\\t");
                    break;
                default:
                    // Escape leading whitespace, #, !
                    if (i == 0 && (c == ' ' || c == '#' || c == '!'))
                        result.Append('\\');
                    result.Append(c);
                    break;
            }
        }

        return result.ToString();
    }

    private static string ConvertToString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? string.Empty
        };
    }
}
