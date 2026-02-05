using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Partial class containing the spigot.yml configuration schema.
/// Used by Spigot and Paper server software.
/// </summary>
public partial class MinecraftProvider
{
    /// <summary>
    /// Adds the spigot.yml configuration schema.
    /// </summary>
    static partial void AddSpigotConfigSchema(Dictionary<string, ConfigurationSchema> schemas)
    {
        schemas["spigot.yml"] = new ConfigurationSchema
        {
            FileName = "spigot.yml",
            FileType = ConfigFileType.Yaml,
            Fields =
            [
                // =====================
                // SETTINGS SECTION
                // =====================
                new ConfigField
                {
                    Key = "settings.debug",
                    DisplayName = "Debug Mode",
                    Description = "Enable debug logging.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.bungeecord",
                    DisplayName = "BungeeCord Mode",
                    Description = "Enable BungeeCord IP forwarding. Only enable behind a BungeeCord proxy!",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.timeout-time",
                    DisplayName = "Timeout Time",
                    Description = "Seconds before a player times out.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 60,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.restart-on-crash",
                    DisplayName = "Restart on Crash",
                    Description = "Automatically restart server after crash.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.restart-script",
                    DisplayName = "Restart Script",
                    Description = "Path to script to run for server restart.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "./start.sh",
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.netty-threads",
                    DisplayName = "Netty Threads",
                    Description = "Number of network threads (0 = auto).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 4,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.sample-count",
                    DisplayName = "Player Sample Count",
                    Description = "Number of players shown in server list hover.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 12,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.player-shuffle",
                    DisplayName = "Player Shuffle",
                    Description = "Ticks between player processing order shuffle (0 = disabled).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 0,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.user-cache-size",
                    DisplayName = "User Cache Size",
                    Description = "Number of user profiles to cache.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1000,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.save-user-cache-on-stop-only",
                    DisplayName = "Save User Cache on Stop Only",
                    Description = "Only save user cache when server stops.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.moved-wrongly-threshold",
                    DisplayName = "Moved Wrongly Threshold",
                    Description = "Threshold for 'moved wrongly' detection.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 0.0625,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.moved-too-quickly-multiplier",
                    DisplayName = "Moved Too Quickly Multiplier",
                    Description = "Multiplier for 'moved too quickly' detection.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 10.0,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.log-villager-deaths",
                    DisplayName = "Log Villager Deaths",
                    Description = "Log villager death messages.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Settings"
                },
                new ConfigField
                {
                    Key = "settings.log-named-deaths",
                    DisplayName = "Log Named Deaths",
                    Description = "Log named entity death messages.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Settings"
                },

                // =====================
                // ATTRIBUTE LIMITS
                // =====================
                new ConfigField
                {
                    Key = "settings.attribute.maxHealth.max",
                    DisplayName = "Max Health Limit",
                    Description = "Maximum allowed max health attribute.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 2048.0,
                    Category = "Attribute Limits"
                },
                new ConfigField
                {
                    Key = "settings.attribute.movementSpeed.max",
                    DisplayName = "Max Movement Speed",
                    Description = "Maximum allowed movement speed attribute.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 2048.0,
                    Category = "Attribute Limits"
                },
                new ConfigField
                {
                    Key = "settings.attribute.attackDamage.max",
                    DisplayName = "Max Attack Damage",
                    Description = "Maximum allowed attack damage attribute.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 2048.0,
                    Category = "Attribute Limits"
                },

                // =====================
                // COMMANDS SECTION
                // =====================
                new ConfigField
                {
                    Key = "commands.tab-complete",
                    DisplayName = "Tab Complete",
                    Description = "Minimum characters before tab completion (-1 = disabled).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 0,
                    Validation = new ValidationRule { MinValue = -1 },
                    Category = "Commands"
                },
                new ConfigField
                {
                    Key = "commands.send-namespaced",
                    DisplayName = "Send Namespaced Commands",
                    Description = "Include namespace in command tab completion.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Commands"
                },
                new ConfigField
                {
                    Key = "commands.log",
                    DisplayName = "Log Commands",
                    Description = "Log player commands to console.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Commands"
                },
                new ConfigField
                {
                    Key = "commands.silent-commandblock-console",
                    DisplayName = "Silent Command Block Console",
                    Description = "Hide command block output in console.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Commands"
                },

                // =====================
                // MESSAGES SECTION
                // =====================
                new ConfigField
                {
                    Key = "messages.whitelist",
                    DisplayName = "Whitelist Message",
                    Description = "Message shown when non-whitelisted player tries to join.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "You are not whitelisted on this server!",
                    Category = "Messages"
                },
                new ConfigField
                {
                    Key = "messages.unknown-command",
                    DisplayName = "Unknown Command Message",
                    Description = "Message shown for unknown commands.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "Unknown command. Type \"/help\" for help.",
                    Category = "Messages"
                },
                new ConfigField
                {
                    Key = "messages.server-full",
                    DisplayName = "Server Full Message",
                    Description = "Message shown when server is full.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "The server is full!",
                    Category = "Messages"
                },
                new ConfigField
                {
                    Key = "messages.outdated-client",
                    DisplayName = "Outdated Client Message",
                    Description = "Message shown for outdated clients. {0} = server version.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "Outdated client! Please use {0}",
                    Category = "Messages"
                },
                new ConfigField
                {
                    Key = "messages.outdated-server",
                    DisplayName = "Outdated Server Message",
                    Description = "Message shown when server is outdated. {0} = client version.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "Outdated server! I'm still on {0}",
                    Category = "Messages"
                },
                new ConfigField
                {
                    Key = "messages.restart",
                    DisplayName = "Restart Message",
                    Description = "Message shown during server restart.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "Server is restarting",
                    Category = "Messages"
                },

                // =====================
                // WORLD SETTINGS SECTION
                // =====================
                new ConfigField
                {
                    Key = "world-settings.default.verbose",
                    DisplayName = "Verbose World Loading",
                    Description = "Enable verbose world loading messages.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "World Settings"
                },
                new ConfigField
                {
                    Key = "world-settings.default.mob-spawn-range",
                    DisplayName = "Mob Spawn Range",
                    Description = "Chunk radius for mob spawning.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 8,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "World Settings"
                },
                new ConfigField
                {
                    Key = "world-settings.default.item-despawn-rate",
                    DisplayName = "Item Despawn Rate",
                    Description = "Ticks before dropped items despawn (6000 = 5 min).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 6000,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "World Settings"
                },
                new ConfigField
                {
                    Key = "world-settings.default.arrow-despawn-rate",
                    DisplayName = "Arrow Despawn Rate",
                    Description = "Ticks before arrows despawn (1200 = 1 min).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1200,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "World Settings"
                },
                new ConfigField
                {
                    Key = "world-settings.default.trident-despawn-rate",
                    DisplayName = "Trident Despawn Rate",
                    Description = "Ticks before tridents despawn (1200 = 1 min).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1200,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "World Settings"
                },
                new ConfigField
                {
                    Key = "world-settings.default.nerf-spawner-mobs",
                    DisplayName = "Nerf Spawner Mobs",
                    Description = "Disable AI for mobs from spawners.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "World Settings"
                },
                new ConfigField
                {
                    Key = "world-settings.default.zombie-aggressive-towards-villager",
                    DisplayName = "Zombies Attack Villagers",
                    Description = "Allow zombies to attack villagers.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "World Settings"
                },
                new ConfigField
                {
                    Key = "world-settings.default.enable-zombie-pigmen-portal-spawns",
                    DisplayName = "Zombie Pigmen Portal Spawns",
                    Description = "Allow zombie pigmen to spawn in nether portals.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "World Settings"
                },
                new ConfigField
                {
                    Key = "world-settings.default.wither-spawn-sound-radius",
                    DisplayName = "Wither Spawn Sound Radius",
                    Description = "Block radius for wither spawn sound (0 = disabled).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 0,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "World Settings"
                },
                new ConfigField
                {
                    Key = "world-settings.default.view-distance",
                    DisplayName = "World View Distance",
                    Description = "Override view distance for this world (default = server setting).",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "default",
                    Options =
                    [
                        new SelectOption { Value = "default", Label = "Use Server Setting" },
                        new SelectOption { Value = "3", Label = "3 chunks" },
                        new SelectOption { Value = "4", Label = "4 chunks" },
                        new SelectOption { Value = "6", Label = "6 chunks" },
                        new SelectOption { Value = "8", Label = "8 chunks" },
                        new SelectOption { Value = "10", Label = "10 chunks" },
                        new SelectOption { Value = "12", Label = "12 chunks" },
                        new SelectOption { Value = "16", Label = "16 chunks" }
                    ],
                    Category = "World Settings"
                },
                new ConfigField
                {
                    Key = "world-settings.default.simulation-distance",
                    DisplayName = "World Simulation Distance",
                    Description = "Override simulation distance for this world (default = server setting).",
                    Type = ConfigFieldType.Select,
                    DefaultValue = "default",
                    Options =
                    [
                        new SelectOption { Value = "default", Label = "Use Server Setting" },
                        new SelectOption { Value = "3", Label = "3 chunks" },
                        new SelectOption { Value = "4", Label = "4 chunks" },
                        new SelectOption { Value = "6", Label = "6 chunks" },
                        new SelectOption { Value = "8", Label = "8 chunks" },
                        new SelectOption { Value = "10", Label = "10 chunks" },
                        new SelectOption { Value = "12", Label = "12 chunks" }
                    ],
                    Category = "World Settings"
                },

                // =====================
                // MERGE RADIUS
                // =====================
                new ConfigField
                {
                    Key = "world-settings.default.merge-radius.item",
                    DisplayName = "Item Merge Radius",
                    Description = "Block radius for merging dropped items.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 2.5,
                    Category = "Merge Radius"
                },
                new ConfigField
                {
                    Key = "world-settings.default.merge-radius.exp",
                    DisplayName = "Experience Merge Radius",
                    Description = "Block radius for merging experience orbs.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 3.0,
                    Category = "Merge Radius"
                },

                // =====================
                // ENTITY ACTIVATION RANGE
                // =====================
                new ConfigField
                {
                    Key = "world-settings.default.entity-activation-range.animals",
                    DisplayName = "Animal Activation Range",
                    Description = "Block radius for animal AI activation.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 32,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Activation"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-activation-range.monsters",
                    DisplayName = "Monster Activation Range",
                    Description = "Block radius for monster AI activation.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 32,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Activation"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-activation-range.raiders",
                    DisplayName = "Raider Activation Range",
                    Description = "Block radius for raider AI activation.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 48,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Activation"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-activation-range.misc",
                    DisplayName = "Misc Entity Activation Range",
                    Description = "Block radius for misc entity AI activation.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 16,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Activation"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-activation-range.water",
                    DisplayName = "Water Entity Activation Range",
                    Description = "Block radius for water entity AI activation.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 16,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Activation"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-activation-range.villagers",
                    DisplayName = "Villager Activation Range",
                    Description = "Block radius for villager AI activation.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 32,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Activation"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-activation-range.flying-monsters",
                    DisplayName = "Flying Monster Activation Range",
                    Description = "Block radius for flying monster AI activation.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 32,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Activation"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-activation-range.tick-inactive-villagers",
                    DisplayName = "Tick Inactive Villagers",
                    Description = "Tick villagers even when not active.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Entity Activation"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-activation-range.wake-up-inactive.animals-max-per-tick",
                    DisplayName = "Wake Up Animals Per Tick",
                    Description = "Maximum animals to wake up per tick.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 4,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Activation"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-activation-range.wake-up-inactive.monsters-max-per-tick",
                    DisplayName = "Wake Up Monsters Per Tick",
                    Description = "Maximum monsters to wake up per tick.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 8,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Activation"
                },

                // =====================
                // ENTITY TRACKING RANGE
                // =====================
                new ConfigField
                {
                    Key = "world-settings.default.entity-tracking-range.players",
                    DisplayName = "Player Tracking Range",
                    Description = "Block radius for player visibility to clients.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 48,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Tracking"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-tracking-range.animals",
                    DisplayName = "Animal Tracking Range",
                    Description = "Block radius for animal visibility to clients.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 48,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Tracking"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-tracking-range.monsters",
                    DisplayName = "Monster Tracking Range",
                    Description = "Block radius for monster visibility to clients.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 48,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Tracking"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-tracking-range.misc",
                    DisplayName = "Misc Tracking Range",
                    Description = "Block radius for misc entity visibility to clients.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 32,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Tracking"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-tracking-range.display",
                    DisplayName = "Display Tracking Range",
                    Description = "Block radius for display entity visibility to clients.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 128,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Tracking"
                },
                new ConfigField
                {
                    Key = "world-settings.default.entity-tracking-range.other",
                    DisplayName = "Other Tracking Range",
                    Description = "Block radius for other entity visibility to clients.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 64,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Entity Tracking"
                },

                // =====================
                // HOPPER SETTINGS
                // =====================
                new ConfigField
                {
                    Key = "world-settings.default.ticks-per.hopper-transfer",
                    DisplayName = "Hopper Transfer Interval",
                    Description = "Ticks between hopper item transfers.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 8,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Hopper"
                },
                new ConfigField
                {
                    Key = "world-settings.default.ticks-per.hopper-check",
                    DisplayName = "Hopper Check Interval",
                    Description = "Ticks between hopper item checks.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Hopper"
                },
                new ConfigField
                {
                    Key = "world-settings.default.hopper-amount",
                    DisplayName = "Hopper Transfer Amount",
                    Description = "Items transferred per hopper operation.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Hopper"
                },
                new ConfigField
                {
                    Key = "world-settings.default.hopper-can-load-chunks",
                    DisplayName = "Hoppers Load Chunks",
                    Description = "Allow hoppers to load chunks.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Hopper"
                },

                // =====================
                // HUNGER SETTINGS
                // =====================
                new ConfigField
                {
                    Key = "world-settings.default.hunger.walk-exhaustion",
                    DisplayName = "Walk Exhaustion",
                    Description = "Exhaustion from walking per meter.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 0.2,
                    Category = "Hunger"
                },
                new ConfigField
                {
                    Key = "world-settings.default.hunger.sprint-exhaustion",
                    DisplayName = "Sprint Exhaustion",
                    Description = "Exhaustion from sprinting per meter.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 0.8,
                    Category = "Hunger"
                },
                new ConfigField
                {
                    Key = "world-settings.default.hunger.combat-exhaustion",
                    DisplayName = "Combat Exhaustion",
                    Description = "Exhaustion from attacking.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 0.1,
                    Category = "Hunger"
                },
                new ConfigField
                {
                    Key = "world-settings.default.hunger.regen-exhaustion",
                    DisplayName = "Regen Exhaustion",
                    Description = "Exhaustion from natural health regeneration.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 6.0,
                    Category = "Hunger"
                },
                new ConfigField
                {
                    Key = "world-settings.default.hunger.swim-multiplier",
                    DisplayName = "Swim Exhaustion Multiplier",
                    Description = "Multiplier for swimming exhaustion.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 0.01,
                    Category = "Hunger"
                },
                new ConfigField
                {
                    Key = "world-settings.default.hunger.other-multiplier",
                    DisplayName = "Other Exhaustion Multiplier",
                    Description = "Multiplier for other exhaustion sources.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 0.0,
                    Category = "Hunger"
                },

                // =====================
                // MAX ENTITY COLLISIONS
                // =====================
                new ConfigField
                {
                    Key = "world-settings.default.max-entity-collisions",
                    DisplayName = "Max Entity Collisions",
                    Description = "Maximum entities processed for collision per tick.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 8,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Performance"
                },
                new ConfigField
                {
                    Key = "world-settings.default.max-tick-time.tile",
                    DisplayName = "Max Tile Entity Tick Time",
                    Description = "Max milliseconds for tile entity ticking.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 50,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Performance"
                },
                new ConfigField
                {
                    Key = "world-settings.default.max-tick-time.entity",
                    DisplayName = "Max Entity Tick Time",
                    Description = "Max milliseconds for entity ticking.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 50,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Performance"
                }
            ]
        };
    }
}
