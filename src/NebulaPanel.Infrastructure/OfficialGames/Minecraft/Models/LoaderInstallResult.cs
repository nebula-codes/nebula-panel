namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

/// <summary>
/// Result from a loader-specific installation operation.
/// </summary>
public record LoaderInstallResult
{
    /// <summary>
    /// Whether the installation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if installation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Path to the main executable jar file.
    /// </summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// The version that was installed.
    /// </summary>
    public string? InstalledVersion { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static LoaderInstallResult Succeeded(string? executablePath, string installedVersion) => new()
    {
        Success = true,
        ExecutablePath = executablePath,
        InstalledVersion = installedVersion
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static LoaderInstallResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}
