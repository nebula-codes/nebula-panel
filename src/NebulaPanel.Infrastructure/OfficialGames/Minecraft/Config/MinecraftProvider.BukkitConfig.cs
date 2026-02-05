using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Partial class containing the bukkit.yml configuration schema.
/// Used by Paper, Spigot, and other Bukkit-based server software.
/// </summary>
public partial class MinecraftProvider
{
    /// <summary>
    /// Adds the bukkit.yml configuration schema.
    /// </summary>
    static partial void AddBukkitConfigSchema(Dictionary<string, ConfigurationSchema> schemas)
    {
        schemas["bukkit.yml"] = new ConfigurationSchema
        {
            FileName = "bukkit.yml",
            FileType = ConfigFileType.Yaml,
            Fields =
            [
                // =====================
                // SETTINGS SECTION
                // =====================
                new ConfigField
                {
                    Key = "settings.allow-end",
                    DisplayName = "Allow End",
                    Description = "Allow players to travel to The End dimension.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.warn-on-overload",
                    DisplayName = "Warn on Overload",
                    Description = "Show warning messages in console when server is overloaded.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.permissions-file",
                    DisplayName = "Permissions File",
                    Description = "Path to the permissions configuration file.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "permissions.yml",
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.update-folder",
                    DisplayName = "Update Folder",
                    Description = "Folder for plugin updates.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "update",
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.plugin-profiling",
                    DisplayName = "Plugin Profiling",
                    Description = "Enable plugin performance profiling with /timings.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.connection-throttle",
                    DisplayName = "Connection Throttle",
                    Description = "Milliseconds between connection attempts from same IP (-1 = disabled).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 4000,
                    Validation = new ValidationRule { MinValue = -1 },
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.query-plugins",
                    DisplayName = "Query Plugins",
                    Description = "Show plugin list in server query responses.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.deprecated-verbose",
                    DisplayName = "Deprecated Verbose",
                    Description = "Log deprecated API usage by plugins.",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "default",
                    Options =
                    [
                        new SelectOption { Value = "default", Label = "Default" },
                        new SelectOption { Value = "true", Label = "Always" },
                        new SelectOption { Value = "false", Label = "Never" }
                    ],
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.shutdown-message",
                    DisplayName = "Shutdown Message",
                    Description = "Message shown to players when server shuts down.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "Server closed",
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.minimum-api",
                    DisplayName = "Minimum API Version",
                    Description = "Minimum Bukkit API version required for plugins.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "none",
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.use-map-color-cache",
                    DisplayName = "Use Map Color Cache",
                    Description = "Cache map colors for better performance.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Settings"
                },

                // =====================
                // SPAWN LIMITS SECTION
                // =====================
                new ConfigField
                {
                    Key = "spawn-limits.monsters",
                    DisplayName = "Monster Spawn Limit",
                    Description = "Maximum number of monsters per world.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 70,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Spawn Limits"
                },
                new ConfigField
                {
                    Key = "spawn-limits.animals",
                    DisplayName = "Animal Spawn Limit",
                    Description = "Maximum number of passive animals per world.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 10,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Spawn Limits"
                },
                new ConfigField
                {
                    Key = "spawn-limits.water-animals",
                    DisplayName = "Water Animal Spawn Limit",
                    Description = "Maximum number of water animals (dolphins, squid) per world.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 5,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Spawn Limits"
                },
                new ConfigField
                {
                    Key = "spawn-limits.water-ambient",
                    DisplayName = "Water Ambient Spawn Limit",
                    Description = "Maximum number of fish per world.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 20,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Spawn Limits"
                },
                new ConfigField
                {
                    Key = "spawn-limits.water-underground-creature",
                    DisplayName = "Water Underground Creature Limit",
                    Description = "Maximum number of glow squids per world.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 5,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Spawn Limits"
                },
                new ConfigField
                {
                    Key = "spawn-limits.axolotls",
                    DisplayName = "Axolotl Spawn Limit",
                    Description = "Maximum number of axolotls per world.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 5,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Spawn Limits"
                },
                new ConfigField
                {
                    Key = "spawn-limits.ambient",
                    DisplayName = "Ambient Spawn Limit",
                    Description = "Maximum number of bats per world.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 15,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Spawn Limits"
                },

                // =====================
                // CHUNK GARBAGE COLLECTOR
                // =====================
                new ConfigField
                {
                    Key = "chunk-gc.period-in-ticks",
                    DisplayName = "Chunk GC Period",
                    Description = "Ticks between chunk garbage collection cycles (0 = disabled).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 600,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Chunk GC"
                },

                // =====================
                // TICK INTERVALS SECTION
                // =====================
                new ConfigField
                {
                    Key = "ticks-per.animal-spawns",
                    DisplayName = "Animal Spawn Interval",
                    Description = "Ticks between animal spawn attempts.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 400,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Tick Intervals"
                },
                new ConfigField
                {
                    Key = "ticks-per.monster-spawns",
                    DisplayName = "Monster Spawn Interval",
                    Description = "Ticks between monster spawn attempts.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Tick Intervals"
                },
                new ConfigField
                {
                    Key = "ticks-per.water-spawns",
                    DisplayName = "Water Spawn Interval",
                    Description = "Ticks between water animal spawn attempts.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Tick Intervals"
                },
                new ConfigField
                {
                    Key = "ticks-per.water-ambient-spawns",
                    DisplayName = "Water Ambient Spawn Interval",
                    Description = "Ticks between fish spawn attempts.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Tick Intervals"
                },
                new ConfigField
                {
                    Key = "ticks-per.water-underground-creature-spawns",
                    DisplayName = "Water Underground Spawn Interval",
                    Description = "Ticks between glow squid spawn attempts.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Tick Intervals"
                },
                new ConfigField
                {
                    Key = "ticks-per.axolotl-spawns",
                    DisplayName = "Axolotl Spawn Interval",
                    Description = "Ticks between axolotl spawn attempts.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Tick Intervals"
                },
                new ConfigField
                {
                    Key = "ticks-per.ambient-spawns",
                    DisplayName = "Ambient Spawn Interval",
                    Description = "Ticks between bat spawn attempts.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Tick Intervals"
                },
                new ConfigField
                {
                    Key = "ticks-per.autosave",
                    DisplayName = "Autosave Interval",
                    Description = "Ticks between world autosaves (6000 = 5 minutes).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 6000,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Tick Intervals"
                },

                // =====================
                // ALIASES SECTION
                // =====================
                new ConfigField
                {
                    Key = "aliases",
                    DisplayName = "Command Aliases",
                    Description = "Command aliases configuration (now or bukkit).",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "now",
                    Options =
                    [
                        new SelectOption { Value = "now", Label = "Load Now" },
                        new SelectOption { Value = "bukkit", Label = "Bukkit Style" }
                    ],
                    Category = "Aliases"
                }
            ]
        };
    }
}
