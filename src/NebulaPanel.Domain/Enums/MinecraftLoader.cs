namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Minecraft server software/loader types.
/// </summary>
public enum MinecraftLoader
{
    /// <summary>
    /// Official Mojang vanilla server.
    /// </summary>
    Vanilla,

    /// <summary>
    /// Paper - high performance fork of Spigot.
    /// </summary>
    Paper,

    /// <summary>
    /// Spigot - CraftBukkit fork with plugin support.
    /// </summary>
    Spigot,

    /// <summary>
    /// Fabric - lightweight modding toolchain.
    /// </summary>
    Fabric,

    /// <summary>
    /// Forge - traditional modding platform.
    /// </summary>
    Forge,

    /// <summary>
    /// NeoForge - continuation of Forge for 1.20.2+.
    /// </summary>
    NeoForge,

    /// <summary>
    /// Quilt - Fabric fork with different governance.
    /// </summary>
    Quilt,

    /// <summary>
    /// Purpur - Paper fork with extra features.
    /// </summary>
    Purpur,

    /// <summary>
    /// Velocity - modern proxy server.
    /// </summary>
    Velocity,

    /// <summary>
    /// BungeeCord - legacy proxy server.
    /// </summary>
    BungeeCord
}
