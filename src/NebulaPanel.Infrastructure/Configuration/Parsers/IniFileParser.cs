using System.Text;
using System.Text.RegularExpressions;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces.Configuration;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Configuration.Parsers;

/// <summary>
/// Parser for INI configuration files with [Section] headers.
/// Keys are stored as "Section.Key" in the dictionary.
/// </summary>
public partial class IniFileParser : IConfigFileParser
{
    public ConfigFileType SupportedType => ConfigFileType.Ini;

    public Task<Dictionary<string, object?>> ParseAsync(string content, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(content))
            return Task.FromResult(result);

        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);
        var currentSection = string.Empty;

        foreach (var rawLine in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            // Check for section header
            var sectionMatch = SectionPattern().Match(line);
            if (sectionMatch.Success)
            {
                currentSection = sectionMatch.Groups["section"].Value;
                continue;
            }

            // Parse key=value
            var kvMatch = KeyValuePattern().Match(line);
            if (!kvMatch.Success)
                continue;

            var key = kvMatch.Groups["key"].Value.Trim();
            var value = kvMatch.Groups["value"].Value.Trim();

            // Remove quotes if present
            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }

            // Remove inline comments
            var commentIndex = value.IndexOfAny([';', '#']);
            if (commentIndex >= 0)
            {
                // Check if it's inside quotes (simplified check)
                var quoteCount = value[..commentIndex].Count(c => c == '"');
                if (quoteCount % 2 == 0)
                {
                    value = value[..commentIndex].TrimEnd();
                }
            }

            // Build full key with section prefix
            var fullKey = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}.{key}";
            result[fullKey] = ParseValue(value);
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
            // Preserve existing file structure
            var lines = existingContent.Split(["\r\n", "\n"], StringSplitOptions.None);
            var writtenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentSection = string.Empty;
            var isFirst = true;
            var useCrLf = existingContent.Contains("\r\n");

            foreach (var rawLine in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!isFirst)
                    builder.Append(useCrLf ? "\r\n" : "\n");
                isFirst = false;

                var line = rawLine.Trim();

                // Preserve comments and empty lines
                if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                {
                    builder.Append(rawLine);
                    continue;
                }

                // Check for section header
                var sectionMatch = SectionPattern().Match(line);
                if (sectionMatch.Success)
                {
                    currentSection = sectionMatch.Groups["section"].Value;
                    builder.Append(rawLine);
                    continue;
                }

                // Parse key=value
                var kvMatch = KeyValuePattern().Match(line);
                if (!kvMatch.Success)
                {
                    builder.Append(rawLine);
                    continue;
                }

                var key = kvMatch.Groups["key"].Value.Trim();
                var fullKey = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}.{key}";
                writtenKeys.Add(fullKey);

                if (values.TryGetValue(fullKey, out var value))
                {
                    // Preserve leading whitespace
                    var leadingWhitespace = rawLine[..^rawLine.TrimStart().Length];
                    builder.Append(leadingWhitespace);
                    builder.Append(key);
                    builder.Append('=');
                    builder.Append(FormatValue(value));
                }
                else
                {
                    builder.Append(rawLine);
                }
            }

            // Append new values not in original file, grouped by section
            var newValues = values.Where(kv => !writtenKeys.Contains(kv.Key)).ToList();
            if (newValues.Count > 0)
            {
                var groupedValues = newValues
                    .Select(kv => new
                    {
                        kv.Key,
                        kv.Value,
                        Section = kv.Key.Contains('.') ? kv.Key[..kv.Key.LastIndexOf('.')] : string.Empty,
                        KeyName = kv.Key.Contains('.') ? kv.Key[(kv.Key.LastIndexOf('.') + 1)..] : kv.Key
                    })
                    .GroupBy(x => x.Section);

                foreach (var group in groupedValues)
                {
                    if (!string.IsNullOrEmpty(group.Key) && group.Key != currentSection)
                    {
                        builder.Append(useCrLf ? "\r\n\r\n" : "\n\n");
                        builder.Append('[');
                        builder.Append(group.Key);
                        builder.Append(']');
                    }

                    foreach (var item in group)
                    {
                        builder.Append(useCrLf ? "\r\n" : "\n");
                        builder.Append(item.KeyName);
                        builder.Append('=');
                        builder.Append(FormatValue(item.Value));
                    }
                }
            }
        }
        else
        {
            // Generate new file content
            var groupedValues = values
                .Select(kv => new
                {
                    kv.Key,
                    kv.Value,
                    Section = kv.Key.Contains('.') ? kv.Key[..kv.Key.LastIndexOf('.')] : string.Empty,
                    KeyName = kv.Key.Contains('.') ? kv.Key[(kv.Key.LastIndexOf('.') + 1)..] : kv.Key
                })
                .GroupBy(x => x.Section)
                .OrderBy(g => g.Key);

            var isFirstSection = true;

            foreach (var group in groupedValues)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!isFirstSection)
                    builder.AppendLine();
                isFirstSection = false;

                if (!string.IsNullOrEmpty(group.Key))
                {
                    builder.AppendLine($"[{group.Key}]");
                }

                foreach (var item in group.OrderBy(x => x.KeyName))
                {
                    builder.Append(item.KeyName);
                    builder.Append('=');
                    builder.AppendLine(FormatValue(item.Value));
                }
            }
        }

        return Task.FromResult(builder.ToString());
    }

    public object? GetValue(Dictionary<string, object?> data, string key, string? jsonPath = null)
    {
        // For INI files, the key already includes the section (Section.Key format)
        var lookupKey = jsonPath ?? key;
        return data.TryGetValue(lookupKey, out var value) ? value : null;
    }

    public void SetValue(Dictionary<string, object?> data, string key, object? value, string? jsonPath = null)
    {
        var lookupKey = jsonPath ?? key;
        data[lookupKey] = value;
    }

    [GeneratedRegex(@"^\[(?<section>[^\]]+)\]$")]
    private static partial Regex SectionPattern();

    [GeneratedRegex(@"^(?<key>[^=]+)=(?<value>.*)$")]
    private static partial Regex KeyValuePattern();

    private static object? ParseValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Try to parse as boolean
        if (bool.TryParse(value, out var boolResult))
            return boolResult;

        // Handle common boolean alternatives
        if (value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
            value == "1")
            return true;

        if (value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            value == "0")
            return false;

        // Try to parse as integer
        if (int.TryParse(value, out var intResult))
            return intResult;

        // Try to parse as double
        if (double.TryParse(value, out var doubleResult))
            return doubleResult;

        // Return as string
        return value;
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            bool b => b ? "true" : "false",
            string s when s.Contains(' ') || s.Contains('=') || s.Contains(';') || s.Contains('#') => $"\"{s}\"",
            _ => value.ToString() ?? string.Empty
        };
    }
}
