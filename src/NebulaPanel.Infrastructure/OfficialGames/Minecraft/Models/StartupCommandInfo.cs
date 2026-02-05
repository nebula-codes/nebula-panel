namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

/// <summary>
/// Information needed to start a Minecraft server.
/// </summary>
public record StartupCommandInfo
{
    /// <summary>
    /// Path to the executable jar file (relative to install directory).
    /// Null for modern Forge which uses args files directly.
    /// </summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// Arguments to pass after the jar file (e.g., "nogui").
    /// For modern Forge, this includes the @argsfile references.
    /// </summary>
    public required string Arguments { get; init; }

    /// <summary>
    /// Whether this loader uses argument files (@file syntax).
    /// Modern Forge uses this approach.
    /// </summary>
    public bool UseArgsFile { get; init; }

    /// <summary>
    /// Additional JVM arguments recommended for this loader.
    /// </summary>
    public string? RecommendedJvmArgs { get; init; }
}
