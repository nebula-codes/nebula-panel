namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents a semantic version with optional pre-release suffix.
/// </summary>
public record VersionInfo
{
    /// <summary>
    /// The base version string (e.g., "1.2.3").
    /// </summary>
    public string Version { get; init; } = "0.0.0";

    /// <summary>
    /// The pre-release suffix (e.g., "beta.1", "rc.2"), or null for stable releases.
    /// </summary>
    public string? PreRelease { get; init; }

    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public int Major => int.TryParse(Version.Split('.')[0], out var v) ? v : 0;

    /// <summary>
    /// Gets the minor version number.
    /// </summary>
    public int Minor => Version.Split('.').Length > 1 && int.TryParse(Version.Split('.')[1], out var v) ? v : 0;

    /// <summary>
    /// Gets the patch version number.
    /// </summary>
    public int Patch => Version.Split('.').Length > 2 && int.TryParse(Version.Split('.')[2], out var v) ? v : 0;

    /// <summary>
    /// Gets whether this is a pre-release version.
    /// </summary>
    public bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

    /// <summary>
    /// Gets the full version string including pre-release suffix if present.
    /// </summary>
    public string FullVersion => IsPreRelease ? $"{Version}-{PreRelease}" : Version;

    /// <summary>
    /// Parses a version string into a VersionInfo record.
    /// </summary>
    /// <param name="version">The version string (e.g., "v1.2.3", "1.2.3-beta.1").</param>
    /// <returns>A VersionInfo record representing the parsed version.</returns>
    public static VersionInfo Parse(string version)
    {
        var trimmed = version.TrimStart('v', 'V');
        var parts = trimmed.Split('-', 2);

        return new VersionInfo
        {
            Version = parts[0],
            PreRelease = parts.Length > 1 ? parts[1] : null
        };
    }

    /// <summary>
    /// Determines whether this version is newer than another version.
    /// </summary>
    /// <param name="other">The version to compare against.</param>
    /// <returns>True if this version is newer than the other version.</returns>
    public bool IsNewerThan(VersionInfo other)
    {
        if (Major != other.Major) return Major > other.Major;
        if (Minor != other.Minor) return Minor > other.Minor;
        if (Patch != other.Patch) return Patch > other.Patch;

        // Both stable = equal
        if (!IsPreRelease && !other.IsPreRelease) return false;

        // Stable > PreRelease (for same version number)
        if (!IsPreRelease && other.IsPreRelease) return true;
        if (IsPreRelease && !other.IsPreRelease) return false;

        // Compare pre-release strings lexicographically
        return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal) > 0;
    }
}
