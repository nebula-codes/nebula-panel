using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.FikaSpt;

/// <summary>
/// Partial class containing the fika.jsonc configuration schema for FIKA SPT.
/// File location: SPT/user/mods/fika-server/assets/configs/fika.jsonc
/// </summary>
public partial class FikaSptProvider
{
    static partial void AddFikaConfigSchema(Dictionary<string, ConfigurationSchema> schemas)
    {
        schemas["SPT/user/mods/fika-server/assets/configs/fika.jsonc"] = new ConfigurationSchema
        {
            FileName = "SPT/user/mods/fika-server/assets/configs/fika.jsonc",
            DisplayName = "Fika Server Config",
            Description = "Core Fika server mod configuration — gameplay rules, networking, webhooks, and headless clients",
            FileType = ConfigFileType.Json,
            CreateIfMissing = false,
            Sections =
            [
                new ConfigSection
                {
                    Key = "Gameplay",
                    DisplayName = "Gameplay",
                    Description = "Client-side gameplay rules and raid settings",
                    Icon = "gamepad-2",
                    Order = 1,
                    DefaultExpanded = true
                },
                new ConfigSection
                {
                    Key = "Server",
                    DisplayName = "Server",
                    Description = "Core server behavior — item sending, profiles, sessions",
                    Icon = "server",
                    Order = 2,
                    DefaultExpanded = true
                },
                new ConfigSection
                {
                    Key = "Network",
                    DisplayName = "Network",
                    Description = "SPT HTTP bindings and NAT punch-through",
                    Icon = "network",
                    Order = 3,
                    DefaultExpanded = false
                },
                new ConfigSection
                {
                    Key = "Webhook",
                    DisplayName = "Webhook",
                    Description = "Discord webhook integration for server events",
                    Icon = "webhook",
                    Order = 4,
                    DefaultExpanded = false
                },
                new ConfigSection
                {
                    Key = "Headless",
                    DisplayName = "Headless Clients",
                    Description = "Headless AI client profile generation and scripting",
                    Icon = "bot",
                    Order = 5,
                    DefaultExpanded = false
                },
                new ConfigSection
                {
                    Key = "Background",
                    DisplayName = "Background",
                    Description = "Background task processing",
                    Icon = "layers",
                    Order = 6,
                    DefaultExpanded = false
                }
            ],
            Fields =
            [
                // =====================
                // GAMEPLAY (client.*)
                // =====================
                new ConfigField
                {
                    Key = "client.useBtr",
                    DisplayName = "Enable BTR",
                    Description = "Enable the BTR armored vehicle in Streets of Tarkov.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Gameplay",
                    Order = 1
                },
                new ConfigField
                {
                    Key = "client.friendlyFire",
                    DisplayName = "Friendly Fire",
                    Description = "Allow players in the same group to damage each other.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Gameplay",
                    Order = 2
                },
                new ConfigField
                {
                    Key = "client.dynamicVExfils",
                    DisplayName = "Dynamic Vehicle Exfils",
                    Description = "Enable dynamic vehicle extraction points.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Gameplay",
                    Order = 3
                },
                new ConfigField
                {
                    Key = "client.allowFreeCam",
                    DisplayName = "Allow Free Camera",
                    Description = "Allow players to use free camera mode during raids.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Gameplay",
                    Order = 4
                },
                new ConfigField
                {
                    Key = "client.allowSpectateFreeCam",
                    DisplayName = "Allow Spectate Free Camera",
                    Description = "Allow dead players to spectate using free camera.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Gameplay",
                    Order = 5
                },
                new ConfigField
                {
                    Key = "client.forceSaveOnDeath",
                    DisplayName = "Force Save on Death",
                    Description = "Force a profile save when a player dies in raid.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Gameplay",
                    Order = 6
                },
                new ConfigField
                {
                    Key = "client.useInertia",
                    DisplayName = "Use Inertia",
                    Description = "Enable the inertia movement system.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Gameplay",
                    Order = 7
                },
                new ConfigField
                {
                    Key = "client.sharedQuestProgression",
                    DisplayName = "Shared Quest Progression",
                    Description = "Share quest progress between players in the same group.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Gameplay",
                    Order = 8
                },
                new ConfigField
                {
                    Key = "client.canEditRaidSettings",
                    DisplayName = "Can Edit Raid Settings",
                    Description = "Allow the raid host to modify raid settings before starting.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Gameplay",
                    Order = 9
                },
                new ConfigField
                {
                    Key = "client.enableTransits",
                    DisplayName = "Enable Transits",
                    Description = "Enable map-to-map transit extractions.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Gameplay",
                    Order = 10
                },
                new ConfigField
                {
                    Key = "client.anyoneCanStartRaid",
                    DisplayName = "Anyone Can Start Raid",
                    Description = "Allow any group member to start the raid, not just the host.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Gameplay",
                    Order = 11
                },
                new ConfigField
                {
                    Key = "client.allowNamePlates",
                    DisplayName = "Allow Name Plates",
                    Description = "Show player name plates above teammates in raid.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Gameplay",
                    Order = 12
                },

                // =====================
                // SERVER (server.*)
                // =====================
                new ConfigField
                {
                    Key = "server.allowItemSending",
                    DisplayName = "Allow Item Sending",
                    Description = "Allow players to send items to each other.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Server",
                    Order = 1
                },
                new ConfigField
                {
                    Key = "server.itemSendingStorageTime",
                    DisplayName = "Item Sending Storage Time",
                    Description = "Number of days sent items are kept before being deleted.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 7,
                    Category = "Server",
                    Order = 2,
                    Unit = "days",
                    Validation = new ValidationRule { MinValue = 1, MaxValue = 365 }
                },
                new ConfigField
                {
                    Key = "server.sentItemsLoseFIR",
                    DisplayName = "Sent Items Lose FiR",
                    Description = "Sent items lose their Found in Raid status.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Server",
                    Order = 3
                },
                new ConfigField
                {
                    Key = "server.launcherListAllProfiles",
                    DisplayName = "Launcher Lists All Profiles",
                    Description = "Show all profiles in the launcher, not just the current user's.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Server",
                    Order = 4
                },
                new ConfigField
                {
                    Key = "server.sessionTimeout",
                    DisplayName = "Session Timeout",
                    Description = "Time in minutes before an inactive session is expired.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 5,
                    Category = "Server",
                    Order = 5,
                    Unit = "min",
                    Validation = new ValidationRule { MinValue = 1, MaxValue = 1440 }
                },
                new ConfigField
                {
                    Key = "server.showDevProfile",
                    DisplayName = "Show Dev Profile",
                    Description = "Show the developer profile option in the launcher.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Server",
                    Order = 6
                },
                new ConfigField
                {
                    Key = "server.showNonStandardProfile",
                    DisplayName = "Show Non-Standard Profile",
                    Description = "Show non-standard profile editions in the launcher.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Server",
                    Order = 7
                },
                new ConfigField
                {
                    Key = "server.SPT.disableSPTChatBots",
                    DisplayName = "Disable SPT Chat Bots",
                    Description = "Disable the built-in SPT chat bot messages.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Server",
                    Order = 8
                },
                new ConfigField
                {
                    Key = "server.apiKey",
                    DisplayName = "API Key",
                    Description = "Server API key for authentication. Auto-generated on first run.",
                    Type = ConfigFieldType.Password,
                    DefaultValue = "",
                    Category = "Server",
                    Order = 9,
                    Advanced = true
                },

                // =====================
                // NETWORK (server.SPT.http.* + natPunchServer.*)
                // =====================
                new ConfigField
                {
                    Key = "server.SPT.http.ip",
                    DisplayName = "Listen IP",
                    Description = "IP address the SPT server listens on. Use 0.0.0.0 for all interfaces.",
                    Type = ConfigFieldType.IpAddress,
                    DefaultValue = "0.0.0.0",
                    Category = "Network",
                    Order = 1
                },
                new ConfigField
                {
                    Key = "server.SPT.http.port",
                    DisplayName = "Listen Port",
                    Description = "Port the SPT server listens on.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 6969,
                    Category = "Network",
                    Order = 2
                },
                new ConfigField
                {
                    Key = "server.SPT.http.backendIp",
                    DisplayName = "Backend IP",
                    Description = "IP address for the backend API. Usually matches the listen IP.",
                    Type = ConfigFieldType.IpAddress,
                    DefaultValue = "0.0.0.0",
                    Category = "Network",
                    Order = 3
                },
                new ConfigField
                {
                    Key = "server.SPT.http.backendPort",
                    DisplayName = "Backend Port",
                    Description = "Port for the backend API. Usually matches the listen port.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 6969,
                    Category = "Network",
                    Order = 4
                },
                new ConfigField
                {
                    Key = "natPunchServer.enable",
                    DisplayName = "Enable NAT Punch",
                    Description = "Enable the NAT punch-through server for P2P connectivity.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Network",
                    Order = 5
                },
                new ConfigField
                {
                    Key = "natPunchServer.port",
                    DisplayName = "NAT Punch Port",
                    Description = "Port used by the NAT punch-through server.",
                    Type = ConfigFieldType.Port,
                    DefaultValue = 6790,
                    Category = "Network",
                    Order = 6,
                    ShowWhen = new Dictionary<string, object> { ["natPunchServer.enable"] = true }
                },

                // =====================
                // WEBHOOK (server.webhook.*)
                // =====================
                new ConfigField
                {
                    Key = "server.webhook.enabled",
                    DisplayName = "Enable Webhook",
                    Description = "Enable Discord webhook notifications for server events.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Webhook",
                    Order = 1
                },
                new ConfigField
                {
                    Key = "server.webhook.name",
                    DisplayName = "Webhook Name",
                    Description = "Display name for the webhook bot.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "Fika Server",
                    Category = "Webhook",
                    Order = 2,
                    ShowWhen = new Dictionary<string, object> { ["server.webhook.enabled"] = true }
                },
                new ConfigField
                {
                    Key = "server.webhook.avatarUrl",
                    DisplayName = "Avatar URL",
                    Description = "URL to the webhook bot's avatar image.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Webhook",
                    Order = 3,
                    Advanced = true,
                    ShowWhen = new Dictionary<string, object> { ["server.webhook.enabled"] = true }
                },
                new ConfigField
                {
                    Key = "server.webhook.url",
                    DisplayName = "Webhook URL",
                    Description = "Discord webhook URL to send notifications to.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Webhook",
                    Order = 4,
                    Placeholder = "https://discord.com/api/webhooks/...",
                    ShowWhen = new Dictionary<string, object> { ["server.webhook.enabled"] = true }
                },

                // =====================
                // HEADLESS (headless.*)
                // =====================
                new ConfigField
                {
                    Key = "headless.profiles.amount",
                    DisplayName = "Headless Profile Count",
                    Description = "Number of headless AI profiles to generate. Set to 0 to disable.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 0,
                    Category = "Headless",
                    Order = 1,
                    Validation = new ValidationRule { MinValue = 0, MaxValue = 50 }
                },
                new ConfigField
                {
                    Key = "headless.scripts.generate",
                    DisplayName = "Generate Scripts",
                    Description = "Auto-generate launch scripts for headless clients.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Headless",
                    Order = 2
                },
                new ConfigField
                {
                    Key = "headless.scripts.forceIp",
                    DisplayName = "Force IP",
                    Description = "IP address used in generated headless client scripts.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "https://127.0.0.1:6969",
                    Category = "Headless",
                    Order = 3,
                    Placeholder = "https://127.0.0.1:6969",
                    ShowWhen = new Dictionary<string, object> { ["headless.scripts.generate"] = true }
                },
                new ConfigField
                {
                    Key = "headless.setLevelToAverageOfLobby",
                    DisplayName = "Set Level to Lobby Average",
                    Description = "Set headless client levels to the average level of players in the lobby.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Headless",
                    Order = 4
                },
                new ConfigField
                {
                    Key = "headless.restartAfterAmountOfRaids",
                    DisplayName = "Restart After Raids",
                    Description = "Restart headless clients after this many raids. Set to 0 to disable.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 0,
                    Category = "Headless",
                    Order = 5,
                    Validation = new ValidationRule { MinValue = 0, MaxValue = 1000 }
                },

                // =====================
                // BACKGROUND (background.*)
                // =====================
                new ConfigField
                {
                    Key = "background.enable",
                    DisplayName = "Enable Background Tasks",
                    Description = "Enable background task processing on the server.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Background",
                    Order = 1
                }
            ]
        };
    }
}
