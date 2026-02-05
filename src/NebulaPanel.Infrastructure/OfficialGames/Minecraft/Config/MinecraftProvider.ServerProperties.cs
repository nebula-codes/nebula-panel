using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Partial class containing the server.properties configuration schema.
/// </summary>
public partial class MinecraftProvider
{
    /// <summary>
    /// Adds the complete server.properties configuration schema with all vanilla fields.
    /// </summary>
    static partial void AddServerPropertiesSchema(Dictionary<string, ConfigurationSchema> schemas)
    {
        schemas["server.properties"] = new ConfigurationSchema
        {
            FileName = "server.properties",
            FileType = ConfigFileType.Properties,
            Fields =
            [
                // =====================
                // SERVER CATEGORY
                // =====================
                new ConfigField
                {
                    Key = "motd",
                    DisplayName = "Server Message (MOTD)",
                    Description = "Message displayed in the server list. Supports color codes with section symbol.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "A Minecraft Server",
                    Validation = new ValidationRule { MaxLength = 59 },
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "max-players",
                    DisplayName = "Max Players",
                    Description = "Maximum number of players that can join the server simultaneously.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 20,
                    Validation = new ValidationRule { MinValue = 0, MaxValue = 2147483647 },
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "gamemode",
                    DisplayName = "Default Game Mode",
                    Description = "Default game mode for new players joining the server.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "survival",
                    Options =
                    [
                        new SelectOption { Value = "survival", Label = "Survival" },
                        new SelectOption { Value = "creative", Label = "Creative" },
                        new SelectOption { Value = "adventure", Label = "Adventure" },
                        new SelectOption { Value = "spectator", Label = "Spectator" }
                    ],
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "force-gamemode",
                    DisplayName = "Force Game Mode",
                    Description = "Force players to join in the default game mode instead of their previous mode.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "difficulty",
                    DisplayName = "Difficulty",
                    Description = "Server difficulty level affecting mob damage and hunger.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "easy",
                    Options =
                    [
                        new SelectOption { Value = "peaceful", Label = "Peaceful" },
                        new SelectOption { Value = "easy", Label = "Easy" },
                        new SelectOption { Value = "normal", Label = "Normal" },
                        new SelectOption { Value = "hard", Label = "Hard" }
                    ],
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "hardcore",
                    DisplayName = "Hardcore Mode",
                    Description = "Enable hardcore mode (players are banned on death, difficulty locked to hard).",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "pvp",
                    DisplayName = "PvP Combat",
                    Description = "Allow players to deal damage to each other.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "enable-status",
                    DisplayName = "Enable Status",
                    Description = "Allow the server to appear in the server list.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "hide-online-players",
                    DisplayName = "Hide Online Players",
                    Description = "Hide the player list from the server status response.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "player-idle-timeout",
                    DisplayName = "Player Idle Timeout",
                    Description = "Minutes before idle players are kicked (0 = disabled).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 0,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "op-permission-level",
                    DisplayName = "Default OP Level",
                    Description = "Default permission level for operators (1-4).",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "4",
                    Options =
                    [
                        new SelectOption { Value = "1", Label = "Level 1 - Bypass spawn protection" },
                        new SelectOption { Value = "2", Label = "Level 2 - Use cheat commands" },
                        new SelectOption { Value = "3", Label = "Level 3 - Manage players" },
                        new SelectOption { Value = "4", Label = "Level 4 - Full access" }
                    ],
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "function-permission-level",
                    DisplayName = "Function Permission Level",
                    Description = "Permission level required to run functions.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "1", Label = "Level 1" },
                        new SelectOption { Value = "2", Label = "Level 2" },
                        new SelectOption { Value = "3", Label = "Level 3" },
                        new SelectOption { Value = "4", Label = "Level 4" }
                    ],
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "resource-pack",
                    DisplayName = "Resource Pack URL",
                    Description = "URL to a resource pack for clients to download.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "resource-pack-sha1",
                    DisplayName = "Resource Pack SHA1",
                    Description = "SHA1 hash of the resource pack for verification.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Validation = new ValidationRule { Pattern = "^([a-fA-F0-9]{40})?$", ErrorMessage = "Must be a valid 40-character SHA1 hash or empty" },
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "require-resource-pack",
                    DisplayName = "Require Resource Pack",
                    Description = "Kick players who decline the resource pack.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Server"
                },
                new ConfigField
                {
                    Key = "resource-pack-prompt",
                    DisplayName = "Resource Pack Prompt",
                    Description = "Custom message shown when prompting for resource pack.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Server"
                },

                // =====================
                // WORLD CATEGORY
                // =====================
                new ConfigField
                {
                    Key = "level-name",
                    DisplayName = "World Name",
                    Description = "Name of the world folder.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "world",
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "level-seed",
                    DisplayName = "World Seed",
                    Description = "Seed for world generation (leave empty for random).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "level-type",
                    DisplayName = "World Type",
                    Description = "Type of world generation.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "minecraft:normal",
                    Options =
                    [
                        new SelectOption { Value = "minecraft:normal", Label = "Normal" },
                        new SelectOption { Value = "minecraft:flat", Label = "Superflat" },
                        new SelectOption { Value = "minecraft:large_biomes", Label = "Large Biomes" },
                        new SelectOption { Value = "minecraft:amplified", Label = "Amplified" },
                        new SelectOption { Value = "minecraft:single_biome_surface", Label = "Single Biome" }
                    ],
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "generator-settings",
                    DisplayName = "Generator Settings",
                    Description = "JSON settings for superflat/custom world generation.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "{}",
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "generate-structures",
                    DisplayName = "Generate Structures",
                    Description = "Generate villages, temples, and other structures.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "spawn-protection",
                    DisplayName = "Spawn Protection Radius",
                    Description = "Radius around spawn where non-ops cannot build (0 = disabled).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 16,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "spawn-animals",
                    DisplayName = "Spawn Animals",
                    Description = "Allow passive mobs to spawn naturally.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "spawn-monsters",
                    DisplayName = "Spawn Monsters",
                    Description = "Allow hostile mobs to spawn naturally.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "spawn-npcs",
                    DisplayName = "Spawn NPCs",
                    Description = "Allow villagers and other NPCs to spawn.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "allow-nether",
                    DisplayName = "Allow Nether",
                    Description = "Allow players to travel to the Nether dimension.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "max-world-size",
                    DisplayName = "Max World Size",
                    Description = "Maximum world border radius in blocks.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 29999984,
                    Validation = new ValidationRule { MinValue = 1, MaxValue = 29999984 },
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "initial-enabled-packs",
                    DisplayName = "Initial Enabled Packs",
                    Description = "Data packs to enable by default.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "vanilla",
                    Category = "World"
                },
                new ConfigField
                {
                    Key = "initial-disabled-packs",
                    DisplayName = "Initial Disabled Packs",
                    Description = "Data packs to disable by default.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "World"
                },

                // =====================
                // NETWORK CATEGORY
                // =====================
                new ConfigField
                {
                    Key = "server-ip",
                    DisplayName = "Server IP",
                    Description = "IP address to bind to (leave empty for all interfaces).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Network"
                },
                new ConfigField
                {
                    Key = "server-port",
                    DisplayName = "Server Port",
                    Description = "Port the server listens on for client connections.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 25565,
                    Category = "Network"
                },
                new ConfigField
                {
                    Key = "enable-query",
                    DisplayName = "Enable Query",
                    Description = "Enable GameSpy4 query protocol for server information.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Network"
                },
                new ConfigField
                {
                    Key = "query.port",
                    DisplayName = "Query Port",
                    Description = "Port for query protocol.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 25565,
                    Category = "Network"
                },
                new ConfigField
                {
                    Key = "enable-rcon",
                    DisplayName = "Enable RCON",
                    Description = "Enable remote console access for server management.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Network"
                },
                new ConfigField
                {
                    Key = "rcon.port",
                    DisplayName = "RCON Port",
                    Description = "Port for RCON connections.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 25575,
                    Category = "Network"
                },
                new ConfigField
                {
                    Key = "rcon.password",
                    DisplayName = "RCON Password",
                    Description = "Password for RCON authentication (required if RCON enabled).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Network"
                },
                new ConfigField
                {
                    Key = "broadcast-rcon-to-ops",
                    DisplayName = "Broadcast RCON to OPs",
                    Description = "Show RCON command output to online operators.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Network"
                },
                new ConfigField
                {
                    Key = "broadcast-console-to-ops",
                    DisplayName = "Broadcast Console to OPs",
                    Description = "Show console command output to online operators.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Network"
                },

                // =====================
                // PERFORMANCE CATEGORY
                // =====================
                new ConfigField
                {
                    Key = "view-distance",
                    DisplayName = "View Distance",
                    Description = "Render distance in chunks (3-32). Lower values improve performance.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 10,
                    Validation = new ValidationRule { MinValue = 3, MaxValue = 32 },
                    Category = "Performance"
                },
                new ConfigField
                {
                    Key = "simulation-distance",
                    DisplayName = "Simulation Distance",
                    Description = "Distance in chunks where entities and blocks are ticked.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 10,
                    Validation = new ValidationRule { MinValue = 3, MaxValue = 32 },
                    Category = "Performance"
                },
                new ConfigField
                {
                    Key = "max-tick-time",
                    DisplayName = "Max Tick Time",
                    Description = "Maximum milliseconds per tick before server watchdog intervenes (-1 = disable).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 60000,
                    Validation = new ValidationRule { MinValue = -1 },
                    Category = "Performance"
                },
                new ConfigField
                {
                    Key = "network-compression-threshold",
                    DisplayName = "Network Compression Threshold",
                    Description = "Minimum packet size for compression (-1 = disable, 0 = compress all).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 256,
                    Validation = new ValidationRule { MinValue = -1, MaxValue = 65535 },
                    Category = "Performance"
                },
                new ConfigField
                {
                    Key = "rate-limit",
                    DisplayName = "Rate Limit",
                    Description = "Maximum packets per second before kicking (0 = disabled).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 0,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Performance"
                },
                new ConfigField
                {
                    Key = "max-chained-neighbor-updates",
                    DisplayName = "Max Chained Neighbor Updates",
                    Description = "Limit chain reaction block updates (-1 = unlimited).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1000000,
                    Validation = new ValidationRule { MinValue = -1 },
                    Category = "Performance"
                },
                new ConfigField
                {
                    Key = "entity-broadcast-range-percentage",
                    DisplayName = "Entity Broadcast Range %",
                    Description = "Percentage of default entity tracking range (10-1000).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 100,
                    Validation = new ValidationRule { MinValue = 10, MaxValue = 1000 },
                    Category = "Performance"
                },
                new ConfigField
                {
                    Key = "sync-chunk-writes",
                    DisplayName = "Sync Chunk Writes",
                    Description = "Write chunks synchronously to disk for data integrity.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Performance"
                },
                new ConfigField
                {
                    Key = "region-file-compression",
                    DisplayName = "Region File Compression",
                    Description = "Compression algorithm for region files.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "deflate",
                    Options =
                    [
                        new SelectOption { Value = "deflate", Label = "Deflate (Default)" },
                        new SelectOption { Value = "lz4", Label = "LZ4 (Faster)" },
                        new SelectOption { Value = "none", Label = "None" }
                    ],
                    Category = "Performance"
                },

                // =====================
                // SECURITY CATEGORY
                // =====================
                new ConfigField
                {
                    Key = "online-mode",
                    DisplayName = "Online Mode",
                    Description = "Authenticate players with Mojang servers. Disable only for offline/LAN.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Security"
                },
                new ConfigField
                {
                    Key = "white-list",
                    DisplayName = "Enable Whitelist",
                    Description = "Only allow whitelisted players to join.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Security"
                },
                new ConfigField
                {
                    Key = "enforce-whitelist",
                    DisplayName = "Enforce Whitelist",
                    Description = "Kick non-whitelisted players when whitelist is reloaded.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Security"
                },
                new ConfigField
                {
                    Key = "prevent-proxy-connections",
                    DisplayName = "Prevent Proxy Connections",
                    Description = "Block connections through known VPNs/proxies.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Security"
                },
                new ConfigField
                {
                    Key = "enforce-secure-profile",
                    DisplayName = "Enforce Secure Profile",
                    Description = "Require players to have a Mojang-signed public key.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Security"
                },

                // =====================
                // GAMEPLAY CATEGORY
                // =====================
                new ConfigField
                {
                    Key = "enable-command-block",
                    DisplayName = "Enable Command Blocks",
                    Description = "Allow command blocks to execute commands.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Gameplay"
                },
                new ConfigField
                {
                    Key = "allow-flight",
                    DisplayName = "Allow Flight",
                    Description = "Allow players to use flight mods without being kicked.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Gameplay"
                },

                // =====================
                // ADVANCED CATEGORY
                // =====================
                new ConfigField
                {
                    Key = "enable-jmx-monitoring",
                    DisplayName = "Enable JMX Monitoring",
                    Description = "Enable Java Management Extensions for monitoring.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Advanced"
                },
                new ConfigField
                {
                    Key = "use-native-transport",
                    DisplayName = "Use Native Transport",
                    Description = "Use optimized native network transport on Linux.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Advanced"
                },
                new ConfigField
                {
                    Key = "text-filtering-config",
                    DisplayName = "Text Filtering Config",
                    Description = "Path to text filtering configuration file.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Advanced"
                },
                new ConfigField
                {
                    Key = "previews-chat",
                    DisplayName = "Previews Chat",
                    Description = "Enable chat preview feature.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Advanced"
                },
                new ConfigField
                {
                    Key = "log-ips",
                    DisplayName = "Log IPs",
                    Description = "Log player IP addresses to server log.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Advanced"
                },
                new ConfigField
                {
                    Key = "debug",
                    DisplayName = "Debug Mode",
                    Description = "Enable debug output (development only).",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Advanced"
                },
                new ConfigField
                {
                    Key = "pause-when-empty-seconds",
                    DisplayName = "Pause When Empty (Seconds)",
                    Description = "Seconds to wait before pausing server when empty (0 = disabled).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 60,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Advanced"
                },
                new ConfigField
                {
                    Key = "bug-report-link",
                    DisplayName = "Bug Report Link",
                    Description = "Custom URL for bug reports shown in crash screens.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Advanced"
                },
                new ConfigField
                {
                    Key = "accept-transfers",
                    DisplayName = "Accept Transfers",
                    Description = "Accept player transfers from other servers (1.20.5+).",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Advanced"
                }
            ]
        };
    }
}
