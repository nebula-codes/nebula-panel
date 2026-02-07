using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Detects and validates Java installations for Minecraft servers.
/// </summary>
public partial class MinecraftJavaDetector(
    ILogger<MinecraftJavaDetector> logger,
    JavaRuntimeManager? javaRuntimeManager = null)
{
    private readonly ILogger<MinecraftJavaDetector> _logger = logger;
    private readonly JavaRuntimeManager? _javaRuntimeManager = javaRuntimeManager;

    /// <summary>
    /// Gets the minimum required Java version for a Minecraft version.
    /// </summary>
    /// <param name="minecraftVersion">Minecraft version string (e.g., "1.20.4").</param>
    /// <returns>Minimum Java version required.</returns>
    public static int GetRequiredJavaVersion(string minecraftVersion)
    {
        var version = ParseMinecraftVersion(minecraftVersion);
        if (version is null)
            return 8; // Default to Java 8 for unknown versions

        // MC 1.20.5+ requires Java 21
        if (version >= new Version(1, 20, 5))
            return 21;

        // MC 1.18+ requires Java 17
        if (version >= new Version(1, 18, 0))
            return 17;

        // MC 1.17+ requires Java 16
        if (version >= new Version(1, 17, 0))
            return 16;

        // Older versions work with Java 8
        return 8;
    }

    /// <summary>
    /// Detects the Java version from a java executable path.
    /// </summary>
    /// <param name="javaPath">Path to java executable, or null to use system default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detected Java version, or null if detection failed.</returns>
    public async Task<JavaVersionInfo?> DetectJavaVersionAsync(
        string? javaPath = null,
        CancellationToken cancellationToken = default)
    {
        var executable = javaPath ?? GetDefaultJavaPath();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Java outputs version info to stderr
            var output = await process.StandardError.ReadToEndAsync(cancellationToken)
                .ConfigureAwait(false);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return ParseJavaVersionOutput(output, executable);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect Java version from {Path}", executable);
            return null;
        }
    }

    /// <summary>
    /// Validates that a Java installation meets the minimum version requirements.
    /// </summary>
    /// <param name="javaPath">Path to java executable, or null to use system default.</param>
    /// <param name="requiredVersion">Minimum required Java version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result.</returns>
    public async Task<JavaValidationResult> ValidateJavaAsync(
        string? javaPath,
        int requiredVersion,
        CancellationToken cancellationToken = default)
    {
        var versionInfo = await DetectJavaVersionAsync(javaPath, cancellationToken)
            .ConfigureAwait(false);

        if (versionInfo is null)
        {
            return new JavaValidationResult
            {
                IsValid = false,
                Error = javaPath is null
                    ? "Java not found. Please install Java and ensure it's in your PATH."
                    : $"Could not execute Java at '{javaPath}'. Verify the path is correct."
            };
        }

        if (versionInfo.MajorVersion < requiredVersion)
        {
            return new JavaValidationResult
            {
                IsValid = false,
                DetectedVersion = versionInfo,
                Error = $"Java {requiredVersion} or higher is required, but Java {versionInfo.MajorVersion} was found."
            };
        }

        return new JavaValidationResult
        {
            IsValid = true,
            DetectedVersion = versionInfo
        };
    }

    /// <summary>
    /// Finds the Java executable to use, checking NativeConfig, JAVA_HOME, and PATH.
    /// </summary>
    /// <param name="configuredPath">Java path from configuration, if any.</param>
    /// <returns>Path to java executable.</returns>
    public string ResolveJavaPath(string? configuredPath)
    {
        // Use configured path if specified
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        // Check JAVA_HOME
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var javaExe = Path.Combine(javaHome, "bin", GetJavaExecutableName());
            if (File.Exists(javaExe))
                return javaExe;
        }

        // Fall back to system PATH
        return GetDefaultJavaPath();
    }

    /// <summary>
    /// Resolves a working Java executable, falling back to auto-downloading a portable JRE
    /// from Adoptium if no suitable system Java is found.
    /// </summary>
    /// <param name="configuredPath">Java path from configuration, if any.</param>
    /// <param name="requiredVersion">Minimum required Java major version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to a validated java executable.</returns>
    public async Task<string> ResolveJavaPathAsync(
        string? configuredPath,
        int requiredVersion,
        CancellationToken cancellationToken = default)
    {
        // 1. Try configured path
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var validation = await ValidateJavaAsync(configuredPath, requiredVersion, cancellationToken)
                .ConfigureAwait(false);
            if (validation.IsValid)
                return configuredPath;

            _logger.LogWarning("Configured Java path '{Path}' is not valid: {Error}", configuredPath, validation.Error);
        }

        // 2. Try JAVA_HOME
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var javaExe = Path.Combine(javaHome, "bin", GetJavaExecutableName());
            if (File.Exists(javaExe))
            {
                var validation = await ValidateJavaAsync(javaExe, requiredVersion, cancellationToken)
                    .ConfigureAwait(false);
                if (validation.IsValid)
                    return javaExe;

                _logger.LogDebug("JAVA_HOME Java at '{Path}' does not meet version requirement ({Required}+)",
                    javaExe, requiredVersion);
            }
        }

        // 3. Try system PATH
        var systemJava = GetDefaultJavaPath();
        var systemValidation = await ValidateJavaAsync(systemJava, requiredVersion, cancellationToken)
            .ConfigureAwait(false);
        if (systemValidation.IsValid)
            return systemJava;

        // 4. Auto-download from Adoptium
        if (_javaRuntimeManager is not null)
        {
            _logger.LogInformation(
                "No suitable Java {Version}+ found on system, auto-downloading portable JRE...",
                requiredVersion);
            return await _javaRuntimeManager.EnsureInstalledAsync(requiredVersion, cancellationToken)
                .ConfigureAwait(false);
        }

        // No runtime manager available — fall back to the best path we have (will fail at runtime)
        _logger.LogWarning(
            "No Java {Version}+ found and auto-download is not available. Java-dependent operations will fail.",
            requiredVersion);
        return ResolveJavaPath(configuredPath);
    }

    private static string GetDefaultJavaPath() =>
        OperatingSystem.IsWindows() ? "java.exe" : "java";

    private static string GetJavaExecutableName() =>
        OperatingSystem.IsWindows() ? "java.exe" : "java";

    private JavaVersionInfo? ParseJavaVersionOutput(string output, string path)
    {
        // Java version output formats:
        // java version "1.8.0_301"
        // openjdk version "11.0.12" 2021-07-20
        // openjdk version "17.0.1" 2021-10-19
        // openjdk version "21" 2023-09-19

        var match = JavaVersionRegex().Match(output);
        if (!match.Success)
        {
            _logger.LogWarning("Could not parse Java version from output: {Output}", output);
            return null;
        }

        var versionString = match.Groups[1].Value;
        var majorVersion = ParseMajorVersion(versionString);

        return new JavaVersionInfo
        {
            Path = path,
            VersionString = versionString,
            MajorVersion = majorVersion,
            FullOutput = output.Trim()
        };
    }

    private static int ParseMajorVersion(string versionString)
    {
        // Handle "1.8.0_301" (Java 8) vs "17.0.1" (Java 17+)
        var parts = versionString.Split('.');

        if (parts.Length > 0 && int.TryParse(parts[0], out var first))
        {
            // Old format: 1.8.0 means Java 8
            if (first == 1 && parts.Length > 1 && int.TryParse(parts[1], out var second))
                return second;

            // New format: 17.0.1 means Java 17
            return first;
        }

        return 0;
    }

    private static Version? ParseMinecraftVersion(string versionString)
    {
        // Handle versions like "1.20.4", "1.20", "1.17.1"
        // Also handle snapshots like "24w10a" by returning null
        var cleanVersion = versionString.Split('-')[0].Split('_')[0];
        var parts = cleanVersion.Split('.');

        try
        {
            var major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
            var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            var patch = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            return new Version(major, minor, patch);
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex(@"version\s+""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex JavaVersionRegex();
}

/// <summary>
/// Information about a detected Java installation.
/// </summary>
public record JavaVersionInfo
{
    /// <summary>
    /// Path to the Java executable.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Version string (e.g., "17.0.1", "1.8.0_301").
    /// </summary>
    public required string VersionString { get; init; }

    /// <summary>
    /// Major version number (e.g., 17, 21, 8).
    /// </summary>
    public required int MajorVersion { get; init; }

    /// <summary>
    /// Full output from java -version.
    /// </summary>
    public string? FullOutput { get; init; }
}

/// <summary>
/// Result of Java validation.
/// </summary>
public record JavaValidationResult
{
    /// <summary>
    /// Whether the Java installation is valid for the requirements.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Detected Java version info, if detection succeeded.
    /// </summary>
    public JavaVersionInfo? DetectedVersion { get; init; }

    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? Error { get; init; }
}
