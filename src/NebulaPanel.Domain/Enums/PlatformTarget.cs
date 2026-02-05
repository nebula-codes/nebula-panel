namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Represents the target platform for a release asset.
/// </summary>
public enum PlatformTarget
{
    /// <summary>
    /// Linux x64 (AMD64) platform.
    /// </summary>
    LinuxX64,

    /// <summary>
    /// Linux ARM64 (AArch64) platform.
    /// </summary>
    LinuxArm64,

    /// <summary>
    /// Windows x64 (AMD64) platform.
    /// </summary>
    WindowsX64,

    /// <summary>
    /// Docker container image.
    /// </summary>
    Docker
}
