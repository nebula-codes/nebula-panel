using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.ArkSurvivalAscended;

/// <summary>
/// Partial class containing the Game.ini configuration schema for ASA.
/// </summary>
public partial class ArkSurvivalAscendedProvider
{
    /// <summary>
    /// Adds the Game.ini configuration schema with ASA game mode settings.
    /// </summary>
    static partial void AddGameIniSchema(Dictionary<string, ConfigurationSchema> schemas)
    {
        schemas["ShooterGame/Saved/Config/WindowsServer/Game.ini"] = new ConfigurationSchema
        {
            FileName = "ShooterGame/Saved/Config/WindowsServer/Game.ini",
            DisplayName = "Game Settings (Game.ini)",
            Description = "Game mode settings for Ark: Survival Ascended",
            FileType = ConfigFileType.Ini,
            CreateIfMissing = true,
            Sections =
            [
                new ConfigSection
                {
                    Key = "ShooterGameMode",
                    DisplayName = "Game Mode Settings",
                    Description = "Engrams, tribes, and breeding settings",
                    Icon = "gamepad",
                    Order = 1,
                    DefaultExpanded = true
                }
            ],
            Fields =
            [
                // =====================
                // ENGRAMS & TRIBES
                // =====================
                new ConfigField
                {
                    Key = "bAutoUnlockAllEngrams",
                    DisplayName = "Auto-Unlock All Engrams",
                    Description = "Automatically unlock all engrams for players as they level up.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "ShooterGameMode",

                    Order = 1
                },
                new ConfigField
                {
                    Key = "MaxNumberOfPlayersInTribe",
                    DisplayName = "Max Players Per Tribe",
                    Description = "Maximum number of players allowed in a single tribe. 0 = unlimited.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 0,
                    SliderOptions = new SliderOptions { Min = 0, Max = 500, Step = 1 },
                    Category = "ShooterGameMode",

                    Order = 2
                },

                // =====================
                // BREEDING
                // =====================
                new ConfigField
                {
                    Key = "BabyImprintingStatScaleMultiplier",
                    DisplayName = "Baby Imprinting Stat Scale",
                    Description = "Multiplier for stat bonuses from imprinting babies.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.1, Max = 10.0, Step = 0.1 },
                    Category = "ShooterGameMode",

                    Order = 3
                },
                new ConfigField
                {
                    Key = "BabyCuddleIntervalMultiplier",
                    DisplayName = "Baby Cuddle Interval",
                    Description = "Multiplier for time between baby cuddle requests. Lower = more frequent.",
                    Type = ConfigFieldType.Slider,
                    DefaultValue = 1.0,
                    SliderOptions = new SliderOptions { Min = 0.01, Max = 10.0, Step = 0.01 },
                    Category = "ShooterGameMode",

                    Order = 4
                }
            ]
        };
    }
}
