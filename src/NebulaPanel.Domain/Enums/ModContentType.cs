namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Type of content to search for.
/// </summary>
public enum ModContentType
{
    /// <summary>
    /// Individual mods.
    /// </summary>
    Mod,

    /// <summary>
    /// Modpacks (collections of mods).
    /// </summary>
    Modpack,

    /// <summary>
    /// Resource packs / texture packs.
    /// </summary>
    ResourcePack,

    /// <summary>
    /// Shader packs.
    /// </summary>
    Shader,

    /// <summary>
    /// World saves / maps.
    /// </summary>
    World
}
