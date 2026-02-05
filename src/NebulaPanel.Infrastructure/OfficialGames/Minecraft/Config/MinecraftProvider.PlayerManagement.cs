using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Partial class containing player management configuration schemas.
/// Includes: eula.txt, whitelist.json, ops.json, banned-players.json, banned-ips.json
/// </summary>
public partial class MinecraftProvider
{
    /// <summary>
    /// Adds player management configuration schemas.
    /// </summary>
    static partial void AddPlayerManagementSchemas(Dictionary<string, ConfigurationSchema> schemas)
    {
        // =====================
        // EULA.TXT
        // =====================
        schemas["eula.txt"] = new ConfigurationSchema
        {
            FileName = "eula.txt",
            FileType = ConfigFileType.Properties,
            Fields =
            [
                new ConfigField
                {
                    Key = "eula",
                    DisplayName = "Accept Minecraft EULA",
                    Description = "You must accept the Minecraft End User License Agreement (https://aka.ms/MinecraftEULA) to run the server. By setting this to true, you acknowledge that you have read and agree to the EULA.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "EULA"
                }
            ]
        };

        // =====================
        // WHITELIST.JSON
        // =====================
        schemas["whitelist.json"] = new ConfigurationSchema
        {
            FileName = "whitelist.json",
            FileType = ConfigFileType.Json,
            Fields =
            [
                new ConfigField
                {
                    Key = "players",
                    DisplayName = "Whitelisted Players",
                    Description = "List of players allowed to join when whitelist is enabled. Each entry requires a player UUID and name. Use /whitelist add <player> in-game or RCON to add players.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "[]",
                    Category = "Players"
                }
            ]
        };

        // =====================
        // OPS.JSON
        // =====================
        schemas["ops.json"] = new ConfigurationSchema
        {
            FileName = "ops.json",
            FileType = ConfigFileType.Json,
            Fields =
            [
                new ConfigField
                {
                    Key = "operators",
                    DisplayName = "Server Operators",
                    Description = "List of players with operator permissions. Each entry includes UUID, name, permission level (1-4), and whether they bypass player limit. Use /op <player> in-game or RCON to add operators.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "[]",
                    Category = "Operators"
                },
                new ConfigField
                {
                    Key = "_info_level_1",
                    DisplayName = "Level 1 Info",
                    Description = "Level 1: Can bypass spawn protection",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Permission Levels (Reference)"
                },
                new ConfigField
                {
                    Key = "_info_level_2",
                    DisplayName = "Level 2 Info",
                    Description = "Level 2: Can use /clear, /difficulty, /effect, /gamemode, /gamerule, /give, /tp, /setblock, and singleplayer cheats",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Permission Levels (Reference)"
                },
                new ConfigField
                {
                    Key = "_info_level_3",
                    DisplayName = "Level 3 Info",
                    Description = "Level 3: Can use /ban, /deop, /kick, /op, /whitelist",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Permission Levels (Reference)"
                },
                new ConfigField
                {
                    Key = "_info_level_4",
                    DisplayName = "Level 4 Info",
                    Description = "Level 4: Can use /stop and all commands",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Permission Levels (Reference)"
                }
            ]
        };

        // =====================
        // BANNED-PLAYERS.JSON
        // =====================
        schemas["banned-players.json"] = new ConfigurationSchema
        {
            FileName = "banned-players.json",
            FileType = ConfigFileType.Json,
            Fields =
            [
                new ConfigField
                {
                    Key = "banned",
                    DisplayName = "Banned Players",
                    Description = "List of players banned from the server. Each entry includes UUID, name, creation date, source, expiration, and reason. Use /ban <player> [reason] in-game or RCON to ban players.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "[]",
                    Category = "Bans"
                }
            ]
        };

        // =====================
        // BANNED-IPS.JSON
        // =====================
        schemas["banned-ips.json"] = new ConfigurationSchema
        {
            FileName = "banned-ips.json",
            FileType = ConfigFileType.Json,
            Fields =
            [
                new ConfigField
                {
                    Key = "banned",
                    DisplayName = "Banned IP Addresses",
                    Description = "List of IP addresses banned from connecting. Each entry includes IP, creation date, source, expiration, and reason. Use /ban-ip <ip|player> [reason] in-game or RCON to ban IPs.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "[]",
                    Category = "IP Bans"
                }
            ]
        };
    }
}
