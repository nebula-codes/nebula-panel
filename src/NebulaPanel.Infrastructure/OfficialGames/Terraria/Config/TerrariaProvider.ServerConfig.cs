using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Terraria;

/// <summary>
/// Partial class containing the serverconfig.txt configuration schema for tModLoader.
/// </summary>
public partial class TerrariaProvider
{
    /// <summary>
    /// Adds the complete serverconfig.txt configuration schema with all Terraria/tModLoader fields.
    /// </summary>
    static partial void AddServerConfigSchema(Dictionary<string, ConfigurationSchema> schemas)
    {
        schemas["serverconfig.txt"] = new ConfigurationSchema
        {
            FileName = "serverconfig.txt",
            DisplayName = "Server Configuration",
            Description = "Main server configuration for Terraria/tModLoader",
            FileType = ConfigFileType.Properties,
            CreateIfMissing = true,
            Sections =
            [
                new ConfigSection
                {
                    Key = "World",
                    DisplayName = "World Settings",
                    Description = "World generation and loading options",
                    Icon = "globe",
                    Order = 1,
                    DefaultExpanded = true
                },
                new ConfigSection
                {
                    Key = "Server",
                    DisplayName = "Server Settings",
                    Description = "Network and player settings",
                    Icon = "server",
                    Order = 2,
                    DefaultExpanded = true
                },
                new ConfigSection
                {
                    Key = "Journey",
                    DisplayName = "Journey Mode",
                    Description = "Journey mode power permissions (0=Locked, 1=Host Only, 2=All Players)",
                    Icon = "sparkles",
                    Order = 3,
                    DefaultExpanded = false
                },
                new ConfigSection
                {
                    Key = "Advanced",
                    DisplayName = "Advanced Settings",
                    Description = "Advanced server configuration",
                    Icon = "cog",
                    Order = 4,
                    DefaultExpanded = false
                }
            ],
            Fields =
            [
                // =====================
                // WORLD CATEGORY
                // =====================
                new ConfigField
                {
                    Key = "world",
                    DisplayName = "World File Path",
                    Description = "Full path to the world file. Leave empty to use autocreate.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "World",
                    Order = 1,
                    Placeholder = "/path/to/world.wld"
                },
                new ConfigField
                {
                    Key = "autocreate",
                    DisplayName = "Auto-Create World Size",
                    Description = "Automatically create a new world of this size if no world file exists.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "3",
                    Options =
                    [
                        new SelectOption { Value = "1", Label = "Small (4200x1200)" },
                        new SelectOption { Value = "2", Label = "Medium (6400x1800)" },
                        new SelectOption { Value = "3", Label = "Large (8400x2400)" }
                    ],
                    Category = "World",
                    Order = 2
                },
                new ConfigField
                {
                    Key = "worldname",
                    DisplayName = "World Name",
                    Description = "Name of the world (used for auto-created worlds).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "World",
                    Category = "World",
                    Order = 3,
                    Validation = new ValidationRule { MaxLength = 64 }
                },
                new ConfigField
                {
                    Key = "difficulty",
                    DisplayName = "Difficulty",
                    Description = "World difficulty setting.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "0",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Classic (Normal)" },
                        new SelectOption { Value = "1", Label = "Expert" },
                        new SelectOption { Value = "2", Label = "Master" },
                        new SelectOption { Value = "3", Label = "Journey" }
                    ],
                    Category = "World",
                    Order = 4
                },
                new ConfigField
                {
                    Key = "seed",
                    DisplayName = "World Seed",
                    Description = "Seed for world generation. Leave empty for random.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "World",
                    Order = 5,
                    Placeholder = "Leave empty for random"
                },
                new ConfigField
                {
                    Key = "worldpath",
                    DisplayName = "Worlds Directory",
                    Description = "Directory where world files are stored. Keep within the server folder for file browser access.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "Worlds",
                    Category = "World",
                    Order = 6,
                    Advanced = true,
                    Placeholder = "Worlds (relative to server folder)"
                },

                // =====================
                // SERVER CATEGORY
                // =====================
                new ConfigField
                {
                    Key = "maxplayers",
                    DisplayName = "Max Players",
                    Description = "Maximum number of players that can join the server.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 8,
                    SliderOptions = new SliderOptions { Min = 1, Max = 255, Step = 1 },
                    Category = "Server",
                    Order = 1
                },
                new ConfigField
                {
                    Key = "port",
                    DisplayName = "Server Port",
                    Description = "Port the server listens on for client connections.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 7777,
                    Category = "Server",
                    Order = 2
                },
                new ConfigField
                {
                    Key = "password",
                    DisplayName = "Server Password",
                    Description = "Password required to join the server. Leave empty for no password.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Server",
                    Order = 3,
                    Placeholder = "Leave empty for no password"
                },
                new ConfigField
                {
                    Key = "motd",
                    DisplayName = "Message of the Day",
                    Description = "Message displayed when players join the server.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Server",
                    Order = 4,
                    Placeholder = "Welcome to the server!",
                    Validation = new ValidationRule { MaxLength = 256 }
                },
                new ConfigField
                {
                    Key = "secure",
                    DisplayName = "Cheat Protection",
                    Description = "Enable server-side cheat protection.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Server",
                    Order = 5
                },
                new ConfigField
                {
                    Key = "npcstream",
                    DisplayName = "NPC Streaming",
                    Description = "NPC update rate. Higher values increase bandwidth but improve NPC behavior.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 60,
                    SliderOptions = new SliderOptions { Min = 0, Max = 200, Step = 5 },
                    Category = "Server",
                    Order = 6,
                    Advanced = true
                },
                new ConfigField
                {
                    Key = "priority",
                    DisplayName = "Process Priority",
                    Description = "Server process priority level.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "1",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Realtime" },
                        new SelectOption { Value = "1", Label = "High" },
                        new SelectOption { Value = "2", Label = "Above Normal" },
                        new SelectOption { Value = "3", Label = "Normal" },
                        new SelectOption { Value = "4", Label = "Below Normal" },
                        new SelectOption { Value = "5", Label = "Idle" }
                    ],
                    Category = "Server",
                    Order = 7,
                    Advanced = true
                },
                new ConfigField
                {
                    Key = "upnp",
                    DisplayName = "UPnP Port Forwarding",
                    Description = "Automatically configure port forwarding via UPnP.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Server",
                    Order = 8,
                    Advanced = true
                },
                new ConfigField
                {
                    Key = "steam",
                    DisplayName = "Steam Integration",
                    Description = "Enable Steam integration and friends joining.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Server",
                    Order = 9
                },
                new ConfigField
                {
                    Key = "lobby",
                    DisplayName = "Steam Lobby Type",
                    Description = "Steam lobby visibility setting.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "friends",
                    Options =
                    [
                        new SelectOption { Value = "friends", Label = "Friends Only" },
                        new SelectOption { Value = "private", Label = "Private (Invite Only)" }
                    ],
                    Category = "Server",
                    Order = 10
                },
                new ConfigField
                {
                    Key = "language",
                    DisplayName = "Server Language",
                    Description = "Language for server messages.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "en-US",
                    Options =
                    [
                        new SelectOption { Value = "en-US", Label = "English" },
                        new SelectOption { Value = "de-DE", Label = "German" },
                        new SelectOption { Value = "it-IT", Label = "Italian" },
                        new SelectOption { Value = "fr-FR", Label = "French" },
                        new SelectOption { Value = "es-ES", Label = "Spanish" },
                        new SelectOption { Value = "ru-RU", Label = "Russian" },
                        new SelectOption { Value = "zh-Hans", Label = "Chinese (Simplified)" },
                        new SelectOption { Value = "pt-BR", Label = "Portuguese (Brazil)" },
                        new SelectOption { Value = "pl-PL", Label = "Polish" }
                    ],
                    Category = "Server",
                    Order = 11,
                    Advanced = true
                },

                // =====================
                // JOURNEY MODE CATEGORY
                // Permission values: 0=Locked, 1=Host Only, 2=All Players
                // =====================
                new ConfigField
                {
                    Key = "journeypermission_time_setfrozen",
                    DisplayName = "Freeze Time",
                    Description = "Permission to freeze/unfreeze time.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Locked" },
                        new SelectOption { Value = "1", Label = "Host Only" },
                        new SelectOption { Value = "2", Label = "All Players" }
                    ],
                    Category = "Journey",
                    Order = 1
                },
                new ConfigField
                {
                    Key = "journeypermission_time_setdawn",
                    DisplayName = "Set Time (Dawn/Noon/Dusk/Midnight)",
                    Description = "Permission to change time of day.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Locked" },
                        new SelectOption { Value = "1", Label = "Host Only" },
                        new SelectOption { Value = "2", Label = "All Players" }
                    ],
                    Category = "Journey",
                    Order = 2
                },
                new ConfigField
                {
                    Key = "journeypermission_time_setspeed",
                    DisplayName = "Time Speed",
                    Description = "Permission to change time flow speed.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Locked" },
                        new SelectOption { Value = "1", Label = "Host Only" },
                        new SelectOption { Value = "2", Label = "All Players" }
                    ],
                    Category = "Journey",
                    Order = 3
                },
                new ConfigField
                {
                    Key = "journeypermission_godmode",
                    DisplayName = "God Mode",
                    Description = "Permission to enable invincibility.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Locked" },
                        new SelectOption { Value = "1", Label = "Host Only" },
                        new SelectOption { Value = "2", Label = "All Players" }
                    ],
                    Category = "Journey",
                    Order = 4
                },
                new ConfigField
                {
                    Key = "journeypermission_wind_setstrength",
                    DisplayName = "Wind Control",
                    Description = "Permission to control wind strength.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Locked" },
                        new SelectOption { Value = "1", Label = "Host Only" },
                        new SelectOption { Value = "2", Label = "All Players" }
                    ],
                    Category = "Journey",
                    Order = 5
                },
                new ConfigField
                {
                    Key = "journeypermission_rain_setstrength",
                    DisplayName = "Rain Control",
                    Description = "Permission to control rain intensity.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Locked" },
                        new SelectOption { Value = "1", Label = "Host Only" },
                        new SelectOption { Value = "2", Label = "All Players" }
                    ],
                    Category = "Journey",
                    Order = 6
                },
                new ConfigField
                {
                    Key = "journeypermission_setdifficulty",
                    DisplayName = "Change Difficulty",
                    Description = "Permission to change enemy difficulty.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Locked" },
                        new SelectOption { Value = "1", Label = "Host Only" },
                        new SelectOption { Value = "2", Label = "All Players" }
                    ],
                    Category = "Journey",
                    Order = 7
                },
                new ConfigField
                {
                    Key = "journeypermission_biomespread_setfrozen",
                    DisplayName = "Freeze Biome Spread",
                    Description = "Permission to freeze/unfreeze biome spreading.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Locked" },
                        new SelectOption { Value = "1", Label = "Host Only" },
                        new SelectOption { Value = "2", Label = "All Players" }
                    ],
                    Category = "Journey",
                    Order = 8
                },
                new ConfigField
                {
                    Key = "journeypermission_setspawnrate",
                    DisplayName = "Enemy Spawn Rate",
                    Description = "Permission to adjust enemy spawn rate.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Locked" },
                        new SelectOption { Value = "1", Label = "Host Only" },
                        new SelectOption { Value = "2", Label = "All Players" }
                    ],
                    Category = "Journey",
                    Order = 9
                },
                new ConfigField
                {
                    Key = "journeypermission_increaseplacementrange",
                    DisplayName = "Increased Placement Range",
                    Description = "Permission to increase tile placement range.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "2",
                    Options =
                    [
                        new SelectOption { Value = "0", Label = "Locked" },
                        new SelectOption { Value = "1", Label = "Host Only" },
                        new SelectOption { Value = "2", Label = "All Players" }
                    ],
                    Category = "Journey",
                    Order = 10
                },

                // =====================
                // ADVANCED CATEGORY
                // =====================
                new ConfigField
                {
                    Key = "modpath",
                    DisplayName = "Mods Directory",
                    Description = "Path to the Mods folder. Keep within the server folder for file browser access.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "Mods",
                    Category = "Advanced",
                    Order = 1,
                    Placeholder = "Mods (relative to server folder)"
                },
                new ConfigField
                {
                    Key = "modpack",
                    DisplayName = "Modpack File",
                    Description = "Path to enabled.json file specifying which mods to load.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Advanced",
                    Order = 2,
                    Placeholder = "Default: ./Mods/enabled.json"
                },
                new ConfigField
                {
                    Key = "slowliquids",
                    DisplayName = "Slow Liquids",
                    Description = "Reduce liquid (water/lava/honey) update frequency for performance.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Advanced",
                    Order = 3
                },
                new ConfigField
                {
                    Key = "banlist",
                    DisplayName = "Ban List Path",
                    Description = "Path to the ban list file.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "banlist.txt",
                    Category = "Advanced",
                    Order = 4
                }
            ]
        };
    }
}
