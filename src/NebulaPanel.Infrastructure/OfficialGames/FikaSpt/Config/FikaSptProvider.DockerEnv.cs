using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.FikaSpt;

/// <summary>
/// Partial class containing the Docker environment variable configuration schema for FIKA SPT.
/// </summary>
public partial class FikaSptProvider
{
    static partial void AddDockerEnvSchema(Dictionary<string, ConfigurationSchema> schemas)
    {
        schemas["docker-env"] = new ConfigurationSchema
        {
            FileName = "docker-env",
            DisplayName = "Docker Environment",
            Description = "Docker container environment variables for FIKA SPT server",
            FileType = ConfigFileType.Properties,
            CreateIfMissing = false,
            Sections =
            [
                new ConfigSection
                {
                    Key = "Core",
                    DisplayName = "Core Settings",
                    Description = "Essential FIKA SPT server settings",
                    Icon = "server",
                    Order = 1,
                    DefaultExpanded = true
                },
                new ConfigSection
                {
                    Key = "Updates",
                    DisplayName = "Auto-Update Settings",
                    Description = "Automatic update behavior for SPT and FIKA",
                    Icon = "arrow-path",
                    Order = 2,
                    DefaultExpanded = true
                },
                new ConfigSection
                {
                    Key = "Advanced",
                    DisplayName = "Advanced Settings",
                    Description = "Advanced container and server configuration",
                    Icon = "cog",
                    Order = 3,
                    DefaultExpanded = false
                }
            ],
            Fields =
            [
                // =====================
                // CORE SETTINGS
                // =====================
                new ConfigField
                {
                    Key = "INSTALL_FIKA",
                    DisplayName = "Install FIKA",
                    Description = "Enable FIKA multiplayer mod installation. Required for multiplayer functionality.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Core",
                    Order = 1,
                    Required = true
                },
                new ConfigField
                {
                    Key = "LISTEN_ALL_NETWORKS",
                    DisplayName = "Listen on All Networks",
                    Description = "Configure the server to listen on all network interfaces (0.0.0.0). Required for external connections.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Core",
                    Order = 2
                },
                new ConfigField
                {
                    Key = "ENABLE_PROFILE_BACKUP",
                    DisplayName = "Enable Profile Backups",
                    Description = "Enable daily automatic profile backups at 00:00 UTC.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Core",
                    Order = 3
                },
                new ConfigField
                {
                    Key = "TZ",
                    DisplayName = "Timezone",
                    Description = "Container timezone for backup scheduling and logs.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "UTC",
                    Category = "Core",
                    Order = 4,
                    Placeholder = "e.g., America/New_York, Europe/London"
                },

                // =====================
                // UPDATE SETTINGS
                // =====================
                new ConfigField
                {
                    Key = "AUTO_UPDATE_SPT",
                    DisplayName = "Auto-Update SPT",
                    Description = "Automatically update SPT when a version mismatch is detected.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Updates",
                    Order = 1
                },
                new ConfigField
                {
                    Key = "AUTO_UPDATE_FIKA",
                    DisplayName = "Auto-Update FIKA",
                    Description = "Automatically update the FIKA server mod to the latest compatible version. Required for initial FIKA installation.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Updates",
                    Order = 2
                },
                new ConfigField
                {
                    Key = "INSTALL_OTHER_MODS",
                    DisplayName = "Install Additional Mods",
                    Description = "Download and install additional mods from the mod URL list on startup.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = false,
                    Category = "Updates",
                    Order = 3
                },
                new ConfigField
                {
                    Key = "FIKA_VERSION",
                    DisplayName = "FIKA Version Override",
                    Description = "Override the FIKA server mod version. Leave empty for the latest compatible version.",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Updates",
                    Order = 4,
                    Advanced = true,
                    Placeholder = "e.g., 2.2.0 (empty = latest)"
                },
                new ConfigField
                {
                    Key = "FORCE_SPT_VERSION",
                    DisplayName = "Force SPT Version",
                    Description = "Force a specific SPT version. Format: major.minor.patch-build-hash (e.g., 4.0.1-40087-1eacf0f).",
                    Type = ConfigFieldType.String,
                    DefaultValue = "",
                    Category = "Updates",
                    Order = 5,
                    Advanced = true,
                    Placeholder = "e.g., 4.0.1-40087-1eacf0f (empty = default)"
                },

                // =====================
                // ADVANCED SETTINGS
                // =====================
                new ConfigField
                {
                    Key = "UID",
                    DisplayName = "User ID",
                    Description = "Linux user ID for running the server process inside the container.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1000,
                    Category = "Advanced",
                    Order = 1,
                    Validation = new ValidationRule { MinValue = 0, MaxValue = 65534 }
                },
                new ConfigField
                {
                    Key = "GID",
                    DisplayName = "Group ID",
                    Description = "Linux group ID for file ownership inside the container.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 1000,
                    Category = "Advanced",
                    Order = 2,
                    Validation = new ValidationRule { MinValue = 0, MaxValue = 65534 }
                },
                new ConfigField
                {
                    Key = "TAKE_OWNERSHIP",
                    DisplayName = "Take File Ownership",
                    Description = "Whether the container should take ownership of server files. Disable if managing permissions externally.",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true,
                    Category = "Advanced",
                    Order = 3
                },
                new ConfigField
                {
                    Key = "NUM_HEADLESS_PROFILES",
                    DisplayName = "Headless AI Profiles",
                    Description = "Number of headless AI profiles to generate. Requires FIKA to be installed.",
                    Type = ConfigFieldType.Int,
                    DefaultValue = 0,
                    Category = "Advanced",
                    Order = 4,
                    Validation = new ValidationRule { MinValue = 0, MaxValue = 50 },
                    ShowWhen = new Dictionary<string, object> { ["INSTALL_FIKA"] = true }
                }
            ]
        };
    }
}
