using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Partial class containing Fabric-specific configuration schemas.
/// Note: Most Fabric mod configs are stored in the config/ directory
/// and are mod-specific. This file contains the Fabric launcher configuration.
/// </summary>
public partial class MinecraftProvider
{
    /// <summary>
    /// Adds Fabric-specific configuration schemas.
    /// </summary>
    static partial void AddFabricConfigSchema(Dictionary<string, ConfigurationSchema> schemas)
    {
        // =====================
        // FABRIC SERVER LAUNCHER PROPERTIES
        // =====================
        // This file is used when running via fabric-server-launcher.jar
        schemas["fabric-server-launcher.properties"] = new ConfigurationSchema
        {
            FileName = "fabric-server-launcher.properties",
            FileType = ConfigFileType.Properties,
            Fields =
            [
                new ConfigField
                {
                    Key = "serverJar",
                    DisplayName = "Server JAR",
                    Description = "Path to the vanilla server JAR file that Fabric will mod.",
                    Type = ConfigFieldType.Path,
                    DefaultValue = "server.jar",
                    Category = "Launcher"
                },
                new ConfigField
                {
                    Key = "fabricLoaderVersion",
                    DisplayName = "Fabric Loader Version",
                    Description = "Version of Fabric Loader to use (leave empty for latest).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Launcher"
                }
            ]
        };

        // =====================
        // DOCKER CONFIGURATION FOR LOADERS
        // =====================
        // This schema documents Docker environment variables for the itzg/minecraft-server image
        schemas["_docker_config"] = new ConfigurationSchema
        {
            FileName = "_docker_config",
            FileType = ConfigFileType.Custom,
            Fields =
            [
                // =====================
                // SERVER TYPE SELECTION
                // =====================
                new ConfigField
                {
                    Key = "TYPE",
                    DisplayName = "Server Type",
                    Description = "Type of Minecraft server to run in Docker container.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "VANILLA",
                    Options =
                    [
                        new SelectOption { Value = "VANILLA", Label = "Vanilla" },
                        new SelectOption { Value = "PAPER", Label = "Paper" },
                        new SelectOption { Value = "SPIGOT", Label = "Spigot" },
                        new SelectOption { Value = "BUKKIT", Label = "Bukkit" },
                        new SelectOption { Value = "FABRIC", Label = "Fabric" },
                        new SelectOption { Value = "FORGE", Label = "Forge" },
                        new SelectOption { Value = "NEOFORGE", Label = "NeoForge" },
                        new SelectOption { Value = "PURPUR", Label = "Purpur" },
                        new SelectOption { Value = "QUILT", Label = "Quilt" },
                        new SelectOption { Value = "FOLIA", Label = "Folia" },
                        new SelectOption { Value = "PUFFERFISH", Label = "Pufferfish" }
                    ],
                    Category = "Docker Server Type"
                },

                // =====================
                // VERSION CONFIGURATION
                // =====================
                new ConfigField
                {
                    Key = "VERSION",
                    DisplayName = "Minecraft Version",
                    Description = "Minecraft version to run (e.g., '1.20.4', 'LATEST', 'SNAPSHOT').",
                    Type = ConfigFieldType.String,
                    DefaultValue = "LATEST",
                    Category = "Docker Version"
                },
                new ConfigField
                {
                    Key = "PAPER_BUILD",
                    DisplayName = "Paper Build",
                    Description = "Specific Paper build number (Paper only).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Version"
                },
                new ConfigField
                {
                    Key = "FABRIC_LOADER_VERSION",
                    DisplayName = "Fabric Loader Version",
                    Description = "Specific Fabric Loader version (Fabric only).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Version"
                },
                new ConfigField
                {
                    Key = "FORGE_VERSION",
                    DisplayName = "Forge Version",
                    Description = "Specific Forge version (Forge only).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Version"
                },
                new ConfigField
                {
                    Key = "QUILT_LOADER_VERSION",
                    DisplayName = "Quilt Loader Version",
                    Description = "Specific Quilt Loader version (Quilt only).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Version"
                },

                // =====================
                // MEMORY CONFIGURATION
                // =====================
                new ConfigField
                {
                    Key = "MEMORY",
                    DisplayName = "Memory",
                    Description = "Memory allocation for the server (e.g., '4G', '2048M').",
                    Type = ConfigFieldType.String,
                    DefaultValue = "4G",
                    Category = "Docker Memory"
                },
                new ConfigField
                {
                    Key = "INIT_MEMORY",
                    DisplayName = "Initial Memory",
                    Description = "Initial memory allocation (leave empty to match MEMORY).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Memory"
                },
                new ConfigField
                {
                    Key = "MAX_MEMORY",
                    DisplayName = "Maximum Memory",
                    Description = "Maximum memory allocation (leave empty to match MEMORY).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Memory"
                },
                new ConfigField
                {
                    Key = "USE_AIKAR_FLAGS",
                    DisplayName = "Use Aikar Flags",
                    Description = "Use Aikar's optimized JVM flags for Minecraft.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Docker Memory"
                },

                // =====================
                // SERVER SETTINGS
                // =====================
                new ConfigField
                {
                    Key = "EULA",
                    DisplayName = "Accept EULA",
                    Description = "Accept Minecraft EULA (required to run).",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Docker Server"
                },
                new ConfigField
                {
                    Key = "SERVER_NAME",
                    DisplayName = "Server Name",
                    Description = "Server name for logging.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Server"
                },
                new ConfigField
                {
                    Key = "SERVER_PORT",
                    DisplayName = "Server Port",
                    Description = "Port the server listens on inside container.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 25565,
                    Category = "Docker Server"
                },
                new ConfigField
                {
                    Key = "MOTD",
                    DisplayName = "Server MOTD",
                    Description = "Server message of the day.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "A Minecraft Server",
                    Category = "Docker Server"
                },
                new ConfigField
                {
                    Key = "MAX_PLAYERS",
                    DisplayName = "Max Players",
                    Description = "Maximum number of players.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 20,
                    Category = "Docker Server"
                },
                new ConfigField
                {
                    Key = "DIFFICULTY",
                    DisplayName = "Difficulty",
                    Description = "Server difficulty.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "easy",
                    Options =
                    [
                        new SelectOption { Value = "peaceful", Label = "Peaceful" },
                        new SelectOption { Value = "easy", Label = "Easy" },
                        new SelectOption { Value = "normal", Label = "Normal" },
                        new SelectOption { Value = "hard", Label = "Hard" }
                    ],
                    Category = "Docker Server"
                },
                new ConfigField
                {
                    Key = "MODE",
                    DisplayName = "Game Mode",
                    Description = "Default game mode.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "survival",
                    Options =
                    [
                        new SelectOption { Value = "survival", Label = "Survival" },
                        new SelectOption { Value = "creative", Label = "Creative" },
                        new SelectOption { Value = "adventure", Label = "Adventure" },
                        new SelectOption { Value = "spectator", Label = "Spectator" }
                    ],
                    Category = "Docker Server"
                },
                new ConfigField
                {
                    Key = "HARDCORE",
                    DisplayName = "Hardcore Mode",
                    Description = "Enable hardcore mode.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Docker Server"
                },
                new ConfigField
                {
                    Key = "PVP",
                    DisplayName = "PvP",
                    Description = "Enable player vs player combat.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Docker Server"
                },
                new ConfigField
                {
                    Key = "ONLINE_MODE",
                    DisplayName = "Online Mode",
                    Description = "Authenticate with Mojang.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Docker Server"
                },

                // =====================
                // WORLD SETTINGS
                // =====================
                new ConfigField
                {
                    Key = "LEVEL",
                    DisplayName = "World Name",
                    Description = "Name of the world folder.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "world",
                    Category = "Docker World"
                },
                new ConfigField
                {
                    Key = "LEVEL_TYPE",
                    DisplayName = "World Type",
                    Description = "Type of world generation.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "minecraft:normal",
                    Options =
                    [
                        new SelectOption { Value = "minecraft:normal", Label = "Normal" },
                        new SelectOption { Value = "minecraft:flat", Label = "Superflat" },
                        new SelectOption { Value = "minecraft:large_biomes", Label = "Large Biomes" },
                        new SelectOption { Value = "minecraft:amplified", Label = "Amplified" }
                    ],
                    Category = "Docker World"
                },
                new ConfigField
                {
                    Key = "SEED",
                    DisplayName = "World Seed",
                    Description = "Seed for world generation.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker World"
                },
                new ConfigField
                {
                    Key = "SPAWN_PROTECTION",
                    DisplayName = "Spawn Protection",
                    Description = "Radius of spawn protection.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 16,
                    Category = "Docker World"
                },
                new ConfigField
                {
                    Key = "VIEW_DISTANCE",
                    DisplayName = "View Distance",
                    Description = "Server view distance in chunks.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 10,
                    Category = "Docker World"
                },
                new ConfigField
                {
                    Key = "SIMULATION_DISTANCE",
                    DisplayName = "Simulation Distance",
                    Description = "Simulation distance in chunks.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 10,
                    Category = "Docker World"
                },

                // =====================
                // RCON SETTINGS
                // =====================
                new ConfigField
                {
                    Key = "ENABLE_RCON",
                    DisplayName = "Enable RCON",
                    Description = "Enable remote console access.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Docker RCON"
                },
                new ConfigField
                {
                    Key = "RCON_PORT",
                    DisplayName = "RCON Port",
                    Description = "Port for RCON connections.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 25575,
                    Category = "Docker RCON"
                },
                new ConfigField
                {
                    Key = "RCON_PASSWORD",
                    DisplayName = "RCON Password",
                    Description = "Password for RCON authentication.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker RCON"
                },

                // =====================
                // MODS/PLUGINS
                // =====================
                new ConfigField
                {
                    Key = "MODS",
                    DisplayName = "Mods",
                    Description = "Comma-separated list of mod URLs to download.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Mods"
                },
                new ConfigField
                {
                    Key = "MODRINTH_PROJECTS",
                    DisplayName = "Modrinth Projects",
                    Description = "Comma-separated list of Modrinth project slugs/IDs.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Mods"
                },
                new ConfigField
                {
                    Key = "CF_API_KEY",
                    DisplayName = "CurseForge API Key",
                    Description = "CurseForge API key for downloading mods.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Mods"
                },
                new ConfigField
                {
                    Key = "CURSEFORGE_FILES",
                    DisplayName = "CurseForge Files",
                    Description = "Comma-separated list of CurseForge file IDs.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Mods"
                },

                // =====================
                // WHITELIST
                // =====================
                new ConfigField
                {
                    Key = "WHITELIST",
                    DisplayName = "Whitelist",
                    Description = "Comma-separated list of players to whitelist.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Whitelist"
                },
                new ConfigField
                {
                    Key = "ENABLE_WHITELIST",
                    DisplayName = "Enable Whitelist",
                    Description = "Enable whitelist enforcement.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Docker Whitelist"
                },
                new ConfigField
                {
                    Key = "OPS",
                    DisplayName = "Operators",
                    Description = "Comma-separated list of players to make operators.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Docker Whitelist"
                }
            ]
        };
    }
}
