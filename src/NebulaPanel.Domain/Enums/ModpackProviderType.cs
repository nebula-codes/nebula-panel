namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Types of modpack providers for Minecraft modpack distribution.
/// </summary>
public enum ModpackProviderType
{
    /// <summary>
    /// CurseForge - largest Minecraft modpack repository.
    /// </summary>
    CurseForge,

    /// <summary>
    /// Modrinth - modern, open-source mod repository.
    /// </summary>
    Modrinth,

    /// <summary>
    /// Feed The Beast - curated modpack platform.
    /// </summary>
    FTB,

    /// <summary>
    /// ATLauncher - community modpack launcher and repository.
    /// </summary>
    ATLauncher,

    /// <summary>
    /// Technic - legacy modpack platform (Tekkit, etc.).
    /// </summary>
    Technic
}
