using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.ArkSurvivalAscended;

/// <summary>
/// Partial class containing the GameUserSettings.ini configuration schema for ASA.
/// </summary>
public partial class ArkSurvivalAscendedProvider
{
    /// <summary>
    /// Adds the GameUserSettings.ini configuration schema with all ASA server fields.
    /// </summary>
    static partial void AddGameUserSettingsSchema(Dictionary<string, ConfigurationSchema> schemas)
    {
        schemas["ShooterGame/Saved/Config/WindowsServer/GameUserSettings.ini"] = new ConfigurationSchema
        {
            FileName = "ShooterGame/Saved/Config/WindowsServer/GameUserSettings.ini",
            DisplayName = "Server Settings (GameUserSettings.ini)",
            Description = "Main server settings for Ark: Survival Ascended",
            FileType = ConfigFileType.Ini,
            CreateIfMissing = true,
            Sections =
            [
                new ConfigSection
                {
                    Key = "ServerSettings",
                    DisplayName = "Server Settings",
                    Description = "Core server configuration",
                    Icon = "server",
                    Order = 1,
                    DefaultExpanded = true
                },
                new ConfigSection
                {
                    Key = "SessionSettings",
                    DisplayName = "Session Settings",
                    Description = "Server browser and network settings",
                    Icon = "globe",
                    Order = 2,
                    DefaultExpanded = true
                },
                new ConfigSection
                {
                    Key = "Rates",
                    DisplayName = "Rates & Multipliers",
                    Description = "XP, harvesting, taming, and breeding multipliers",
                    Icon = "sliders",
                    Order = 3,
                    DefaultExpanded = false
                },
                new ConfigSection
                {
                    Key = "DayNight",
                    DisplayName = "Day/Night Cycle",
                    Description = "Day and night speed settings",
                    Icon = "sun",
                    Order = 4,
                    DefaultExpanded = false
                },
                new ConfigSection
                {
                    Key = "Advanced",
                    DisplayName = "Advanced Settings",
                    Description = "Auto-save, dino limits, and logging",
                    Icon = "cog",
                    Order = 5,
                    DefaultExpanded = false
                }
            ],
            Fields =
            [
                // =====================
                // SERVER SETTINGS
                // =====================
                new ConfigField
                {
                    Key = "ServerPassword",
                    DisplayName = "Server Password",
                    Description = "Password required to join the server. Leave empty for no password.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "ServerSettings",

                    Order = 1,
                    Placeholder = "Leave empty for no password"
                },
                new ConfigField
                {
                    Key = "ServerAdminPassword",
                    DisplayName = "Admin Password",
                    Description = "Password for admin/RCON access. Required for server administration.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "ServerSettings",

                    Order = 2,
                    Required = true
                },
                new ConfigField
                {
                    Key = "MaxPlayers",
                    DisplayName = "Max Players",
                    Description = "Maximum number of players that can join the server.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 70,
                    SliderOptions = new SliderOptions { Min = 1, Max = 127, Step = 1 },
                    Category = "ServerSettings",

                    Order = 3
                },
                new ConfigField
                {
                    Key = "RCONEnabled",
                    DisplayName = "RCON Enabled",
                    Description = "Enable remote console (RCON) access for server administration.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "ServerSettings",

                    Order = 4
                },
                new ConfigField
                {
                    Key = "RCONPort",
                    DisplayName = "RCON Port",
                    Description = "Port for remote console (RCON) connections.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 27020,
                    Category = "ServerSettings",

                    Order = 5
                },
                new ConfigField
                {
                    Key = "DifficultyOffset",
                    DisplayName = "Difficulty",
                    Description = "Server difficulty scale. Higher values mean stronger wild dinos.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.0, Max = 1.0, Step = 0.1 },
                    Category = "ServerSettings",

                    Order = 6
                },
                new ConfigField
                {
                    Key = "ServerPVE",
                    DisplayName = "PvE Mode",
                    Description = "Enable PvE mode (disables player vs player combat). When off, PvP is enabled.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "ServerSettings",

                    Order = 7
                },
                new ConfigField
                {
                    Key = "AllowFlyerCarryPVE",
                    DisplayName = "Allow Flyer Carry (PvE)",
                    Description = "Allow flying creatures to carry other players/dinos in PvE.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "ServerSettings",

                    Order = 8
                },
                new ConfigField
                {
                    Key = "DisableStructureDecayPvE",
                    DisplayName = "Disable Structure Decay",
                    Description = "Prevent structures from decaying over time.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "ServerSettings",

                    Order = 9
                },
                new ConfigField
                {
                    Key = "AllowThirdPersonPlayer",
                    DisplayName = "Allow Third Person",
                    Description = "Allow players to use third-person camera view.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "ServerSettings",

                    Order = 10
                },
                new ConfigField
                {
                    Key = "ShowMapPlayerLocation",
                    DisplayName = "Show Player Location on Map",
                    Description = "Display player positions on the in-game map.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "ServerSettings",

                    Order = 11
                },
                new ConfigField
                {
                    Key = "EnableCrossplay",
                    DisplayName = "Enable Crossplay",
                    Description = "Allow crossplay between different platforms.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "ServerSettings",

                    Order = 12
                },

                // =====================
                // SESSION SETTINGS
                // =====================
                new ConfigField
                {
                    Key = "SessionName",
                    DisplayName = "Server Name",
                    Description = "Server name displayed in the server browser.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "Nebula Panel ASA Server",
                    Category = "SessionSettings",

                    Order = 1,
                    Required = true,
                    Validation = new ValidationRule { MaxLength = 128 }
                },
                new ConfigField
                {
                    Key = "QueryPort",
                    DisplayName = "Query Port",
                    Description = "Steam query port for server browser listing.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 27015,
                    Category = "SessionSettings",

                    Order = 2
                },
                new ConfigField
                {
                    Key = "Port",
                    DisplayName = "Game Port",
                    Description = "Main game port for client connections.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 7777,
                    Category = "SessionSettings",

                    Order = 3
                },

                // =====================
                // RATES & MULTIPLIERS
                // =====================
                new ConfigField
                {
                    Key = "TamingSpeedMultiplier",
                    DisplayName = "Taming Speed",
                    Description = "Multiplier for creature taming speed.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 100.0, Step = 0.1 },
                    Category = "Rates",

                    Order = 1
                },
                new ConfigField
                {
                    Key = "HarvestAmountMultiplier",
                    DisplayName = "Harvest Amount",
                    Description = "Multiplier for resources gathered when harvesting.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 100.0, Step = 0.1 },
                    Category = "Rates",

                    Order = 2
                },
                new ConfigField
                {
                    Key = "XPMultiplier",
                    DisplayName = "XP Multiplier",
                    Description = "Multiplier for experience points gained.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 100.0, Step = 0.1 },
                    Category = "Rates",

                    Order = 3
                },
                new ConfigField
                {
                    Key = "MatingIntervalMultiplier",
                    DisplayName = "Mating Interval",
                    Description = "Multiplier for time between creature mating. Lower = faster.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.01, Max = 100.0, Step = 0.01 },
                    Category = "Rates",

                    Order = 4
                },
                new ConfigField
                {
                    Key = "BabyMatureSpeedMultiplier",
                    DisplayName = "Baby Mature Speed",
                    Description = "Multiplier for baby creature maturation speed.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 100.0, Step = 0.1 },
                    Category = "Rates",

                    Order = 5
                },
                new ConfigField
                {
                    Key = "EggHatchSpeedMultiplier",
                    DisplayName = "Egg Hatch Speed",
                    Description = "Multiplier for egg hatching speed.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 100.0, Step = 0.1 },
                    Category = "Rates",

                    Order = 6
                },
                new ConfigField
                {
                    Key = "HarvestHealthMultiplier",
                    DisplayName = "Harvest Health",
                    Description = "Multiplier for harvestable resource node health.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 100.0, Step = 0.1 },
                    Category = "Rates",

                    Order = 7
                },
                new ConfigField
                {
                    Key = "PlayerDamageMultiplier",
                    DisplayName = "Player Damage",
                    Description = "Multiplier for damage dealt by players.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 100.0, Step = 0.1 },
                    Category = "Rates",

                    Order = 8
                },
                new ConfigField
                {
                    Key = "DinoDamageMultiplier",
                    DisplayName = "Dino Damage",
                    Description = "Multiplier for damage dealt by creatures.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 100.0, Step = 0.1 },
                    Category = "Rates",

                    Order = 9
                },
                new ConfigField
                {
                    Key = "PlayerResistanceMultiplier",
                    DisplayName = "Player Resistance",
                    Description = "Multiplier for player damage resistance. Higher = more resistant.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 100.0, Step = 0.1 },
                    Category = "Rates",

                    Order = 10
                },
                new ConfigField
                {
                    Key = "DinoResistanceMultiplier",
                    DisplayName = "Dino Resistance",
                    Description = "Multiplier for creature damage resistance. Higher = more resistant.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 100.0, Step = 0.1 },
                    Category = "Rates",

                    Order = 11
                },
                new ConfigField
                {
                    Key = "ResourcesRespawnPeriodMultiplier",
                    DisplayName = "Resource Respawn Period",
                    Description = "Multiplier for resource respawn time. Lower = faster respawn.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.01, Max = 100.0, Step = 0.01 },
                    Category = "Rates",

                    Order = 12
                },

                // =====================
                // DAY/NIGHT CYCLE
                // =====================
                new ConfigField
                {
                    Key = "DayTimeSpeedScale",
                    DisplayName = "Day Time Speed",
                    Description = "Speed multiplier for daytime. Higher = shorter days.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 10.0, Step = 0.1 },
                    Category = "DayNight",

                    Order = 1
                },
                new ConfigField
                {
                    Key = "NightTimeSpeedScale",
                    DisplayName = "Night Time Speed",
                    Description = "Speed multiplier for nighttime. Higher = shorter nights.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 10.0, Step = 0.1 },
                    Category = "DayNight",

                    Order = 2
                },
                new ConfigField
                {
                    Key = "DayCycleSpeedScale",
                    DisplayName = "Day Cycle Speed",
                    Description = "Overall day/night cycle speed. Higher = faster cycles.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 10.0, Step = 0.1 },
                    Category = "DayNight",

                    Order = 3
                },

                // =====================
                // ADVANCED
                // =====================
                new ConfigField
                {
                    Key = "AutoSavePeriodMinutes",
                    DisplayName = "Auto-Save Interval (Minutes)",
                    Description = "How often the server auto-saves in minutes.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 15.0,
                    SliderOptions = new SliderOptions { Min = 1, Max = 120, Step = 1 },
                    Category = "Advanced",

                    Order = 1
                },
                new ConfigField
                {
                    Key = "MaxTamedDinos",
                    DisplayName = "Max Tamed Dinos",
                    Description = "Maximum number of tamed creatures allowed on the server.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 5000,
                    SliderOptions = new SliderOptions { Min = 100, Max = 10000, Step = 100 },
                    Category = "Advanced",

                    Order = 2
                },
                new ConfigField
                {
                    Key = "ClampItemStats",
                    DisplayName = "Clamp Item Stats",
                    Description = "Clamp item stats to prevent extreme values.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Advanced",

                    Order = 3
                },
                new ConfigField
                {
                    Key = "AdminLogging",
                    DisplayName = "Admin Logging",
                    Description = "Log admin commands and actions.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Advanced",

                    Order = 4
                },
                new ConfigField
                {
                    Key = "TribeLogDestroyedEnemyStructures",
                    DisplayName = "Log Destroyed Enemy Structures",
                    Description = "Log destruction of enemy structures in tribe log.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Advanced",

                    Order = 5
                }
            ]
        };
    }
}
