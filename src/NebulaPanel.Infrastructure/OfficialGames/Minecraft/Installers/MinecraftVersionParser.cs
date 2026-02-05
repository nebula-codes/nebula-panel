using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Parses Minecraft version strings into structured components.
/// </summary>
public static class MinecraftVersionParser
{
    /// <summary>
    /// Parses a version string into its components.
    /// </summary>
    /// <param name="versionString">Version string like "paper-1.20.4-496".</param>
    /// <returns>Parsed version information, or null if invalid.</returns>
    public static ParsedVersion? Parse(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return null;

        var parts = versionString.Split('-');
        if (parts.Length < 2)
            return null;

        var loaderName = parts[0].ToLowerInvariant();
        var loaderType = ParseLoaderType(loaderName);
        if (loaderType is null)
            return null;

        return loaderType.Value switch
        {
            // vanilla-1.20.4
            MinecraftLoader.Vanilla => ParseVanilla(parts),
            // paper-1.20.4-496
            MinecraftLoader.Paper => ParsePaperStyle(parts, MinecraftLoader.Paper),
            // purpur-1.20.4-2155
            MinecraftLoader.Purpur => ParsePaperStyle(parts, MinecraftLoader.Purpur),
            // fabric-1.20.4-0.15.6
            MinecraftLoader.Fabric => ParseFabric(parts),
            // forge-1.20.4-49.0.38
            MinecraftLoader.Forge => ParseForge(parts),
            // spigot-1.20.4
            MinecraftLoader.Spigot => ParseSpigot(parts),
            // quilt-1.20.4-0.23.0
            MinecraftLoader.Quilt => ParseFabric(parts, MinecraftLoader.Quilt),
            _ => null
        };
    }

    private static MinecraftLoader? ParseLoaderType(string loaderName) => loaderName switch
    {
        "vanilla" => MinecraftLoader.Vanilla,
        "paper" => MinecraftLoader.Paper,
        "purpur" => MinecraftLoader.Purpur,
        "fabric" => MinecraftLoader.Fabric,
        "forge" => MinecraftLoader.Forge,
        "spigot" => MinecraftLoader.Spigot,
        "quilt" => MinecraftLoader.Quilt,
        _ => null
    };

    // vanilla-1.20.4
    private static ParsedVersion? ParseVanilla(string[] parts)
    {
        if (parts.Length < 2)
            return null;

        return new ParsedVersion
        {
            LoaderType = MinecraftLoader.Vanilla,
            MinecraftVersion = parts[1]
        };
    }

    // paper-1.20.4-496 or purpur-1.20.4-2155
    private static ParsedVersion? ParsePaperStyle(string[] parts, MinecraftLoader loader)
    {
        if (parts.Length < 3)
            return null;

        return new ParsedVersion
        {
            LoaderType = loader,
            MinecraftVersion = parts[1],
            LoaderVersion = parts[2]
        };
    }

    // fabric-1.20.4-0.15.6 or quilt-1.20.4-0.23.0
    private static ParsedVersion? ParseFabric(string[] parts, MinecraftLoader loader = MinecraftLoader.Fabric)
    {
        if (parts.Length < 3)
            return null;

        return new ParsedVersion
        {
            LoaderType = loader,
            MinecraftVersion = parts[1],
            LoaderVersion = parts[2]
        };
    }

    // forge-1.20.4-49.0.38
    private static ParsedVersion? ParseForge(string[] parts)
    {
        if (parts.Length < 3)
            return null;

        // Forge version can have multiple parts (e.g., 49.0.38)
        // Rejoin everything after MC version as the forge version
        var forgeVersion = string.Join("-", parts.Skip(2));

        return new ParsedVersion
        {
            LoaderType = MinecraftLoader.Forge,
            MinecraftVersion = parts[1],
            LoaderVersion = forgeVersion
        };
    }

    // spigot-1.20.4
    private static ParsedVersion? ParseSpigot(string[] parts)
    {
        if (parts.Length < 2)
            return null;

        return new ParsedVersion
        {
            LoaderType = MinecraftLoader.Spigot,
            MinecraftVersion = parts[1]
        };
    }
}

/// <summary>
/// Parsed version information.
/// </summary>
public record ParsedVersion
{
    /// <summary>
    /// The loader type (Vanilla, Paper, Fabric, etc.).
    /// </summary>
    public required MinecraftLoader LoaderType { get; init; }

    /// <summary>
    /// The base Minecraft version (e.g., "1.20.4").
    /// </summary>
    public required string MinecraftVersion { get; init; }

    /// <summary>
    /// The loader-specific version (build number, loader version, etc.).
    /// Null for Vanilla and Spigot.
    /// </summary>
    public string? LoaderVersion { get; init; }
}
