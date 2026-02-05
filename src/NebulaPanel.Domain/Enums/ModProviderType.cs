namespace NebulaPanel.Domain.Enums;

public enum ModProviderType
{
    Local,          // Manual file management only
    SteamWorkshop,  // Steam Workshop integration
    CurseForge,     // CurseForge API
    Modrinth,       // Modrinth API (Minecraft)
    Thunderstore,   // Thunderstore (Valheim, Lethal Company, etc.)
    SpigotMC,       // SpigotMC resources (Minecraft plugins)
    Hangar,         // PaperMC Hangar (Minecraft plugins)
    NexusMods,      // Nexus Mods (various games)
    Modtale         // Modtale (Hytale)
}
