using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces.Configuration;

namespace NebulaPanel.Infrastructure.Configuration.Parsers;

/// <summary>
/// Factory for creating configuration file parsers based on file type.
/// </summary>
public class ConfigParserFactory(IEnumerable<IConfigFileParser> parsers) : IConfigParserFactory
{
    private readonly Dictionary<ConfigFileType, IConfigFileParser> _parsers = parsers
        .ToDictionary(p => p.SupportedType);

    public IConfigFileParser GetParser(ConfigFileType fileType)
    {
        if (_parsers.TryGetValue(fileType, out var parser))
            return parser;

        throw new NotSupportedException($"No configuration file parser available for file type: {fileType}");
    }

    public IConfigFileParser? GetParserOrDefault(ConfigFileType fileType)
    {
        return _parsers.TryGetValue(fileType, out var parser) ? parser : null;
    }

    public bool HasParser(ConfigFileType fileType)
    {
        return _parsers.ContainsKey(fileType);
    }
}
