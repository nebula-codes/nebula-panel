using Google.Protobuf.WellKnownTypes;
using NebulaPanel.Agent.Shared.Protos;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.ValueObjects;

using DomainEnums = NebulaPanel.Domain.Enums;
using ProtoEnums = NebulaPanel.Agent.Shared.Protos;

namespace NebulaPanel.Agent.Shared.Mappers;

/// <summary>
/// Bidirectional mapping between proto ServerConfig and domain GameServer.
/// </summary>
public static class ServerConfigMapper
{
    // ─── Proto → Domain ───────────────────────────────────────

    /// <summary>
    /// Reconstructs a GameServer from a proto ServerConfig.
    /// Creates stub Game with executor-relevant fields only.
    /// </summary>
    public static GameServer ToGameServer(ServerConfig config)
    {
        var server = new GameServer
        {
            Id = Guid.Parse(config.ServerId),
            Name = config.Name,
            DeploymentType = ToDomainDeploymentType(config.DeploymentType),
            InstallPath = config.InstallPath,
            PrimaryPort = config.PrimaryPort,
            BindAddress = config.BindAddress,
            Status = ToDomainStatus(config.Status),
            ProcessId = config.ProcessId,
            OwnerId = Guid.Parse(config.OwnerId),
            DockerContainerId = config.DockerContainerId,
            InstalledVersion = config.InstalledVersion,
        };

        // Additional ports
        if (config.AdditionalPorts.Count > 0)
            server.AdditionalPorts = new Dictionary<string, int>(config.AdditionalPorts);

        // Docker config
        if (config.DockerConfig != null)
            server.DockerConfig = ToDomainDockerConfig(config.DockerConfig);

        // Native config
        if (config.NativeConfig != null)
            server.NativeConfig = ToDomainNativeConfig(config.NativeConfig);

        // RCON config
        if (config.RconConfig != null)
            server.RconConfig = ToDomainRconConfig(config.RconConfig);

        // Resource limits
        if (config.ResourceLimits != null)
            server.ResourceLimits = ToDomainResourceLimits(config.ResourceLimits);

        // Installed modpack
        if (config.InstalledModpack != null)
            server.InstalledModpack = ToDomainModpackInfo(config.InstalledModpack);

        // Hytale info
        if (config.HytaleInfo != null)
            server.HytaleInfo = ToDomainHytaleInfo(config.HytaleInfo);

        // Stub Game with executor-relevant fields
        if (config.Game != null)
        {
            server.Game = new Game
            {
                Id = Guid.Empty,
                Name = config.Game.Name,
                Slug = config.Game.Name.ToLowerInvariant().Replace(' ', '-'),
                ExecutableType = ToDomainExecutableType(config.Game.ExecutableType),
                DefaultExecutablePath = config.Game.DefaultExecutablePath,
                DefaultStartCommand = config.Game.DefaultStartCommand,
                DefaultStopCommand = config.Game.DefaultStopCommand,
                SupportsDocker = config.Game.SupportsDocker,
                DefaultDockerImage = config.Game.DefaultDockerImage,
                DockerDataPath = config.Game.DockerDataPath,
                DefaultPort = config.Game.DefaultPort,
                SteamAppId = config.Game.SteamAppId,
            };
        }

        return server;
    }

    // ─── Domain → Proto ───────────────────────────────────────

    /// <summary>
    /// Projects a GameServer to proto ServerConfig.
    /// Used by Manager when making RPC calls to Agent.
    /// </summary>
    public static ServerConfig FromGameServer(GameServer server)
    {
        var config = new ServerConfig
        {
            ServerId = server.Id.ToString(),
            Name = server.Name,
            DeploymentType = ToProtoDeploymentType(server.DeploymentType),
            InstallPath = server.InstallPath,
            PrimaryPort = server.PrimaryPort,
            BindAddress = server.BindAddress,
            Status = ToProtoStatus(server.Status),
            OwnerId = server.OwnerId.ToString(),
            ProcessId = server.ProcessId,
            DockerContainerId = server.DockerContainerId,
            InstalledVersion = server.InstalledVersion,
        };

        // Additional ports
        foreach (var kvp in server.AdditionalPorts)
            config.AdditionalPorts.Add(kvp.Key, kvp.Value);

        // Docker config
        if (server.DockerConfig != null)
            config.DockerConfig = FromDomainDockerConfig(server.DockerConfig);

        // Native config
        if (server.NativeConfig != null)
            config.NativeConfig = FromDomainNativeConfig(server.NativeConfig);

        // RCON config
        if (server.RconConfig != null)
            config.RconConfig = FromDomainRconConfig(server.RconConfig);

        // Resource limits
        if (server.ResourceLimits != null)
            config.ResourceLimits = FromDomainResourceLimits(server.ResourceLimits);

        // Installed modpack
        if (server.InstalledModpack != null)
            config.InstalledModpack = FromDomainModpackInfo(server.InstalledModpack);

        // Hytale info
        if (server.HytaleInfo != null)
            config.HytaleInfo = FromDomainHytaleInfo(server.HytaleInfo);

        // Game info (requires Game navigation property to be loaded)
        try
        {
            var game = server.Game;
            if (game is not null)
            {
                config.Game = new GameInfo
                {
                    Name = game.Name,
                    ExecutableType = ToProtoExecutableType(game.ExecutableType),
                    DefaultExecutablePath = game.DefaultExecutablePath,
                    DefaultStartCommand = game.DefaultStartCommand,
                    DefaultStopCommand = game.DefaultStopCommand,
                    SupportsDocker = game.SupportsDocker,
                    DefaultDockerImage = game.DefaultDockerImage,
                    DockerDataPath = game.DockerDataPath,
                    DefaultPort = game.DefaultPort,
                    SteamAppId = game.SteamAppId,
                };
            }
        }
        catch (InvalidOperationException)
        {
            // Game navigation property not loaded (EF lazy-load proxy) — skip
        }

        return config;
    }

    // ─── Docker Config ────────────────────────────────────────

    private static DockerConfiguration ToDomainDockerConfig(DockerConfig proto)
    {
        var config = new DockerConfiguration
        {
            Image = proto.Image,
            Tag = proto.Tag,
            RestartPolicy = ToDomainRestartPolicy(proto.RestartPolicy),
            Tty = proto.Tty,
            RunAsRoot = proto.RunAsRoot,
            CommandMode = ToDomainDockerCommandMode(proto.CommandMode),
            Network = proto.Network,
            WorkingDirectory = proto.WorkingDirectory,
            Command = proto.Command,
            Entrypoint = proto.Entrypoint,
        };

        if (proto.EnvironmentVariables.Count > 0)
            config.EnvironmentVariables = new Dictionary<string, string>(proto.EnvironmentVariables);

        foreach (var vol in proto.Volumes)
        {
            config.Volumes.Add(new Domain.ValueObjects.VolumeMount
            {
                HostPath = vol.HostPath,
                ContainerPath = vol.ContainerPath,
                ReadOnly = vol.ReadOnly,
                IsNamedVolume = vol.IsNamedVolume,
            });
        }

        foreach (var port in proto.Ports)
        {
            config.Ports.Add(new Domain.ValueObjects.PortMapping
            {
                HostPort = port.HostPort,
                ContainerPort = port.ContainerPort,
                Protocol = port.Protocol,
            });
        }

        if (proto.Limits != null) config.Limits = ToDomainResourceLimits(proto.Limits);

        return config;
    }

    private static DockerConfig FromDomainDockerConfig(DockerConfiguration domain)
    {
        var proto = new DockerConfig
        {
            Image = domain.Image,
            Tag = domain.Tag,
            RestartPolicy = ToProtoRestartPolicy(domain.RestartPolicy),
            Tty = domain.Tty,
            RunAsRoot = domain.RunAsRoot,
            CommandMode = ToProtoDockerCommandMode(domain.CommandMode),
            Network = domain.Network,
            WorkingDirectory = domain.WorkingDirectory,
            Command = domain.Command,
            Entrypoint = domain.Entrypoint,
        };

        foreach (var kvp in domain.EnvironmentVariables)
            proto.EnvironmentVariables.Add(kvp.Key, kvp.Value);

        foreach (var vol in domain.Volumes)
        {
            proto.Volumes.Add(new Protos.VolumeMount
            {
                HostPath = vol.HostPath,
                ContainerPath = vol.ContainerPath,
                ReadOnly = vol.ReadOnly,
                IsNamedVolume = vol.IsNamedVolume,
            });
        }

        foreach (var port in domain.Ports)
        {
            proto.Ports.Add(new Protos.PortMapping
            {
                HostPort = port.HostPort,
                ContainerPort = port.ContainerPort,
                Protocol = port.Protocol,
            });
        }

        if (domain.Limits != null) proto.Limits = FromDomainResourceLimits(domain.Limits);

        return proto;
    }

    // ─── Native Config ────────────────────────────────────────

    private static NativeConfiguration ToDomainNativeConfig(NativeConfig proto)
    {
        var config = new NativeConfiguration
        {
            WorkingDirectory = proto.WorkingDirectory,
            ExecutablePath = proto.ExecutablePath,
            Arguments = proto.Arguments,
            RunAsService = proto.RunAsService,
            UseStartupScript = proto.UseStartupScript,
            JavaPath = proto.JavaPath,
            JavaArguments = proto.JavaArguments,
            RunAsUser = proto.RunAsUser,
            StartupScriptPath = proto.StartupScriptPath,
        };

        if (proto.EnvironmentVariables.Count > 0)
            config.EnvironmentVariables = new Dictionary<string, string>(proto.EnvironmentVariables);

        return config;
    }

    private static NativeConfig FromDomainNativeConfig(NativeConfiguration domain)
    {
        var proto = new NativeConfig
        {
            WorkingDirectory = domain.WorkingDirectory,
            ExecutablePath = domain.ExecutablePath,
            Arguments = domain.Arguments,
            RunAsService = domain.RunAsService,
            UseStartupScript = domain.UseStartupScript,
            JavaPath = domain.JavaPath,
            JavaArguments = domain.JavaArguments,
            RunAsUser = domain.RunAsUser,
            StartupScriptPath = domain.StartupScriptPath,
        };

        foreach (var kvp in domain.EnvironmentVariables)
            proto.EnvironmentVariables.Add(kvp.Key, kvp.Value);

        return proto;
    }

    // ─── RCON Config ──────────────────────────────────────────

    private static RconConfiguration ToDomainRconConfig(RconConfig proto)
    {
        return new RconConfiguration
        {
            Enabled = proto.Enabled,
            Protocol = ToDomainRconProtocol(proto.Protocol),
            Host = proto.Host,
            Port = proto.Port,
            Password = proto.Password,
            UseWebSocket = proto.UseWebSocket,
            WebRconPath = proto.WebRconPath,
            TimeoutSeconds = proto.TimeoutSeconds,
        };
    }

    private static RconConfig FromDomainRconConfig(RconConfiguration domain)
    {
        return new RconConfig
        {
            Enabled = domain.Enabled,
            Protocol = ToProtoRconProtocol(domain.Protocol),
            Host = domain.Host,
            Port = domain.Port,
            Password = domain.Password,
            UseWebSocket = domain.UseWebSocket,
            WebRconPath = domain.WebRconPath,
            TimeoutSeconds = domain.TimeoutSeconds,
        };
    }

    // ─── Resource Limits ──────────────────────────────────────

    private static ResourceLimits ToDomainResourceLimits(ResourceLimitsConfig proto)
    {
        return new ResourceLimits
        {
            MaxMemoryMb = proto.MaxMemoryMb,
            MaxCpuPercent = proto.MaxCpuPercent,
            MaxDiskMb = proto.MaxDiskMb,
            MaxNetworkMbps = proto.MaxNetworkMbps,
        };
    }

    private static ResourceLimitsConfig FromDomainResourceLimits(ResourceLimits domain)
    {
        return new ResourceLimitsConfig
        {
            MaxMemoryMb = domain.MaxMemoryMb,
            MaxCpuPercent = domain.MaxCpuPercent,
            MaxDiskMb = domain.MaxDiskMb,
            MaxNetworkMbps = domain.MaxNetworkMbps,
        };
    }

    // ─── Modpack Info ─────────────────────────────────────────

    private static InstalledModpackInfo ToDomainModpackInfo(InstalledModpackConfig proto)
    {
        return new InstalledModpackInfo
        {
            Provider = ToDomainModpackProvider(proto.Provider),
            ModpackId = proto.ModpackId,
            ModpackName = proto.ModpackName,
            VersionId = proto.VersionId,
            VersionName = proto.VersionName,
            MinecraftVersion = proto.MinecraftVersion,
            LoaderType = ToDomainModLoaderType(proto.LoaderType),
            LoaderVersion = proto.LoaderVersion,
            ModCount = proto.ModCount,
            InstalledAt = proto.InstalledAt?.ToDateTime() ?? DateTime.UtcNow,
        };
    }

    private static InstalledModpackConfig FromDomainModpackInfo(InstalledModpackInfo domain)
    {
        return new InstalledModpackConfig
        {
            Provider = ToProtoModpackProvider(domain.Provider),
            ModpackId = domain.ModpackId,
            ModpackName = domain.ModpackName,
            VersionId = domain.VersionId,
            VersionName = domain.VersionName,
            MinecraftVersion = domain.MinecraftVersion,
            LoaderType = ToProtoModLoaderType(domain.LoaderType),
            LoaderVersion = domain.LoaderVersion,
            ModCount = domain.ModCount,
            InstalledAt = Timestamp.FromDateTime(DateTime.SpecifyKind(domain.InstalledAt, DateTimeKind.Utc)),
        };
    }

    // ─── Hytale Info ──────────────────────────────────────────

    private static HytaleServerInfo ToDomainHytaleInfo(HytaleInfoConfig proto)
    {
        return new HytaleServerInfo
        {
            InstalledVersion = proto.InstalledVersion,
            Patchline = proto.Patchline,
            InstalledAt = proto.InstalledAt?.ToDateTime() ?? DateTime.UtcNow,
        };
    }

    private static HytaleInfoConfig FromDomainHytaleInfo(HytaleServerInfo domain)
    {
        return new HytaleInfoConfig
        {
            InstalledVersion = domain.InstalledVersion,
            Patchline = domain.Patchline,
            InstalledAt = Timestamp.FromDateTime(DateTime.SpecifyKind(domain.InstalledAt, DateTimeKind.Utc)),
        };
    }

    // ─── ResourceUsage ────────────────────────────────────────

    public static ResourceUsageResponse FromDomainResourceUsage(ResourceUsage domain)
    {
        return new ResourceUsageResponse
        {
            CpuPercent = domain.CpuPercent,
            MemoryBytes = domain.MemoryBytes,
            VirtualMemoryBytes = domain.VirtualMemoryBytes,
            DiskReadBytes = domain.DiskReadBytes,
            DiskWriteBytes = domain.DiskWriteBytes,
            NetworkSentBytes = domain.NetworkSentBytes,
            NetworkReceivedBytes = domain.NetworkReceivedBytes,
            ThreadCount = domain.ThreadCount,
            HandleCount = domain.HandleCount,
            Uptime = Duration.FromTimeSpan(domain.Uptime),
            Timestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(domain.Timestamp, DateTimeKind.Utc)),
        };
    }

    public static ResourceUsage ToDomainResourceUsage(ResourceUsageResponse proto)
    {
        return new ResourceUsage
        {
            CpuPercent = proto.CpuPercent,
            MemoryBytes = proto.MemoryBytes,
            VirtualMemoryBytes = proto.VirtualMemoryBytes,
            DiskReadBytes = proto.DiskReadBytes,
            DiskWriteBytes = proto.DiskWriteBytes,
            NetworkSentBytes = proto.NetworkSentBytes,
            NetworkReceivedBytes = proto.NetworkReceivedBytes,
            ThreadCount = proto.ThreadCount,
            HandleCount = proto.HandleCount,
            Uptime = proto.Uptime?.ToTimeSpan() ?? TimeSpan.Zero,
            Timestamp = proto.Timestamp?.ToDateTime() ?? DateTime.UtcNow,
        };
    }

    // ─── Enum Converters ──────────────────────────────────────

    public static DomainEnums.ServerDeploymentType ToDomainDeploymentType(ProtoEnums.ServerDeploymentType proto) => proto switch
    {
        ProtoEnums.ServerDeploymentType.Docker => DomainEnums.ServerDeploymentType.Docker,
        ProtoEnums.ServerDeploymentType.Native => DomainEnums.ServerDeploymentType.Native,
        _ => DomainEnums.ServerDeploymentType.Native,
    };

    public static ProtoEnums.ServerDeploymentType ToProtoDeploymentType(DomainEnums.ServerDeploymentType domain) => domain switch
    {
        DomainEnums.ServerDeploymentType.Docker => ProtoEnums.ServerDeploymentType.Docker,
        DomainEnums.ServerDeploymentType.Native => ProtoEnums.ServerDeploymentType.Native,
        _ => ProtoEnums.ServerDeploymentType.Native,
    };

    public static DomainEnums.ServerStatus ToDomainStatus(ProtoEnums.ServerStatus proto) => proto switch
    {
        ProtoEnums.ServerStatus.Unknown => DomainEnums.ServerStatus.Unknown,
        ProtoEnums.ServerStatus.Stopped => DomainEnums.ServerStatus.Stopped,
        ProtoEnums.ServerStatus.Starting => DomainEnums.ServerStatus.Starting,
        ProtoEnums.ServerStatus.Running => DomainEnums.ServerStatus.Running,
        ProtoEnums.ServerStatus.Stopping => DomainEnums.ServerStatus.Stopping,
        ProtoEnums.ServerStatus.Crashed => DomainEnums.ServerStatus.Crashed,
        ProtoEnums.ServerStatus.Updating => DomainEnums.ServerStatus.Updating,
        ProtoEnums.ServerStatus.Installing => DomainEnums.ServerStatus.Installing,
        ProtoEnums.ServerStatus.Restarting => DomainEnums.ServerStatus.Restarting,
        _ => DomainEnums.ServerStatus.Unknown,
    };

    public static ProtoEnums.ServerStatus ToProtoStatus(DomainEnums.ServerStatus domain) => domain switch
    {
        DomainEnums.ServerStatus.Unknown => ProtoEnums.ServerStatus.Unknown,
        DomainEnums.ServerStatus.Stopped => ProtoEnums.ServerStatus.Stopped,
        DomainEnums.ServerStatus.Starting => ProtoEnums.ServerStatus.Starting,
        DomainEnums.ServerStatus.Running => ProtoEnums.ServerStatus.Running,
        DomainEnums.ServerStatus.Stopping => ProtoEnums.ServerStatus.Stopping,
        DomainEnums.ServerStatus.Crashed => ProtoEnums.ServerStatus.Crashed,
        DomainEnums.ServerStatus.Updating => ProtoEnums.ServerStatus.Updating,
        DomainEnums.ServerStatus.Installing => ProtoEnums.ServerStatus.Installing,
        DomainEnums.ServerStatus.Restarting => ProtoEnums.ServerStatus.Restarting,
        _ => ProtoEnums.ServerStatus.Unknown,
    };

    private static DomainEnums.ExecutableType ToDomainExecutableType(ProtoEnums.ExecutableType proto) => proto switch
    {
        ProtoEnums.ExecutableType.Exe => DomainEnums.ExecutableType.Exe,
        ProtoEnums.ExecutableType.Jar => DomainEnums.ExecutableType.Jar,
        ProtoEnums.ExecutableType.Shell => DomainEnums.ExecutableType.Shell,
        ProtoEnums.ExecutableType.Steamcmd => DomainEnums.ExecutableType.SteamCmd,
        _ => DomainEnums.ExecutableType.Exe,
    };

    private static ProtoEnums.ExecutableType ToProtoExecutableType(DomainEnums.ExecutableType domain) => domain switch
    {
        DomainEnums.ExecutableType.Exe => ProtoEnums.ExecutableType.Exe,
        DomainEnums.ExecutableType.Jar => ProtoEnums.ExecutableType.Jar,
        DomainEnums.ExecutableType.Shell => ProtoEnums.ExecutableType.Shell,
        DomainEnums.ExecutableType.SteamCmd => ProtoEnums.ExecutableType.Steamcmd,
        _ => ProtoEnums.ExecutableType.Exe,
    };

    private static Domain.ValueObjects.RestartPolicy ToDomainRestartPolicy(ProtoEnums.RestartPolicy proto) => proto switch
    {
        ProtoEnums.RestartPolicy.None => Domain.ValueObjects.RestartPolicy.None,
        ProtoEnums.RestartPolicy.Always => Domain.ValueObjects.RestartPolicy.Always,
        ProtoEnums.RestartPolicy.OnFailure => Domain.ValueObjects.RestartPolicy.OnFailure,
        ProtoEnums.RestartPolicy.UnlessStopped => Domain.ValueObjects.RestartPolicy.UnlessStopped,
        _ => Domain.ValueObjects.RestartPolicy.None,
    };

    private static ProtoEnums.RestartPolicy ToProtoRestartPolicy(Domain.ValueObjects.RestartPolicy domain) => domain switch
    {
        Domain.ValueObjects.RestartPolicy.None => ProtoEnums.RestartPolicy.None,
        Domain.ValueObjects.RestartPolicy.Always => ProtoEnums.RestartPolicy.Always,
        Domain.ValueObjects.RestartPolicy.OnFailure => ProtoEnums.RestartPolicy.OnFailure,
        Domain.ValueObjects.RestartPolicy.UnlessStopped => ProtoEnums.RestartPolicy.UnlessStopped,
        _ => ProtoEnums.RestartPolicy.None,
    };

    private static Domain.ValueObjects.DockerCommandMode ToDomainDockerCommandMode(ProtoEnums.DockerCommandMode proto) => proto switch
    {
        ProtoEnums.DockerCommandMode.Stdin => Domain.ValueObjects.DockerCommandMode.Stdin,
        ProtoEnums.DockerCommandMode.TmuxInject => Domain.ValueObjects.DockerCommandMode.TmuxInject,
        _ => Domain.ValueObjects.DockerCommandMode.Stdin,
    };

    private static ProtoEnums.DockerCommandMode ToProtoDockerCommandMode(Domain.ValueObjects.DockerCommandMode domain) => domain switch
    {
        Domain.ValueObjects.DockerCommandMode.Stdin => ProtoEnums.DockerCommandMode.Stdin,
        Domain.ValueObjects.DockerCommandMode.TmuxInject => ProtoEnums.DockerCommandMode.TmuxInject,
        _ => ProtoEnums.DockerCommandMode.Stdin,
    };

    private static DomainEnums.RconProtocolType ToDomainRconProtocol(ProtoEnums.RconProtocolType proto) => proto switch
    {
        ProtoEnums.RconProtocolType.Source => DomainEnums.RconProtocolType.Source,
        ProtoEnums.RconProtocolType.Minecraft => DomainEnums.RconProtocolType.Minecraft,
        ProtoEnums.RconProtocolType.Webrcon => DomainEnums.RconProtocolType.WebRcon,
        ProtoEnums.RconProtocolType.Battleye => DomainEnums.RconProtocolType.Battleye,
        ProtoEnums.RconProtocolType.Telnet => DomainEnums.RconProtocolType.Telnet,
        _ => DomainEnums.RconProtocolType.Source,
    };

    private static ProtoEnums.RconProtocolType ToProtoRconProtocol(DomainEnums.RconProtocolType domain) => domain switch
    {
        DomainEnums.RconProtocolType.Source => ProtoEnums.RconProtocolType.Source,
        DomainEnums.RconProtocolType.Minecraft => ProtoEnums.RconProtocolType.Minecraft,
        DomainEnums.RconProtocolType.WebRcon => ProtoEnums.RconProtocolType.Webrcon,
        DomainEnums.RconProtocolType.Battleye => ProtoEnums.RconProtocolType.Battleye,
        DomainEnums.RconProtocolType.Telnet => ProtoEnums.RconProtocolType.Telnet,
        _ => ProtoEnums.RconProtocolType.Source,
    };

    private static DomainEnums.ModpackProviderType ToDomainModpackProvider(ProtoEnums.ModpackProviderType proto) => proto switch
    {
        ProtoEnums.ModpackProviderType.Curseforge => DomainEnums.ModpackProviderType.CurseForge,
        ProtoEnums.ModpackProviderType.Modrinth => DomainEnums.ModpackProviderType.Modrinth,
        ProtoEnums.ModpackProviderType.Ftb => DomainEnums.ModpackProviderType.FTB,
        ProtoEnums.ModpackProviderType.Atlauncher => DomainEnums.ModpackProviderType.ATLauncher,
        ProtoEnums.ModpackProviderType.Technic => DomainEnums.ModpackProviderType.Technic,
        _ => DomainEnums.ModpackProviderType.CurseForge,
    };

    private static ProtoEnums.ModpackProviderType ToProtoModpackProvider(DomainEnums.ModpackProviderType domain) => domain switch
    {
        DomainEnums.ModpackProviderType.CurseForge => ProtoEnums.ModpackProviderType.Curseforge,
        DomainEnums.ModpackProviderType.Modrinth => ProtoEnums.ModpackProviderType.Modrinth,
        DomainEnums.ModpackProviderType.FTB => ProtoEnums.ModpackProviderType.Ftb,
        DomainEnums.ModpackProviderType.ATLauncher => ProtoEnums.ModpackProviderType.Atlauncher,
        DomainEnums.ModpackProviderType.Technic => ProtoEnums.ModpackProviderType.Technic,
        _ => ProtoEnums.ModpackProviderType.Curseforge,
    };

    private static DomainEnums.ModLoaderType ToDomainModLoaderType(ProtoEnums.ModLoaderType proto) => proto switch
    {
        ProtoEnums.ModLoaderType.None => DomainEnums.ModLoaderType.None,
        ProtoEnums.ModLoaderType.Forge => DomainEnums.ModLoaderType.Forge,
        ProtoEnums.ModLoaderType.Neoforge => DomainEnums.ModLoaderType.NeoForge,
        ProtoEnums.ModLoaderType.Fabric => DomainEnums.ModLoaderType.Fabric,
        ProtoEnums.ModLoaderType.Quilt => DomainEnums.ModLoaderType.Quilt,
        _ => DomainEnums.ModLoaderType.None,
    };

    private static ProtoEnums.ModLoaderType ToProtoModLoaderType(DomainEnums.ModLoaderType domain) => domain switch
    {
        DomainEnums.ModLoaderType.None => ProtoEnums.ModLoaderType.None,
        DomainEnums.ModLoaderType.Forge => ProtoEnums.ModLoaderType.Forge,
        DomainEnums.ModLoaderType.NeoForge => ProtoEnums.ModLoaderType.Neoforge,
        DomainEnums.ModLoaderType.Fabric => ProtoEnums.ModLoaderType.Fabric,
        DomainEnums.ModLoaderType.Quilt => ProtoEnums.ModLoaderType.Quilt,
        _ => ProtoEnums.ModLoaderType.None,
    };
}
