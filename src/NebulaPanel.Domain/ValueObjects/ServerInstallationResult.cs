namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Result of a server installation or update operation.
/// </summary>
public record ServerInstallationResult
{
    /// <summary>
    /// Whether the installation/update succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// The version that was installed.
    /// </summary>
    public string? InstalledVersion { get; init; }

    /// <summary>
    /// Path where the server was installed.
    /// </summary>
    public string? InstalledPath { get; init; }

    /// <summary>
    /// Creates a successful installation result.
    /// </summary>
    public static ServerInstallationResult Succeeded(string version, string path) => new()
    {
        Success = true,
        InstalledVersion = version,
        InstalledPath = path
    };

    /// <summary>
    /// Creates a failed installation result.
    /// </summary>
    public static ServerInstallationResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}
