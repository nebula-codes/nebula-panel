using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service providing preset configurations for popular Steam game servers.
/// </summary>
public class SteamGamePresetService : ISteamGamePresetService
{
    private static readonly List<SteamGamePreset> Presets =
    [
        // Survival Games
        new SteamGamePreset
        {
            Name = "Rust",
            Slug = "rust",
            ServerAppId = "258550",
            ClientAppId = "252490",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "RustDedicated.exe",
            DefaultStartCommand = "-batchmode +server.port 28015 +server.hostname \"My Rust Server\" +server.maxplayers 100 +server.worldsize 4000 +server.seed 12345",
            DefaultStopCommand = "quit",
            DefaultPort = 28015,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.WebRcon,
                DefaultPort = 28016,
                WebRconPath = "/"
            },
            SupportsDocker = true,
            DefaultDockerImage = "didstopia/rust-server",
            DockerDataPath = "/steamcmd/rust",
            SupportsWorkshop = true,
            ModInstallPath = "oxide/plugins",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/252490/capsule_231x87.jpg",
            Description = "Survival game where you must gather resources and build a base while fighting against other players and the environment.",
            Category = "Survival",
            SetupNotes = "Requires Oxide/uMod for plugin support. WebRcon enabled by default on port +1."
        },
        new SteamGamePreset
        {
            Name = "Valheim",
            Slug = "valheim",
            ServerAppId = "896660",
            ClientAppId = "892970",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "valheim_server.exe",
            DefaultStartCommand = "-nographics -batchmode -port 2456 -name \"My Valheim Server\" -world \"MyWorld\" -password \"changeme\"",
            DefaultStopCommand = null,
            DefaultPort = 2456,
            RconPreset = null,
            SupportsDocker = true,
            DefaultDockerImage = "lloesche/valheim-server",
            DockerDataPath = "/config",
            SupportsWorkshop = false,
            ModInstallPath = "BepInEx/plugins",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/892970/capsule_231x87.jpg",
            Description = "Viking survival and exploration game supporting up to 10 players in co-op.",
            Category = "Survival",
            SetupNotes = "Requires BepInEx for mod support. Server uses 3 consecutive ports starting from primary port."
        },
        new SteamGamePreset
        {
            Name = "ARK: Survival Evolved",
            Slug = "ark-survival-evolved",
            ServerAppId = "376030",
            ClientAppId = "346110",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "ShooterGame/Binaries/Win64/ShooterGameServer.exe",
            DefaultStartCommand = "TheIsland?listen?SessionName=MyARKServer?MaxPlayers=70 -server -log",
            DefaultStopCommand = "DoExit",
            DefaultPort = 7777,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.Source,
                DefaultPort = 27020
            },
            SupportsDocker = true,
            DefaultDockerImage = "hermsi/ark-server",
            DockerDataPath = "/ark",
            SupportsWorkshop = true,
            ModInstallPath = "ShooterGame/Content/Mods",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/346110/capsule_231x87.jpg",
            Description = "Dinosaur survival game with taming, building, and multiplayer support.",
            Category = "Survival",
            SetupNotes = "High memory requirements (~8GB minimum). Map can be changed in start command."
        },
        new SteamGamePreset
        {
            Name = "ARK: Survival Ascended",
            Slug = "ark-survival-ascended",
            ServerAppId = "2430930",
            ClientAppId = "2399830",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "ShooterGame/Binaries/Win64/ArkAscendedServer.exe",
            DefaultStartCommand = "TheIsland_WP?listen?SessionName=MyASAServer?MaxPlayers=70 -server -log",
            DefaultStopCommand = "DoExit",
            DefaultPort = 7777,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.Source,
                DefaultPort = 27020
            },
            SupportsDocker = false,
            SupportsWorkshop = true,
            ModInstallPath = "ShooterGame/Content/Mods",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/2399830/capsule_231x87.jpg",
            Description = "Remastered version of ARK using Unreal Engine 5.",
            Category = "Survival",
            SetupNotes = "Very high system requirements. Requires 32GB+ RAM."
        },
        new SteamGamePreset
        {
            Name = "Project Zomboid",
            Slug = "project-zomboid",
            ServerAppId = "380870",
            ClientAppId = "108600",
            ExecutableType = ExecutableType.Shell,
            DefaultExecutablePath = "start-server.sh",
            DefaultStartCommand = "-servername MyServer",
            DefaultStopCommand = "quit",
            DefaultPort = 16261,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.Source,
                DefaultPort = 16262
            },
            SupportsDocker = true,
            DefaultDockerImage = "renegademaster/zomboid-dedicated-server",
            DockerDataPath = "/home/steam/ZomboidDedicatedServer",
            SupportsWorkshop = true,
            ModInstallPath = "steamapps/workshop/content/108600",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/108600/capsule_231x87.jpg",
            Description = "Zombie survival RPG with deep survival mechanics and multiplayer.",
            Category = "Survival",
            SetupNotes = "Configure mods via servertest.ini. Workshop mods auto-download on client connect."
        },
        new SteamGamePreset
        {
            Name = "7 Days to Die",
            Slug = "7-days-to-die",
            ServerAppId = "294420",
            ClientAppId = "251570",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "7DaysToDieServer.exe",
            DefaultStartCommand = "-configfile=serverconfig.xml -logfile=output_log.txt -quit -batchmode -nographics -dedicated",
            DefaultStopCommand = "shutdown",
            DefaultPort = 26900,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.Source,
                DefaultPort = 8081
            },
            SupportsDocker = true,
            DefaultDockerImage = "vinanrra/7dtd-server",
            DockerDataPath = "/home/sdtdserver",
            SupportsWorkshop = false,
            ModInstallPath = "Mods",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/251570/capsule_231x87.jpg",
            Description = "Zombie survival with base building, crafting, and horde defense.",
            Category = "Survival",
            SetupNotes = "Uses Telnet for remote commands. Configure via serverconfig.xml."
        },

        // Sandbox/Building Games
        new SteamGamePreset
        {
            Name = "Terraria",
            Slug = "terraria",
            ServerAppId = "105600",
            ClientAppId = "105600",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "TerrariaServer.exe",
            DefaultStartCommand = "-config serverconfig.txt",
            DefaultStopCommand = "exit",
            DefaultPort = 7777,
            RconPreset = null,
            SupportsDocker = true,
            DefaultDockerImage = "ryshe/terraria",
            DockerDataPath = "/world",
            SupportsWorkshop = true,
            ModInstallPath = "tModLoader/Mods",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/105600/capsule_231x87.jpg",
            Description = "2D sandbox adventure game with exploration, crafting, and combat.",
            Category = "Sandbox",
            SetupNotes = "For mods, use tModLoader version. Configure via serverconfig.txt."
        },
        new SteamGamePreset
        {
            Name = "Satisfactory",
            Slug = "satisfactory",
            ServerAppId = "1690800",
            ClientAppId = "526870",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "FactoryServer.exe",
            DefaultStartCommand = "-multihome=0.0.0.0 -ServerQueryPort=15777 -BeaconPort=15000 -Port=7777",
            DefaultStopCommand = null,
            DefaultPort = 7777,
            RconPreset = null,
            SupportsDocker = true,
            DefaultDockerImage = "wolveix/satisfactory-server",
            DockerDataPath = "/config",
            SupportsWorkshop = false,
            ModInstallPath = "FactoryGame/Mods",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/526870/capsule_231x87.jpg",
            Description = "First-person factory building game set on an alien planet.",
            Category = "Sandbox",
            SetupNotes = "Requires multiple ports. High memory usage for large factories."
        },

        // Vampire/Fantasy Survival
        new SteamGamePreset
        {
            Name = "V Rising",
            Slug = "v-rising",
            ServerAppId = "1829350",
            ClientAppId = "1604030",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "VRisingServer.exe",
            DefaultStartCommand = "-persistentDataPath save-data -serverName \"My V Rising Server\" -saveName \"world1\"",
            DefaultStopCommand = null,
            DefaultPort = 9876,
            RconPreset = null,
            SupportsDocker = true,
            DefaultDockerImage = "trueosiris/vrising",
            DockerDataPath = "/mnt/vrising/server",
            SupportsWorkshop = false,
            ModInstallPath = "BepInEx/plugins",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/1604030/capsule_231x87.jpg",
            Description = "Vampire survival game with castle building and PvP/PvE gameplay.",
            Category = "Survival",
            SetupNotes = "Configure via ServerGameSettings.json and ServerHostSettings.json."
        },
        new SteamGamePreset
        {
            Name = "Palworld",
            Slug = "palworld",
            ServerAppId = "2394010",
            ClientAppId = "1623730",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "Pal/Binaries/Win64/PalServer-Win64-Test-Cmd.exe",
            DefaultStartCommand = "-port=8211 -players=32",
            DefaultStopCommand = null,
            DefaultPort = 8211,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.Source,
                DefaultPort = 25575
            },
            SupportsDocker = true,
            DefaultDockerImage = "thijsvanloef/palworld-server-docker",
            DockerDataPath = "/palworld",
            SupportsWorkshop = false,
            ModInstallPath = "Pal/Content/Paks/~mods",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/1623730/capsule_231x87.jpg",
            Description = "Open-world survival crafting game with creature companions.",
            Category = "Survival",
            SetupNotes = "Configure via PalWorldSettings.ini. RCON must be enabled in settings."
        },
        new SteamGamePreset
        {
            Name = "Enshrouded",
            Slug = "enshrouded",
            ServerAppId = "2278520",
            ClientAppId = "1203620",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "enshrouded_server.exe",
            DefaultStartCommand = "",
            DefaultStopCommand = null,
            DefaultPort = 15636,
            RconPreset = null,
            SupportsDocker = true,
            DefaultDockerImage = "sknnr/enshrouded-dedicated-server",
            DockerDataPath = "/home/steam/enshrouded",
            SupportsWorkshop = false,
            ModInstallPath = null,
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/1203620/capsule_231x87.jpg",
            Description = "Survival action RPG with base building and co-op gameplay.",
            Category = "Survival",
            SetupNotes = "Configure via enshrouded_server.json. Up to 16 players supported."
        },

        // Competitive/FPS Games
        new SteamGamePreset
        {
            Name = "Counter-Strike 2",
            Slug = "counter-strike-2",
            ServerAppId = "730",
            ClientAppId = "730",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "game/bin/win64/cs2.exe",
            DefaultStartCommand = "-dedicated +map de_dust2 +maxplayers 10",
            DefaultStopCommand = "quit",
            DefaultPort = 27015,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.Source,
                DefaultPort = 27015
            },
            SupportsDocker = true,
            DefaultDockerImage = "joedwards32/cs2",
            DockerDataPath = "/home/steam/cs2-dedicated",
            SupportsWorkshop = true,
            ModInstallPath = "game/csgo/addons",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/730/capsule_231x87.jpg",
            Description = "Competitive tactical shooter with various game modes.",
            Category = "FPS",
            SetupNotes = "Requires Steam Game Server Login Token (GSLT) for public servers."
        },
        new SteamGamePreset
        {
            Name = "Team Fortress 2",
            Slug = "team-fortress-2",
            ServerAppId = "232250",
            ClientAppId = "440",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "srcds.exe",
            DefaultStartCommand = "-game tf +map ctf_2fort +maxplayers 24",
            DefaultStopCommand = "quit",
            DefaultPort = 27015,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.Source,
                DefaultPort = 27015
            },
            SupportsDocker = true,
            DefaultDockerImage = "cm2network/tf2",
            DockerDataPath = "/home/steam/tf2-dedicated",
            SupportsWorkshop = true,
            ModInstallPath = "tf/addons",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/440/capsule_231x87.jpg",
            Description = "Class-based team shooter with various game modes.",
            Category = "FPS",
            SetupNotes = "Uses SourceMod for plugins. GSLT recommended for public servers."
        },
        new SteamGamePreset
        {
            Name = "Garry's Mod",
            Slug = "garrys-mod",
            ServerAppId = "4020",
            ClientAppId = "4000",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "srcds.exe",
            DefaultStartCommand = "-game garrysmod +gamemode sandbox +map gm_construct +maxplayers 16",
            DefaultStopCommand = "quit",
            DefaultPort = 27015,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.Source,
                DefaultPort = 27015
            },
            SupportsDocker = true,
            DefaultDockerImage = "cm2network/gmod",
            DockerDataPath = "/home/steam/gmod-dedicated",
            SupportsWorkshop = true,
            ModInstallPath = "garrysmod/addons",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/4000/capsule_231x87.jpg",
            Description = "Sandbox physics game with extensive mod and gamemode support.",
            Category = "Sandbox",
            SetupNotes = "Extensive Workshop support. Collection IDs can be specified in start command."
        },

        // Space/Sci-Fi Games
        new SteamGamePreset
        {
            Name = "Space Engineers",
            Slug = "space-engineers",
            ServerAppId = "298740",
            ClientAppId = "244850",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "DedicatedServer64/SpaceEngineersDedicated.exe",
            DefaultStartCommand = "-console -noconsole",
            DefaultStopCommand = null,
            DefaultPort = 27016,
            RconPreset = null,
            SupportsDocker = true,
            DefaultDockerImage = "devidian/spaceengineers",
            DockerDataPath = "/appdata",
            SupportsWorkshop = true,
            ModInstallPath = "SpaceEngineers/Mods",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/244850/capsule_231x87.jpg",
            Description = "Space sandbox game with voxel-based building and engineering.",
            Category = "Sandbox",
            SetupNotes = "Configure via SpaceEngineers-Dedicated.cfg. High memory requirements."
        },
        new SteamGamePreset
        {
            Name = "Starbound",
            Slug = "starbound",
            ServerAppId = "211820",
            ClientAppId = "211820",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "win64/starbound_server.exe",
            DefaultStartCommand = "",
            DefaultStopCommand = null,
            DefaultPort = 21025,
            RconPreset = null,
            SupportsDocker = true,
            DefaultDockerImage = "didstopia/starbound-server",
            DockerDataPath = "/steamcmd/starbound",
            SupportsWorkshop = true,
            ModInstallPath = "mods",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/211820/capsule_231x87.jpg",
            Description = "2D space sandbox adventure with exploration and building.",
            Category = "Sandbox",
            SetupNotes = "Configure via starbound_server.config. Workshop mods are auto-synced to clients."
        },

        // Racing/Vehicle Games
        new SteamGamePreset
        {
            Name = "Assetto Corsa",
            Slug = "assetto-corsa",
            ServerAppId = "302550",
            ClientAppId = "244210",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "acServer.exe",
            DefaultStartCommand = "",
            DefaultStopCommand = null,
            DefaultPort = 9600,
            RconPreset = null,
            SupportsDocker = true,
            DefaultDockerImage = "compujuckel/assettoserver",
            DockerDataPath = "/assettoserver",
            SupportsWorkshop = false,
            ModInstallPath = "content",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/244210/capsule_231x87.jpg",
            Description = "Realistic racing simulator with extensive mod support.",
            Category = "Racing",
            SetupNotes = "Configure via server_cfg.ini and entry_list.ini. Mods must be manually installed."
        },
        new SteamGamePreset
        {
            Name = "BeamNG.drive",
            Slug = "beamng-drive",
            ServerAppId = "284160",
            ClientAppId = "284160",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "Bin64/BeamMP-Server.exe",
            DefaultStartCommand = "",
            DefaultStopCommand = null,
            DefaultPort = 30814,
            RconPreset = null,
            SupportsDocker = true,
            DefaultDockerImage = "rouhim/beammp-server",
            DockerDataPath = "/beammp",
            SupportsWorkshop = false,
            ModInstallPath = "Resources/Client",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/284160/capsule_231x87.jpg",
            Description = "Soft-body physics driving game with multiplayer via BeamMP.",
            Category = "Racing",
            SetupNotes = "Requires BeamMP mod for multiplayer. Get auth key from beammp.com."
        },

        // Simulation Games
        new SteamGamePreset
        {
            Name = "Factorio",
            Slug = "factorio",
            ServerAppId = "427520",
            ClientAppId = "427520",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "bin/x64/factorio.exe",
            DefaultStartCommand = "--start-server saves/my-save.zip --server-settings server-settings.json",
            DefaultStopCommand = "/quit",
            DefaultPort = 34197,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.Source,
                DefaultPort = 27015
            },
            SupportsDocker = true,
            DefaultDockerImage = "factoriotools/factorio",
            DockerDataPath = "/factorio",
            SupportsWorkshop = false,
            ModInstallPath = "mods",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/427520/capsule_231x87.jpg",
            Description = "Factory building and automation game with complex logistics.",
            Category = "Simulation",
            SetupNotes = "Mods available from mods.factorio.com. Configure via server-settings.json."
        },
        new SteamGamePreset
        {
            Name = "Barotrauma",
            Slug = "barotrauma",
            ServerAppId = "1026340",
            ClientAppId = "602960",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "DedicatedServer.exe",
            DefaultStartCommand = "",
            DefaultStopCommand = "quit",
            DefaultPort = 27015,
            RconPreset = null,
            SupportsDocker = true,
            DefaultDockerImage = "cm2network/barotrauma",
            DockerDataPath = "/home/steam/barotrauma-dedicated",
            SupportsWorkshop = true,
            ModInstallPath = "LocalMods",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/602960/capsule_231x87.jpg",
            Description = "Submarine survival game set in an alien ocean.",
            Category = "Simulation",
            SetupNotes = "Configure via serversettings.xml. Workshop mods work automatically."
        },
        new SteamGamePreset
        {
            Name = "Unturned",
            Slug = "unturned",
            ServerAppId = "1110390",
            ClientAppId = "304930",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "Unturned.exe",
            DefaultStartCommand = "-batchmode -nographics +secureserver/MyServer",
            DefaultStopCommand = "shutdown",
            DefaultPort = 27015,
            RconPreset = new SteamGameRconPreset
            {
                Supported = true,
                Protocol = RconProtocolType.Source,
                DefaultPort = 27015
            },
            SupportsDocker = true,
            DefaultDockerImage = "cm2network/unturned",
            DockerDataPath = "/home/steam/unturned-dedicated",
            SupportsWorkshop = true,
            ModInstallPath = "Workshop",
            IconUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/304930/capsule_231x87.jpg",
            Description = "Free-to-play zombie survival game with crafting and building.",
            Category = "Survival",
            SetupNotes = "Configure via Commands.dat. Workshop mods via WorkshopDownloadConfig.json."
        }
    ];

    private static readonly Dictionary<string, SteamGamePreset> PresetsByAppId =
        Presets.ToDictionary(p => p.ServerAppId, p => p);

    private static readonly Dictionary<string, SteamGamePreset> PresetsBySlug =
        Presets.ToDictionary(p => p.Slug, p => p, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyList<SteamGamePreset> GetAllPresets() => Presets;

    /// <inheritdoc />
    public SteamGamePreset? GetPresetByAppId(string appId) =>
        PresetsByAppId.GetValueOrDefault(appId);

    /// <inheritdoc />
    public SteamGamePreset? GetPresetBySlug(string slug) =>
        PresetsBySlug.GetValueOrDefault(slug);

    /// <inheritdoc />
    public IReadOnlyList<SteamGamePreset> SearchPresets(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Presets;
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Presets
            .Where(p => terms.All(term =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Slug.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)))
            .ToList();
    }
}
