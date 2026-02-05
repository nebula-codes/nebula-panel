using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Partial class containing the paper-global.yml configuration schema.
/// Used by Paper server software for global optimizations.
/// </summary>
public partial class MinecraftProvider
{
    /// <summary>
    /// Adds the paper-global.yml configuration schema.
    /// </summary>
    static partial void AddPaperConfigSchema(Dictionary<string, ConfigurationSchema> schemas)
    {
        schemas["config/paper-global.yml"] = new ConfigurationSchema
        {
            FileName = "config/paper-global.yml",
            FileType = ConfigFileType.Yaml,
            Fields =
            [
                // =====================
                // ASYNC CHUNKS
                // =====================
                new ConfigField
                {
                    Key = "async-chunks.threads",
                    DisplayName = "Async Chunk Threads",
                    Description = "Number of threads for async chunk operations (-1 = auto).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = -1,
                    Validation = new ValidationRule { MinValue = -1 },
                    Category = "Async Chunks"
                },

                // =====================
                // CHUNK LOADING (BASIC)
                // =====================
                new ConfigField
                {
                    Key = "chunk-loading-basic.autoconfig-send-distance",
                    DisplayName = "Auto-config Send Distance",
                    Description = "Automatically configure chunk send distance based on view distance.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Chunk Loading"
                },
                new ConfigField
                {
                    Key = "chunk-loading-basic.player-max-chunk-generate-rate",
                    DisplayName = "Player Max Chunk Gen Rate",
                    Description = "Maximum chunks generated per second per player (-1 = unlimited).",
                    Type = ConfigFieldType.Float,
                    DefaultValue = -1.0,
                    Category = "Chunk Loading"
                },
                new ConfigField
                {
                    Key = "chunk-loading-basic.player-max-chunk-load-rate",
                    DisplayName = "Player Max Chunk Load Rate",
                    Description = "Maximum chunks loaded per second per player (-1 = unlimited).",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 100.0,
                    Category = "Chunk Loading"
                },
                new ConfigField
                {
                    Key = "chunk-loading-basic.player-max-chunk-send-rate",
                    DisplayName = "Player Max Chunk Send Rate",
                    Description = "Maximum chunks sent per second per player (-1 = unlimited).",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 75.0,
                    Category = "Chunk Loading"
                },

                // =====================
                // CHUNK LOADING (ADVANCED)
                // =====================
                new ConfigField
                {
                    Key = "chunk-loading-advanced.auto-config-send-distance",
                    DisplayName = "Auto Config Send Distance",
                    Description = "Automatically configure chunk send distance.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Chunk Loading Advanced"
                },
                new ConfigField
                {
                    Key = "chunk-loading-advanced.player-max-concurrent-chunk-generates",
                    DisplayName = "Max Concurrent Chunk Generates",
                    Description = "Maximum concurrent chunk generations per player (0 = auto).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 0,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Chunk Loading Advanced"
                },
                new ConfigField
                {
                    Key = "chunk-loading-advanced.player-max-concurrent-chunk-loads",
                    DisplayName = "Max Concurrent Chunk Loads",
                    Description = "Maximum concurrent chunk loads per player (0 = auto).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 0,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Chunk Loading Advanced"
                },

                // =====================
                // COLLISIONS
                // =====================
                new ConfigField
                {
                    Key = "collisions.enable-player-collisions",
                    DisplayName = "Enable Player Collisions",
                    Description = "Enable player-to-player collisions.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Collisions"
                },
                new ConfigField
                {
                    Key = "collisions.send-full-pos-for-hard-colliding-entities",
                    DisplayName = "Full Position for Hard Collisions",
                    Description = "Send full position data for entities with hard collisions.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Collisions"
                },

                // =====================
                // COMMANDS
                // =====================
                new ConfigField
                {
                    Key = "commands.suggest-player-names-when-null-tab-completions",
                    DisplayName = "Suggest Player Names",
                    Description = "Suggest player names when no tab completions available.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Commands"
                },
                new ConfigField
                {
                    Key = "commands.time-command-affects-all-worlds",
                    DisplayName = "Time Command All Worlds",
                    Description = "Make /time command affect all worlds.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Commands"
                },
                new ConfigField
                {
                    Key = "commands.fix-target-selector-tag-completion",
                    DisplayName = "Fix Target Selector Completion",
                    Description = "Fix target selector tag tab completion.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Commands"
                },

                // =====================
                // CONSOLE
                // =====================
                new ConfigField
                {
                    Key = "console.enable-brigadier-highlighting",
                    DisplayName = "Brigadier Highlighting",
                    Description = "Enable syntax highlighting for commands in console.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Console"
                },
                new ConfigField
                {
                    Key = "console.enable-brigadier-completions",
                    DisplayName = "Brigadier Completions",
                    Description = "Enable tab completions in console.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Console"
                },

                // =====================
                // ITEM VALIDATION
                // =====================
                new ConfigField
                {
                    Key = "item-validation.display-name",
                    DisplayName = "Max Display Name Length",
                    Description = "Maximum length for item display names.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 8192,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Item Validation"
                },
                new ConfigField
                {
                    Key = "item-validation.lore-line",
                    DisplayName = "Max Lore Line Length",
                    Description = "Maximum length for item lore lines.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 8192,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Item Validation"
                },
                new ConfigField
                {
                    Key = "item-validation.book.title",
                    DisplayName = "Max Book Title Length",
                    Description = "Maximum length for book titles.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 8192,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Item Validation"
                },
                new ConfigField
                {
                    Key = "item-validation.book.author",
                    DisplayName = "Max Book Author Length",
                    Description = "Maximum length for book author names.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 8192,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Item Validation"
                },
                new ConfigField
                {
                    Key = "item-validation.book.page",
                    DisplayName = "Max Book Page Length",
                    Description = "Maximum length for book pages.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 16384,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Item Validation"
                },
                new ConfigField
                {
                    Key = "item-validation.resolve-selectors-in-books",
                    DisplayName = "Resolve Selectors in Books",
                    Description = "Resolve target selectors in books.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Item Validation"
                },

                // =====================
                // LOGGING
                // =====================
                new ConfigField
                {
                    Key = "logging.deobfuscate-stacktraces",
                    DisplayName = "Deobfuscate Stacktraces",
                    Description = "Convert obfuscated class names in stack traces to readable names.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Logging"
                },
                new ConfigField
                {
                    Key = "logging.log-player-ip-addresses",
                    DisplayName = "Log Player IP Addresses",
                    Description = "Log player IP addresses in server log.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Logging"
                },

                // =====================
                // MESSAGES
                // =====================
                new ConfigField
                {
                    Key = "messages.no-permission",
                    DisplayName = "No Permission Message",
                    Description = "Message shown when player lacks permission.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "<red>I'm sorry, but you do not have permission to perform this command. Please contact the server administrators if you believe that this is in error.",
                    Category = "Messages"
                },
                new ConfigField
                {
                    Key = "messages.use-display-name-in-quit-message",
                    DisplayName = "Display Name in Quit Message",
                    Description = "Use player's display name in quit messages.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Messages"
                },
                new ConfigField
                {
                    Key = "messages.kick.authentication-servers-down",
                    DisplayName = "Auth Servers Down Message",
                    Description = "Message shown when Mojang auth servers are unavailable.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "<lang:multiplayer.disconnect.authservers_down>",
                    Category = "Kick Messages"
                },
                new ConfigField
                {
                    Key = "messages.kick.connection-throttle",
                    DisplayName = "Connection Throttle Message",
                    Description = "Message shown when connection is throttled.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "Connection throttled! Please wait before reconnecting.",
                    Category = "Kick Messages"
                },
                new ConfigField
                {
                    Key = "messages.kick.flying-player",
                    DisplayName = "Flying Player Kick Message",
                    Description = "Message shown when player is kicked for flying.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "<lang:multiplayer.disconnect.flying>",
                    Category = "Kick Messages"
                },
                new ConfigField
                {
                    Key = "messages.kick.flying-vehicle",
                    DisplayName = "Flying Vehicle Kick Message",
                    Description = "Message shown when player is kicked for flying vehicle.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "<lang:multiplayer.disconnect.flying>",
                    Category = "Kick Messages"
                },

                // =====================
                // MISC
                // =====================
                new ConfigField
                {
                    Key = "misc.fix-entity-position-desync",
                    DisplayName = "Fix Entity Position Desync",
                    Description = "Fix client-server entity position desync.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Misc"
                },
                new ConfigField
                {
                    Key = "misc.lag-compensate-block-breaking",
                    DisplayName = "Lag Compensate Block Breaking",
                    Description = "Compensate for lag when breaking blocks.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Misc"
                },
                new ConfigField
                {
                    Key = "misc.load-permissions-yml-before-plugins",
                    DisplayName = "Load Permissions Before Plugins",
                    Description = "Load permissions.yml before plugins.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Misc"
                },
                new ConfigField
                {
                    Key = "misc.max-joins-per-tick",
                    DisplayName = "Max Joins Per Tick",
                    Description = "Maximum player joins processed per tick.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 5,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Misc"
                },
                new ConfigField
                {
                    Key = "misc.region-file-cache-size",
                    DisplayName = "Region File Cache Size",
                    Description = "Number of region files to cache.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 256,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Misc"
                },
                new ConfigField
                {
                    Key = "misc.strict-advancement-dimension-check",
                    DisplayName = "Strict Advancement Dimension Check",
                    Description = "Strictly check dimensions for advancements.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Misc"
                },
                new ConfigField
                {
                    Key = "misc.use-alternative-luck-formula",
                    DisplayName = "Alternative Luck Formula",
                    Description = "Use alternative luck formula for loot.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Misc"
                },
                new ConfigField
                {
                    Key = "misc.use-dimension-type-for-custom-spawners",
                    DisplayName = "Dimension Type for Custom Spawners",
                    Description = "Use dimension type for custom spawner mob selection.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Misc"
                },

                // =====================
                // PACKET LIMITER
                // =====================
                new ConfigField
                {
                    Key = "packet-limiter.kick-message",
                    DisplayName = "Packet Limit Kick Message",
                    Description = "Message shown when kicked for packet spam.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "<red><lang:disconnect.exceeded_packet_rate>",
                    Category = "Packet Limiter"
                },
                new ConfigField
                {
                    Key = "packet-limiter.limits.all.interval",
                    DisplayName = "All Packets Interval",
                    Description = "Time window for all packets limit (seconds).",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 7.0,
                    Category = "Packet Limiter"
                },
                new ConfigField
                {
                    Key = "packet-limiter.limits.all.max-packet-rate",
                    DisplayName = "All Packets Max Rate",
                    Description = "Maximum packets per interval.",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 500.0,
                    Category = "Packet Limiter"
                },

                // =====================
                // PLAYER AUTO SAVE
                // =====================
                new ConfigField
                {
                    Key = "player-auto-save.max-per-tick",
                    DisplayName = "Max Player Saves Per Tick",
                    Description = "Maximum players saved per tick (-1 = all).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = -1,
                    Validation = new ValidationRule { MinValue = -1 },
                    Category = "Player Auto Save"
                },
                new ConfigField
                {
                    Key = "player-auto-save.rate",
                    DisplayName = "Player Auto Save Rate",
                    Description = "Ticks between player data saves (-1 = use world autosave).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = -1,
                    Validation = new ValidationRule { MinValue = -1 },
                    Category = "Player Auto Save"
                },

                // =====================
                // PROXIES
                // =====================
                new ConfigField
                {
                    Key = "proxies.proxy-protocol",
                    DisplayName = "Proxy Protocol",
                    Description = "Enable HAProxy PROXY protocol support.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Proxies"
                },
                new ConfigField
                {
                    Key = "proxies.velocity.enabled",
                    DisplayName = "Velocity Support",
                    Description = "Enable Velocity modern forwarding support.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Velocity Proxy"
                },
                new ConfigField
                {
                    Key = "proxies.velocity.online-mode",
                    DisplayName = "Velocity Online Mode",
                    Description = "Use online mode with Velocity proxy.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Velocity Proxy"
                },
                new ConfigField
                {
                    Key = "proxies.velocity.secret",
                    DisplayName = "Velocity Secret",
                    Description = "Shared secret between Velocity and server.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Velocity Proxy"
                },
                new ConfigField
                {
                    Key = "proxies.bungee-cord.online-mode",
                    DisplayName = "BungeeCord Online Mode",
                    Description = "Use BungeeCord's online mode setting.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "BungeeCord Proxy"
                },

                // =====================
                // SCOREBOARDS
                // =====================
                new ConfigField
                {
                    Key = "scoreboards.save-empty-scoreboard-teams",
                    DisplayName = "Save Empty Scoreboard Teams",
                    Description = "Save scoreboard teams with no members.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Scoreboards"
                },
                new ConfigField
                {
                    Key = "scoreboards.track-plugin-scoreboards",
                    DisplayName = "Track Plugin Scoreboards",
                    Description = "Track scoreboards created by plugins.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Scoreboards"
                },

                // =====================
                // SPAM LIMITER
                // =====================
                new ConfigField
                {
                    Key = "spam-limiter.incoming-packet-threshold",
                    DisplayName = "Incoming Packet Threshold",
                    Description = "Maximum incoming packets per second.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 300,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Spam Limiter"
                },
                new ConfigField
                {
                    Key = "spam-limiter.recipe-spam-increment",
                    DisplayName = "Recipe Spam Increment",
                    Description = "Points added per recipe request.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Spam Limiter"
                },
                new ConfigField
                {
                    Key = "spam-limiter.recipe-spam-limit",
                    DisplayName = "Recipe Spam Limit",
                    Description = "Maximum recipe spam points before action.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 20,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Spam Limiter"
                },
                new ConfigField
                {
                    Key = "spam-limiter.tab-spam-increment",
                    DisplayName = "Tab Spam Increment",
                    Description = "Points added per tab completion request.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1,
                    Validation = new ValidationRule { MinValue = 0 },
                    Category = "Spam Limiter"
                },
                new ConfigField
                {
                    Key = "spam-limiter.tab-spam-limit",
                    DisplayName = "Tab Spam Limit",
                    Description = "Maximum tab spam points before action.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 500,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Spam Limiter"
                },

                // =====================
                // TIMINGS
                // =====================
                new ConfigField
                {
                    Key = "timings.enabled",
                    DisplayName = "Timings Enabled",
                    Description = "Enable Paper timings for performance analysis.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Timings"
                },
                new ConfigField
                {
                    Key = "timings.verbose",
                    DisplayName = "Timings Verbose",
                    Description = "Enable verbose timings output.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Timings"
                },
                new ConfigField
                {
                    Key = "timings.server-name-privacy",
                    DisplayName = "Server Name Privacy",
                    Description = "Hide server name in timings reports.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Timings"
                },
                new ConfigField
                {
                    Key = "timings.history-interval",
                    DisplayName = "Timings History Interval",
                    Description = "Seconds between timings history snapshots.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 300,
                    Validation = new ValidationRule { MinValue = 60 },
                    Category = "Timings"
                },
                new ConfigField
                {
                    Key = "timings.history-length",
                    DisplayName = "Timings History Length",
                    Description = "Seconds of timings history to keep.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 3600,
                    Validation = new ValidationRule { MinValue = 300 },
                    Category = "Timings"
                },
                new ConfigField
                {
                    Key = "timings.url",
                    DisplayName = "Timings URL",
                    Description = "URL for timings viewer.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "https://timings.aikar.co/",
                    Category = "Timings"
                },

                // =====================
                // WATCHDOG
                // =====================
                new ConfigField
                {
                    Key = "watchdog.early-warning-delay",
                    DisplayName = "Watchdog Early Warning Delay",
                    Description = "Milliseconds before early warning (10000 = 10 seconds).",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 10000,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Watchdog"
                },
                new ConfigField
                {
                    Key = "watchdog.early-warning-every",
                    DisplayName = "Watchdog Early Warning Interval",
                    Description = "Milliseconds between early warnings.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 5000,
                    Validation = new ValidationRule { MinValue = 1 },
                    Category = "Watchdog"
                }
            ]
        };
    }
}
