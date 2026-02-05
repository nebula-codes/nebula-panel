namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Types of mod loaders for Minecraft modpacks.
/// </summary>
public enum ModLoaderType
{
    /// <summary>
    /// No specific mod loader (vanilla or unknown).
    /// </summary>
    None,

    /// <summary>
    /// Forge - traditional and most widely-used modding platform.
    /// </summary>
    Forge,

    /// <summary>
    /// NeoForge - community continuation of Forge for newer Minecraft versions.
    /// </summary>
    NeoForge,

    /// <summary>
    /// Fabric - lightweight, modern modding toolchain.
    /// </summary>
    Fabric,

    /// <summary>
    /// Quilt - Fabric fork with different governance and additional features.
    /// </summary>
    Quilt
}
