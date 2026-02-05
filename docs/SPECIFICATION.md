# Nebula Panel - Game Server Manager
## Technical Specification & Development Prompt

---

## Project Overview

**Nebula Panel** is a comprehensive, self-hosted game server management platform designed to orchestrate multiple game servers across various games. It provides a unified interface for server deployment, configuration, monitoring, and maintenance with support for both containerized (Docker) and native process execution.

### Core Philosophy
- **Unified Management**: One panel to manage servers across different games, engines, and deployment methods
- **Flexibility First**: Support Steam-based games, Java servers, native executables, and custom configurations
- **Resource Awareness**: Real-time monitoring at both host and per-server levels
- **Multi-Tenancy**: Role-based access control for teams and communities
- **Developer Experience**: Clean architecture, maintainable code, and extensible design

---

## Technology Stack

### Backend
| Component | Technology | Purpose |
|-----------|------------|---------|
| Framework | **.NET 10** | Core application framework |
| UI Framework | **Blazor Server/WebAssembly (Hybrid)** | Interactive web UI with C# |
| Real-time Communication | **SignalR** | Live console streaming, metrics updates, notifications |
| Container Management | **Docker.DotNet** | Docker container lifecycle management |
| Process Management | **System.Diagnostics.Process** | Native process control for non-Docker servers |
| Hardware Monitoring | **LibreHardwareMonitor** | Host system metrics (CPU, RAM, GPU, temps, disk) |
| Database | **SQLite** (default) or **PostgreSQL** | Primary data store |
| Caching | **In-Memory** (default) or **Redis** | Session management, real-time metrics buffering |
| Background Jobs | **Hangfire** or **Quartz.NET** | Scheduled tasks, updates, backups |
| Authentication | **ASP.NET Core Identity** + **JWT** | User auth with optional OAuth providers |

### Frontend
| Component | Technology | Purpose |
|-----------|------------|---------|
| CSS Framework | **Tailwind CSS 4.x** | Utility-first styling |
| Component Library | **Custom Blazor Components** | Reusable UI elements |
| Data Grids | **Radzen Blazor** or **MudBlazor DataGrid** | Advanced tables with sort/filter/search |
| Icons | **Lucide** or **Heroicons** | Consistent iconography |
| Charts | **ApexCharts.Blazor** or **Chart.js** via interop | Resource visualization |
| Theming | **CSS Custom Properties + Tailwind Config** | Centralized, extensible themes |

### Infrastructure
| Component | Technology | Purpose |
|-----------|------------|---------|
| Containerization | **Docker** | Optional server isolation |
| Reverse Proxy | **YARP** (in-app) or **Traefik** | Dynamic routing for game server web UIs |
| File Storage | **Local filesystem** with abstraction | Server files, backups, mods |
| Logging | **Serilog** with **Seq** or **Loki** sink | Structured logging |

---

## Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              NEBULA PANEL                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐              │
│  │   Blazor UI     │  │   SignalR Hub   │  │    REST API     │              │
│  │  (Components)   │  │  (Real-time)    │  │   (External)    │              │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘              │
│           │                    │                    │                        │
│  ┌────────┴────────────────────┴────────────────────┴────────┐              │
│  │                    Application Services                    │              │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐      │              │
│  │  │  Server  │ │   User   │ │   Mod    │ │  Monitor │      │              │
│  │  │ Manager  │ │ Manager  │ │ Manager  │ │  Service │      │              │
│  │  └──────────┘ └──────────┘ └──────────┘ └──────────┘      │              │
│  └───────────────────────────┬────────────────────────────────┘              │
│                              │                                               │
│  ┌───────────────────────────┴────────────────────────────────┐              │
│  │                    Infrastructure Layer                     │              │
│  │  ┌────────────┐ ┌────────────┐ ┌────────────┐              │              │
│  │  │   Docker   │ │  Process   │ │   Steam    │              │              │
│  │  │  Adapter   │ │  Adapter   │ │  Adapter   │              │              │
│  │  └────────────┘ └────────────┘ └────────────┘              │              │
│  └────────────────────────────────────────────────────────────┘              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        ▼                           ▼                           ▼
┌───────────────┐          ┌───────────────┐          ┌───────────────┐
│    Docker     │          │    Native     │          │   SteamCMD    │
│  Containers   │          │   Processes   │          │   Downloads   │
└───────────────┘          └───────────────┘          └───────────────┘
```

### Project Structure

```
src/
├── NebulaPanel.Domain/                 # Core domain entities and interfaces
│   ├── Entities/
│   │   ├── Game.cs
│   │   ├── GameServer.cs
│   │   ├── ServerConfiguration.cs
│   │   ├── User.cs
│   │   ├── Role.cs
│   │   ├── Permission.cs
│   │   ├── Mod.cs
│   │   └── ResourceSnapshot.cs
│   ├── Enums/
│   │   ├── ServerStatus.cs
│   │   ├── ServerType.cs              # Docker, Native, SteamCMD
│   │   └── ExecutableType.cs          # Jar, Exe, Shell
│   ├── Interfaces/
│   │   ├── IServerExecutor.cs
│   │   ├── IResourceMonitor.cs
│   │   └── IModProvider.cs
│   └── Events/
│       ├── ServerStartedEvent.cs
│       └── ServerStoppedEvent.cs
│
├── NebulaPanel.Application/            # Business logic and use cases
│   ├── Services/
│   │   ├── GameServerService.cs
│   │   ├── UserService.cs
│   │   ├── ModManagementService.cs
│   │   ├── BackupService.cs
│   │   └── ScheduledTaskService.cs
│   ├── DTOs/
│   ├── Validators/
│   └── Mappings/
│
├── NebulaPanel.Infrastructure/         # External concerns implementation
│   ├── Persistence/
│   │   ├── NebulaPanelDbContext.cs
│   │   ├── Configurations/            # EF Core configurations
│   │   └── Repositories/
│   ├── Executors/
│   │   ├── DockerServerExecutor.cs
│   │   ├── NativeProcessExecutor.cs
│   │   └── SteamCmdExecutor.cs
│   ├── Monitoring/
│   │   ├── HostResourceMonitor.cs
│   │   ├── DockerResourceMonitor.cs
│   │   └── ProcessResourceMonitor.cs
│   ├── ModProviders/
│   │   ├── SteamWorkshopProvider.cs
│   │   ├── CurseForgeProvider.cs
│   │   └── LocalModProvider.cs
│   └── FileManagement/
│       └── ServerFileManager.cs
│
├── NebulaPanel.Web/                    # Blazor application
│   ├── Components/
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor
│   │   │   ├── NavMenu.razor
│   │   │   └── TopBar.razor
│   │   ├── Pages/
│   │   │   ├── Dashboard.razor
│   │   │   ├── Servers/
│   │   │   │   ├── ServerList.razor
│   │   │   │   ├── ServerDetail.razor
│   │   │   │   ├── ServerConsole.razor
│   │   │   │   └── ServerConfig.razor
│   │   │   ├── Games/
│   │   │   ├── Users/
│   │   │   ├── Mods/
│   │   │   └── Settings/
│   │   ├── Shared/
│   │   │   ├── DataGrid.razor
│   │   │   ├── ResourceGauge.razor
│   │   │   ├── ConsoleViewer.razor
│   │   │   ├── FileExplorer.razor
│   │   │   └── Modal.razor
│   │   └── Theming/
│   │       ├── ThemeProvider.razor
│   │       └── ThemeSwitcher.razor
│   ├── Hubs/
│   │   ├── ConsoleHub.cs
│   │   └── MetricsHub.cs
│   ├── wwwroot/
│   │   ├── css/
│   │   │   ├── app.css
│   │   │   └── themes/
│   │   │       ├── nebula-dark.css
│   │   │       └── nebula-light.css
│   │   └── js/
│   └── Program.cs
│
├── NebulaPanel.Shared/                 # Shared models for client/server
│   ├── Models/
│   └── Constants/
│
└── tests/
    ├── NebulaPanel.Domain.Tests/
    ├── NebulaPanel.Application.Tests/
    └── NebulaPanel.Integration.Tests/
```

---

## Core Features Specification

### 1. Game Management

Games serve as templates and organizational containers for game servers.

```csharp
public class Game
{
    public Guid Id { get; set; }
    public string Name { get; set; }                    // "Minecraft", "Rust", "Valheim"
    public string Slug { get; set; }                    // "minecraft", "rust", "valheim"
    public string? SteamAppId { get; set; }             // For Steam-based games
    public ExecutableType ExecutableType { get; set; }  // Jar, Exe, Shell
    public string DefaultExecutablePath { get; set; }   // Relative path to executable
    public string DefaultStartCommand { get; set; }     // Start command template
    public string? DefaultStopCommand { get; set; }     // Graceful stop command (RCON, etc.)
    public bool SupportsDocker { get; set; }
    public string? DefaultDockerImage { get; set; }
    public string? IconPath { get; set; }
    
    // Mod support - multiple providers per game
    public bool SupportsMods { get; set; }
    public List<ModProviderConfiguration> ModProviders { get; set; } = new();
    
    // RCON defaults for this game
    public RconDefaults? RconDefaults { get; set; }
    
    // Configuration file schemas (multiple files supported)
    public Dictionary<string, ConfigurationSchema> ConfigurationSchemas { get; set; }
    
    public ICollection<GameServer> Servers { get; set; }
}

/// <summary>
/// Configuration for a mod provider on a specific game.
/// Games can have multiple providers (e.g., Minecraft with Modrinth + CurseForge).
/// </summary>
public class ModProviderConfiguration
{
    public ModProviderType Provider { get; set; }
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }                   // Lower = higher priority in unified search
    public string? GameSlug { get; set; }               // Provider-specific game identifier
    public string? GameVersion { get; set; }            // Default game version filter
    public string ModInstallPath { get; set; }          // Relative path: "mods/", "plugins/", etc.
    public Dictionary<string, string> ProviderSettings { get; set; } = new(); // Provider-specific config
}

/// <summary>
/// Default RCON settings for a game. Individual servers can override these.
/// </summary>
public class RconDefaults
{
    public bool DefaultEnabled { get; set; }
    public RconProtocolType Protocol { get; set; }
    public int DefaultPort { get; set; }
    public bool UseWebSocket { get; set; }
    public string? WebRconPath { get; set; }
}

public enum ExecutableType
{
    Exe,        // Windows executable
    Jar,        // Java JAR file
    Shell,      // Shell script (Linux)
    SteamCmd    // Managed via SteamCMD
}

public enum ModProviderType
{
    Local,          // Manual file management only
    SteamWorkshop,  // Steam Workshop integration
    CurseForge,     // CurseForge API
    Modrinth,       // Modrinth API (Minecraft)
    Thunderstore,   // Thunderstore (Valheim, Lethal Company, etc.)
    SpigotMC,       // SpigotMC resources (Minecraft plugins)
    Hangar,         // PaperMC Hangar (Minecraft plugins)
    NexusMods       // Nexus Mods (various games)
}
```

#### Game Configuration Schema

Each game defines its configuration schema for dynamic form generation:

```csharp
public class ConfigurationSchema
{
    public string FileName { get; set; }           // "server.properties", "serverconfig.json"
    public ConfigFileType FileType { get; set; }   // Properties, Json, Yaml, Ini, Custom
    public List<ConfigField> Fields { get; set; }
}

public class ConfigField
{
    public string Key { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public ConfigFieldType Type { get; set; }      // String, Int, Bool, Select, MultiSelect
    public object? DefaultValue { get; set; }
    public List<SelectOption>? Options { get; set; }
    public ValidationRule? Validation { get; set; }
    public string? Category { get; set; }          // For grouping in UI
}
```

### 2. Game Server Management

Each game server is an instance of a game with its own configuration and lifecycle.

```csharp
public class GameServer
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; }
    
    // Deployment Configuration
    public ServerDeploymentType DeploymentType { get; set; }  // Docker, Native
    public string InstallPath { get; set; }                    // Base path for server files
    public string? DockerContainerId { get; set; }
    public DockerConfiguration? DockerConfig { get; set; }
    public NativeConfiguration? NativeConfig { get; set; }
    
    // RCON Configuration (per-server override of game defaults)
    public RconConfiguration? RconConfig { get; set; }
    
    // Network Configuration
    public int PrimaryPort { get; set; }
    public Dictionary<string, int> AdditionalPorts { get; set; }  // "rcon": 25575, "query": 25565
    public string BindAddress { get; set; } = "0.0.0.0";
    
    // Runtime State (not persisted, or cached in Redis)
    public ServerStatus Status { get; set; }
    public DateTime? LastStarted { get; set; }
    public DateTime? LastStopped { get; set; }
    public int? ProcessId { get; set; }
    
    // Resource Limits
    public ResourceLimits ResourceLimits { get; set; }
    
    // Relationships
    public Guid OwnerId { get; set; }
    public User Owner { get; set; }
    public ICollection<ServerMod> InstalledMods { get; set; }
    public ICollection<ScheduledTask> ScheduledTasks { get; set; }
    public ICollection<Backup> Backups { get; set; }
    
    // Helper to determine command sending method
    public CommandMethod PreferredCommandMethod => RconConfig?.Enabled == true 
        ? CommandMethod.Rcon 
        : CommandMethod.Stdin;
}

public enum CommandMethod
{
    Stdin,      // Send via process stdin
    Rcon,       // Send via RCON protocol
    WebApi      // Send via game's HTTP API (rare)
}

public enum ServerDeploymentType
{
    Docker,
    Native
}

public enum ServerStatus
{
    Unknown,
    Stopped,
    Starting,
    Running,
    Stopping,
    Crashed,
    Updating,
    Installing
}

public class DockerConfiguration
{
    public string Image { get; set; }
    public string? Tag { get; set; } = "latest";
    public Dictionary<string, string> EnvironmentVariables { get; set; }
    public List<VolumeMount> Volumes { get; set; }
    public List<PortMapping> Ports { get; set; }
    public string? Network { get; set; }
    public ResourceLimits? Limits { get; set; }
    public RestartPolicy RestartPolicy { get; set; }
}

public class NativeConfiguration
{
    public string WorkingDirectory { get; set; }
    public string ExecutablePath { get; set; }
    public string Arguments { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; }
    public string? JavaPath { get; set; }              // For JAR files
    public string? JavaArguments { get; set; }         // -Xmx4G -Xms2G
    public bool RunAsService { get; set; }
    public string? RunAsUser { get; set; }             // Linux user to run as
}

public class ResourceLimits
{
    public int? MaxMemoryMb { get; set; }
    public int? MaxCpuPercent { get; set; }
    public int? MaxDiskMb { get; set; }
    public int? MaxNetworkMbps { get; set; }
}
```

### 3. Server Executor Interface

Abstraction for different server execution strategies:

```csharp
public interface IServerExecutor
{
    ServerDeploymentType DeploymentType { get; }
    
    Task<bool> InstallAsync(GameServer server, IProgress<InstallProgress>? progress = null, 
                            CancellationToken ct = default);
    Task<bool> UpdateAsync(GameServer server, IProgress<UpdateProgress>? progress = null,
                           CancellationToken ct = default);
    Task<bool> StartAsync(GameServer server, CancellationToken ct = default);
    Task<bool> StopAsync(GameServer server, bool force = false, CancellationToken ct = default);
    Task<bool> RestartAsync(GameServer server, CancellationToken ct = default);
    Task<ServerStatus> GetStatusAsync(GameServer server, CancellationToken ct = default);
    
    IAsyncEnumerable<string> StreamConsoleAsync(GameServer server, CancellationToken ct = default);
    Task SendCommandAsync(GameServer server, string command, CancellationToken ct = default);
    
    Task<ResourceUsage> GetResourceUsageAsync(GameServer server, CancellationToken ct = default);
}
```

#### Docker Executor Implementation Notes

```csharp
public class DockerServerExecutor : IServerExecutor
{
    private readonly DockerClient _docker;
    
    public async Task<bool> StartAsync(GameServer server, CancellationToken ct = default)
    {
        var config = server.DockerConfig!;
        
        // Check if container exists
        if (!string.IsNullOrEmpty(server.DockerContainerId))
        {
            await _docker.Containers.StartContainerAsync(server.DockerContainerId, null, ct);
            return true;
        }
        
        // Create new container
        var createParams = new CreateContainerParameters
        {
            Image = $"{config.Image}:{config.Tag}",
            Name = $"nebula-{server.Id}",
            Env = config.EnvironmentVariables.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
            HostConfig = new HostConfig
            {
                PortBindings = BuildPortBindings(config.Ports),
                Binds = config.Volumes.Select(v => $"{v.HostPath}:{v.ContainerPath}:{v.Mode}").ToList(),
                Memory = config.Limits?.MaxMemoryMb * 1024 * 1024,
                CPUPercent = config.Limits?.MaxCpuPercent,
                RestartPolicy = new RestartPolicy { Name = config.RestartPolicy.ToString() }
            }
        };
        
        var response = await _docker.Containers.CreateContainerAsync(createParams, ct);
        server.DockerContainerId = response.ID;
        
        await _docker.Containers.StartContainerAsync(response.ID, null, ct);
        return true;
    }
    
    public async IAsyncEnumerable<string> StreamConsoleAsync(GameServer server, 
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var logParams = new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Follow = true,
            Tail = "100"
        };
        
        using var stream = await _docker.Containers.GetContainerLogsAsync(
            server.DockerContainerId!, logParams, ct);
        
        // Parse multiplexed stream and yield lines
        await foreach (var line in ParseDockerLogStreamAsync(stream, ct))
        {
            yield return line;
        }
    }
}
```

#### Native Process Executor Implementation Notes

```csharp
public class NativeProcessExecutor : IServerExecutor
{
    public async Task<bool> StartAsync(GameServer server, CancellationToken ct = default)
    {
        var config = server.NativeConfig!;
        
        var startInfo = new ProcessStartInfo
        {
            FileName = DetermineExecutable(server),
            Arguments = BuildArguments(server),
            WorkingDirectory = config.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        
        foreach (var env in config.EnvironmentVariables)
        {
            startInfo.EnvironmentVariables[env.Key] = env.Value;
        }
        
        var process = Process.Start(startInfo);
        server.ProcessId = process?.Id;
        
        // Start console output capture
        _ = CaptureOutputAsync(server, process!, ct);
        
        return process != null;
    }
    
    private string DetermineExecutable(GameServer server)
    {
        var game = server.Game;
        var config = server.NativeConfig!;
        
        return game.ExecutableType switch
        {
            ExecutableType.Jar => config.JavaPath ?? "java",
            ExecutableType.Exe => Path.Combine(config.WorkingDirectory, config.ExecutablePath),
            ExecutableType.Shell => "/bin/bash",
            _ => config.ExecutablePath
        };
    }
    
    private string BuildArguments(GameServer server)
    {
        var game = server.Game;
        var config = server.NativeConfig!;
        
        return game.ExecutableType switch
        {
            ExecutableType.Jar => $"{config.JavaArguments} -jar {config.ExecutablePath} {config.Arguments}",
            ExecutableType.Shell => $"-c \"{config.ExecutablePath} {config.Arguments}\"",
            _ => config.Arguments
        };
    }
}
```

### 4. RCON Integration

Many game servers support RCON (Remote Console) for sending commands without stdin access. This is especially important for Docker containers or when the console stream isn't available.

```csharp
public interface IRconClient : IAsyncDisposable
{
    RconProtocolType Protocol { get; }
    bool IsConnected { get; }
    
    Task<bool> ConnectAsync(string host, int port, string password, CancellationToken ct = default);
    Task DisconnectAsync();
    Task<RconResponse> SendCommandAsync(string command, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}

public enum RconProtocolType
{
    Source,         // Source Engine RCON (Rust, CS2, Garry's Mod, ARK, etc.)
    Minecraft,      // Minecraft RCON (slightly different packet structure)
    WebRcon,        // HTTP-based RCON (Rust WebRCON, some custom implementations)
    Battleye,       // BattlEye RCon (DayZ, Arma)
    Custom          // Game-specific implementations
}

public record RconResponse
{
    public bool Success { get; init; }
    public string? Response { get; init; }
    public string? Error { get; init; }
    public TimeSpan Latency { get; init; }
}

public class RconConfiguration
{
    public bool Enabled { get; set; }
    public RconProtocolType Protocol { get; set; }
    public int Port { get; set; }
    public string Password { get; set; } = string.Empty;
    public bool UseWebSocket { get; set; }              // For WebRCON
    public string? WebRconPath { get; set; }            // e.g., "/rcon" for HTTP endpoints
    public int TimeoutSeconds { get; set; } = 10;
    public int ReconnectAttempts { get; set; } = 3;
}
```

#### Source RCON Implementation

```csharp
public class SourceRconClient : IRconClient
{
    public RconProtocolType Protocol => RconProtocolType.Source;
    
    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _requestId;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    
    public async Task<bool> ConnectAsync(string host, int port, string password, 
        CancellationToken ct = default)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(host, port, ct);
        _stream = _client.GetStream();
        
        // Send auth packet
        var authResponse = await SendPacketAsync(
            RconPacketType.Auth, 
            password, 
            ct);
        
        return authResponse.Id != -1; // -1 indicates auth failure
    }
    
    public async Task<RconResponse> SendCommandAsync(string command, CancellationToken ct = default)
    {
        if (_stream == null || !IsConnected)
            return new RconResponse { Success = false, Error = "Not connected" };
        
        var sw = Stopwatch.StartNew();
        
        await _sendLock.WaitAsync(ct);
        try
        {
            var response = await SendPacketAsync(RconPacketType.ExecCommand, command, ct);
            sw.Stop();
            
            return new RconResponse
            {
                Success = true,
                Response = response.Body,
                Latency = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new RconResponse { Success = false, Error = ex.Message };
        }
        finally
        {
            _sendLock.Release();
        }
    }
    
    private async Task<RconPacket> SendPacketAsync(RconPacketType type, string body, 
        CancellationToken ct)
    {
        var requestId = Interlocked.Increment(ref _requestId);
        var packet = new RconPacket(requestId, type, body);
        
        await _stream!.WriteAsync(packet.ToBytes(), ct);
        await _stream.FlushAsync(ct);
        
        return await ReadPacketAsync(ct);
    }
    
    private async Task<RconPacket> ReadPacketAsync(CancellationToken ct)
    {
        var sizeBuffer = new byte[4];
        await _stream!.ReadExactlyAsync(sizeBuffer, ct);
        var size = BitConverter.ToInt32(sizeBuffer);
        
        var bodyBuffer = new byte[size];
        await _stream.ReadExactlyAsync(bodyBuffer, ct);
        
        return RconPacket.Parse(bodyBuffer);
    }
    
    // Packet structure for Source RCON
    private record RconPacket(int Id, RconPacketType Type, string Body)
    {
        public byte[] ToBytes()
        {
            var bodyBytes = Encoding.UTF8.GetBytes(Body);
            var size = 4 + 4 + bodyBytes.Length + 2; // id + type + body + null terminators
            
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            
            bw.Write(size);
            bw.Write(Id);
            bw.Write((int)Type);
            bw.Write(bodyBytes);
            bw.Write((byte)0);
            bw.Write((byte)0);
            
            return ms.ToArray();
        }
        
        public static RconPacket Parse(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            
            var id = br.ReadInt32();
            var type = (RconPacketType)br.ReadInt32();
            var bodyLength = data.Length - 10; // Subtract id, type, and null terminators
            var body = Encoding.UTF8.GetString(br.ReadBytes(bodyLength));
            
            return new RconPacket(id, type, body.TrimEnd('\0'));
        }
    }
    
    private enum RconPacketType
    {
        Auth = 3,
        AuthResponse = 2,
        ExecCommand = 2,
        ResponseValue = 0
    }
}
```

#### RCON Provider Factory

```csharp
public interface IRconClientFactory
{
    IRconClient Create(RconProtocolType protocol);
    IRconClient CreateForServer(GameServer server);
}

public class RconClientFactory : IRconClientFactory
{
    public IRconClient Create(RconProtocolType protocol) => protocol switch
    {
        RconProtocolType.Source => new SourceRconClient(),
        RconProtocolType.Minecraft => new MinecraftRconClient(),
        RconProtocolType.WebRcon => new WebRconClient(),
        RconProtocolType.Battleye => new BattleyeRconClient(),
        _ => throw new NotSupportedException($"RCON protocol {protocol} is not supported")
    };
    
    public IRconClient CreateForServer(GameServer server)
    {
        var config = server.RconConfig;
        if (config == null || !config.Enabled)
            throw new InvalidOperationException("RCON is not configured for this server");
        
        return Create(config.Protocol);
    }
}
```

#### Updated Server Executor with RCON Support

```csharp
public interface IServerExecutor
{
    // ... existing methods ...
    
    // Enhanced command sending with method selection
    Task SendCommandAsync(GameServer server, string command, 
        CommandMethod? method = null, CancellationToken ct = default);
    
    // RCON-specific operations
    Task<RconResponse> SendRconCommandAsync(GameServer server, string command, 
        CancellationToken ct = default);
    Task<bool> TestRconConnectionAsync(GameServer server, CancellationToken ct = default);
}
```

---

### 5. SteamCMD Integration

```csharp
public class SteamCmdExecutor
{
    private readonly string _steamCmdPath;
    private readonly string _installBasePath;
    
    public async Task<bool> InstallOrUpdateGameAsync(
        string appId, 
        string installDir,
        string? branch = null,
        string? betaPassword = null,
        IProgress<SteamCmdProgress>? progress = null,
        CancellationToken ct = default)
    {
        var arguments = BuildSteamCmdArguments(appId, installDir, branch, betaPassword);
        
        var startInfo = new ProcessStartInfo
        {
            FileName = _steamCmdPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        
        using var process = Process.Start(startInfo)!;
        
        await foreach (var line in ReadLinesAsync(process.StandardOutput, ct))
        {
            var parsedProgress = ParseSteamCmdOutput(line);
            if (parsedProgress != null)
            {
                progress?.Report(parsedProgress);
            }
        }
        
        await process.WaitForExitAsync(ct);
        return process.ExitCode == 0;
    }
    
    private string BuildSteamCmdArguments(string appId, string installDir, 
        string? branch, string? betaPassword)
    {
        var sb = new StringBuilder();
        sb.Append("+force_install_dir ").Append(installDir).Append(' ');
        sb.Append("+login anonymous ");
        sb.Append("+app_update ").Append(appId);
        
        if (!string.IsNullOrEmpty(branch))
        {
            sb.Append(" -beta ").Append(branch);
            if (!string.IsNullOrEmpty(betaPassword))
            {
                sb.Append(" -betapassword ").Append(betaPassword);
            }
        }
        
        sb.Append(" validate +quit");
        return sb.ToString();
    }
}
```

### 5. Resource Monitoring

#### Host Resource Monitor (LibreHardwareMonitor)

```csharp
public class HostResourceMonitor : IHostResourceMonitor, IDisposable
{
    private readonly Computer _computer;
    private readonly Timer _updateTimer;
    
    public HostResourceMonitor()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true
        };
        _computer.Open();
        
        _updateTimer = new Timer(UpdateMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }
    
    public HostMetrics GetCurrentMetrics()
    {
        var metrics = new HostMetrics
        {
            Timestamp = DateTime.UtcNow,
            Cpu = GetCpuMetrics(),
            Memory = GetMemoryMetrics(),
            Gpu = GetGpuMetrics(),
            Storage = GetStorageMetrics(),
            Network = GetNetworkMetrics()
        };
        
        return metrics;
    }
    
    private CpuMetrics GetCpuMetrics()
    {
        var cpuHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        
        cpuHardware?.Update();
        
        return new CpuMetrics
        {
            Name = cpuHardware?.Name ?? "Unknown",
            UsagePercent = GetSensorValue(cpuHardware, SensorType.Load, "CPU Total"),
            Temperature = GetSensorValue(cpuHardware, SensorType.Temperature, "CPU Package"),
            CoreCount = Environment.ProcessorCount,
            PerCoreUsage = GetPerCoreUsage(cpuHardware)
        };
    }
    
    // Similar implementations for Memory, GPU, Storage, Network...
}

public record HostMetrics
{
    public DateTime Timestamp { get; init; }
    public CpuMetrics Cpu { get; init; }
    public MemoryMetrics Memory { get; init; }
    public GpuMetrics? Gpu { get; init; }
    public List<StorageMetrics> Storage { get; init; }
    public NetworkMetrics Network { get; init; }
}
```

#### Per-Server Resource Monitoring

```csharp
public interface IServerResourceMonitor
{
    Task<ServerResourceUsage> GetUsageAsync(GameServer server, CancellationToken ct = default);
    IAsyncEnumerable<ServerResourceUsage> StreamUsageAsync(GameServer server, 
        TimeSpan interval, CancellationToken ct = default);
}

public class ServerResourceMonitor : IServerResourceMonitor
{
    private readonly DockerClient _docker;
    
    public async Task<ServerResourceUsage> GetUsageAsync(GameServer server, CancellationToken ct)
    {
        return server.DeploymentType switch
        {
            ServerDeploymentType.Docker => await GetDockerUsageAsync(server, ct),
            ServerDeploymentType.Native => await GetProcessUsageAsync(server, ct),
            _ => throw new NotSupportedException()
        };
    }
    
    private async Task<ServerResourceUsage> GetDockerUsageAsync(GameServer server, CancellationToken ct)
    {
        var stats = await _docker.Containers.GetContainerStatsAsync(
            server.DockerContainerId!, 
            new ContainerStatsParameters { Stream = false }, 
            ct);
        
        // Parse Docker stats into ServerResourceUsage
        return new ServerResourceUsage
        {
            ServerId = server.Id,
            Timestamp = DateTime.UtcNow,
            CpuPercent = CalculateDockerCpuPercent(stats),
            MemoryUsedMb = stats.MemoryStats.Usage / (1024 * 1024),
            MemoryLimitMb = stats.MemoryStats.Limit / (1024 * 1024),
            NetworkRxBytes = stats.Networks?.Sum(n => (long)n.Value.RxBytes) ?? 0,
            NetworkTxBytes = stats.Networks?.Sum(n => (long)n.Value.TxBytes) ?? 0
        };
    }
    
    private async Task<ServerResourceUsage> GetProcessUsageAsync(GameServer server, CancellationToken ct)
    {
        if (server.ProcessId == null) 
            return ServerResourceUsage.Empty(server.Id);
        
        try
        {
            var process = Process.GetProcessById(server.ProcessId.Value);
            
            return new ServerResourceUsage
            {
                ServerId = server.Id,
                Timestamp = DateTime.UtcNow,
                CpuPercent = await CalculateProcessCpuAsync(process),
                MemoryUsedMb = process.WorkingSet64 / (1024 * 1024),
                MemoryLimitMb = server.ResourceLimits?.MaxMemoryMb
            };
        }
        catch (ArgumentException)
        {
            // Process no longer exists
            return ServerResourceUsage.Empty(server.Id);
        }
    }
}
```

### 6. User & Permission System

```csharp
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    public ICollection<UserRole> Roles { get; set; }
    public ICollection<GameServer> OwnedServers { get; set; }
    public ICollection<ServerPermission> ServerPermissions { get; set; }
}

public class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; }              // "Admin", "Moderator", "User"
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }        // Cannot be deleted
    public int Priority { get; set; }             // Higher = more authority
    
    public ICollection<RolePermission> Permissions { get; set; }
}

public class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; }              // "servers.create", "servers.*.start"
    public string Name { get; set; }
    public string Category { get; set; }          // "Servers", "Users", "System"
    public string? Description { get; set; }
}

// Per-server permission override
public class ServerPermission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ServerId { get; set; }
    public string PermissionCode { get; set; }    // "start", "stop", "console", "files", "config"
    public bool IsGranted { get; set; }           // true = grant, false = deny
}
```

#### Permission Codes

```csharp
public static class Permissions
{
    public static class System
    {
        public const string ViewDashboard = "system.dashboard.view";
        public const string ViewHostMetrics = "system.metrics.view";
        public const string ManageSettings = "system.settings.manage";
        public const string ViewLogs = "system.logs.view";
    }
    
    public static class Users
    {
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Edit = "users.edit";
        public const string Delete = "users.delete";
        public const string ManageRoles = "users.roles.manage";
    }
    
    public static class Games
    {
        public const string View = "games.view";
        public const string Create = "games.create";
        public const string Edit = "games.edit";
        public const string Delete = "games.delete";
    }
    
    public static class Servers
    {
        public const string ViewOwn = "servers.own.view";
        public const string ViewAll = "servers.all.view";
        public const string Create = "servers.create";
        public const string Delete = "servers.delete";
        
        // Per-server actions (can be overridden per-server)
        public const string Start = "servers.{id}.start";
        public const string Stop = "servers.{id}.stop";
        public const string Restart = "servers.{id}.restart";
        public const string Console = "servers.{id}.console";
        public const string Files = "servers.{id}.files";
        public const string Config = "servers.{id}.config";
        public const string Mods = "servers.{id}.mods";
        public const string Backup = "servers.{id}.backup";
        public const string Schedule = "servers.{id}.schedule";
    }
}
```

### 7. Mod Management

Games can support multiple mod providers simultaneously (e.g., Minecraft with Modrinth + CurseForge). The mod system provides both individual provider access and a unified search interface.

#### Mod Provider Interface

```csharp
public interface IModProvider
{
    ModProviderType ProviderType { get; }
    string DisplayName { get; }
    string? IconUrl { get; }
    
    /// <summary>
    /// Check if this provider supports the given game.
    /// </summary>
    Task<bool> SupportsGameAsync(string gameSlug, CancellationToken ct = default);
    
    /// <summary>
    /// Search for mods on this provider.
    /// </summary>
    Task<ModSearchResult> SearchAsync(ModSearchQuery query, CancellationToken ct = default);
    
    /// <summary>
    /// Get detailed information about a specific mod.
    /// </summary>
    Task<ModDetails> GetDetailsAsync(string modId, CancellationToken ct = default);
    
    /// <summary>
    /// Get available versions for a mod, optionally filtered by game version.
    /// </summary>
    Task<IReadOnlyList<ModVersion>> GetVersionsAsync(string modId, string? gameVersion = null, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Download a mod version to the specified path.
    /// </summary>
    Task<ModDownloadResult> DownloadAsync(string modId, string versionId, string destinationPath,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);
    
    /// <summary>
    /// Check for updates for installed mods.
    /// </summary>
    Task<IReadOnlyList<ModUpdateInfo>> CheckUpdatesAsync(IEnumerable<InstalledModInfo> installedMods,
        string? gameVersion = null, CancellationToken ct = default);
}

public record ModSearchQuery
{
    public string Query { get; init; } = "";
    public string? GameSlug { get; init; }
    public string? GameVersion { get; init; }
    public string? ModLoader { get; init; }             // "fabric", "forge", "paper", etc.
    public ModSearchSort Sort { get; init; } = ModSearchSort.Relevance;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public List<string>? Categories { get; init; }
}

public enum ModSearchSort
{
    Relevance,
    Downloads,
    Updated,
    Created,
    Name
}

public record ModSearchResult
{
    public IReadOnlyList<ModSearchItem> Mods { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record ModSearchItem
{
    public string Id { get; init; }                     // Provider-specific ID
    public string Slug { get; init; }
    public string Name { get; init; }
    public string? Summary { get; init; }
    public string? IconUrl { get; init; }
    public string? Author { get; init; }
    public long Downloads { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public List<string> Categories { get; init; } = [];
    public List<string> GameVersions { get; init; } = [];
    public ModProviderType Provider { get; init; }
}

public record ModDetails
{
    public string Id { get; init; }
    public string Slug { get; init; }
    public string Name { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }           // Full description (markdown/html)
    public string? IconUrl { get; init; }
    public string? BannerUrl { get; init; }
    public string? SourceUrl { get; init; }             // GitHub, etc.
    public string? WikiUrl { get; init; }
    public string? DiscordUrl { get; init; }
    public List<ModAuthor> Authors { get; init; } = [];
    public long Downloads { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<string> Categories { get; init; } = [];
    public List<string> GameVersions { get; init; } = [];
    public List<string> Loaders { get; init; } = [];    // fabric, forge, paper, etc.
    public List<ModScreenshot> Screenshots { get; init; } = [];
    public ModProviderType Provider { get; init; }
}

public record ModVersion
{
    public string Id { get; init; }                     // Version ID
    public string Version { get; init; }                // Display version: "1.2.3"
    public string? Name { get; init; }                  // Version name/title
    public string? Changelog { get; init; }
    public List<string> GameVersions { get; init; } = [];
    public List<string> Loaders { get; init; } = [];
    public DateTime ReleasedAt { get; init; }
    public long Downloads { get; init; }
    public ModVersionType VersionType { get; init; }
    public List<ModDependency> Dependencies { get; init; } = [];
    public List<ModFile> Files { get; init; } = [];
}

public enum ModVersionType
{
    Release,
    Beta,
    Alpha
}

public record ModDependency
{
    public string ModId { get; init; }
    public string? ModName { get; init; }
    public string? VersionId { get; init; }
    public ModDependencyType Type { get; init; }
}

public enum ModDependencyType
{
    Required,
    Optional,
    Incompatible,
    Embedded
}

public record ModFile
{
    public string FileName { get; init; }
    public string Url { get; init; }
    public long SizeBytes { get; init; }
    public string? Sha512 { get; init; }
    public string? Sha1 { get; init; }
    public bool IsPrimary { get; init; }
}
```

#### Unified Mod Service

The unified mod service aggregates results from multiple providers:

```csharp
public interface IUnifiedModService
{
    /// <summary>
    /// Search across all enabled mod providers for a game server.
    /// Results are merged and deduplicated where possible.
    /// </summary>
    Task<UnifiedModSearchResult> SearchAsync(GameServer server, ModSearchQuery query,
        CancellationToken ct = default);
    
    /// <summary>
    /// Search a specific provider only.
    /// </summary>
    Task<ModSearchResult> SearchProviderAsync(GameServer server, ModProviderType provider,
        ModSearchQuery query, CancellationToken ct = default);
    
    /// <summary>
    /// Get mod details from a specific provider.
    /// </summary>
    Task<ModDetails> GetDetailsAsync(ModProviderType provider, string modId,
        CancellationToken ct = default);
    
    /// <summary>
    /// Install a mod to the server.
    /// </summary>
    Task<InstallResult> InstallAsync(GameServer server, ModProviderType provider, 
        string modId, string? versionId = null,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);
    
    /// <summary>
    /// Update a mod to the latest compatible version.
    /// </summary>
    Task<InstallResult> UpdateAsync(GameServer server, Guid installedModId,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);
    
    /// <summary>
    /// Uninstall a mod from the server.
    /// </summary>
    Task<bool> UninstallAsync(GameServer server, Guid installedModId, CancellationToken ct = default);
    
    /// <summary>
    /// Get all installed mods for a server.
    /// </summary>
    Task<IReadOnlyList<ServerMod>> GetInstalledAsync(GameServer server, CancellationToken ct = default);
    
    /// <summary>
    /// Check for available updates across all installed mods.
    /// </summary>
    Task<IReadOnlyList<ModUpdateInfo>> CheckAllUpdatesAsync(GameServer server,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get available mod providers for a game.
    /// </summary>
    IReadOnlyList<ModProviderInfo> GetProvidersForGame(Game game);
}

public record UnifiedModSearchResult
{
    public IReadOnlyList<ModSearchItem> Mods { get; init; } = [];
    public Dictionary<ModProviderType, int> ResultCountByProvider { get; init; } = new();
    public int TotalCount { get; init; }
    public bool HasMoreResults { get; init; }
}

public record ModProviderInfo
{
    public ModProviderType Type { get; init; }
    public string Name { get; init; }
    public string? IconUrl { get; init; }
    public bool Enabled { get; init; }
    public int Priority { get; init; }
}

public record ModUpdateInfo
{
    public Guid InstalledModId { get; init; }
    public string ModName { get; init; }
    public string CurrentVersion { get; init; }
    public string LatestVersion { get; init; }
    public string LatestVersionId { get; init; }
    public DateTime ReleasedAt { get; init; }
    public string? Changelog { get; init; }
    public ModProviderType Provider { get; init; }
}
```

#### Unified Mod Service Implementation

```csharp
public class UnifiedModService : IUnifiedModService
{
    private readonly IEnumerable<IModProvider> _providers;
    private readonly IServerModRepository _modRepository;
    private readonly ILogger<UnifiedModService> _logger;
    
    public async Task<UnifiedModSearchResult> SearchAsync(GameServer server, ModSearchQuery query,
        CancellationToken ct = default)
    {
        var game = server.Game;
        var enabledProviders = game.ModProviders
            .Where(p => p.Enabled)
            .OrderBy(p => p.Priority)
            .ToList();
        
        if (!enabledProviders.Any())
        {
            return new UnifiedModSearchResult();
        }
        
        // Search all providers in parallel
        var searchTasks = enabledProviders.Select(async config =>
        {
            var provider = _providers.FirstOrDefault(p => p.ProviderType == config.Provider);
            if (provider == null) return (config.Provider, Result: (ModSearchResult?)null);
            
            try
            {
                var providerQuery = query with
                {
                    GameSlug = config.GameSlug ?? query.GameSlug,
                    GameVersion = query.GameVersion ?? config.GameVersion
                };
                
                var result = await provider.SearchAsync(providerQuery, ct);
                return (config.Provider, Result: result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search provider {Provider}", config.Provider);
                return (config.Provider, Result: (ModSearchResult?)null);
            }
        });
        
        var results = await Task.WhenAll(searchTasks);
        
        // Merge and deduplicate results
        var allMods = new List<ModSearchItem>();
        var countByProvider = new Dictionary<ModProviderType, int>();
        var seenSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var (provider, result) in results.Where(r => r.Result != null))
        {
            countByProvider[provider] = result!.TotalCount;
            
            foreach (var mod in result.Mods)
            {
                // Basic deduplication by slug
                if (seenSlugs.Add(mod.Slug))
                {
                    allMods.Add(mod);
                }
            }
        }
        
        // Sort merged results
        var sortedMods = query.Sort switch
        {
            ModSearchSort.Downloads => allMods.OrderByDescending(m => m.Downloads),
            ModSearchSort.Updated => allMods.OrderByDescending(m => m.UpdatedAt),
            ModSearchSort.Name => allMods.OrderBy(m => m.Name),
            _ => allMods.AsEnumerable() // Keep provider priority order for relevance
        };
        
        return new UnifiedModSearchResult
        {
            Mods = sortedMods.ToList(),
            ResultCountByProvider = countByProvider,
            TotalCount = countByProvider.Values.Sum(),
            HasMoreResults = results.Any(r => r.Result?.TotalPages > 1)
        };
    }
    
    public async Task<InstallResult> InstallAsync(GameServer server, ModProviderType providerType,
        string modId, string? versionId = null,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderType == providerType)
            ?? throw new InvalidOperationException($"Provider {providerType} not available");
        
        var providerConfig = server.Game.ModProviders
            .FirstOrDefault(p => p.Provider == providerType)
            ?? throw new InvalidOperationException($"Provider {providerType} not configured for this game");
        
        // Get mod details
        var modDetails = await provider.GetDetailsAsync(modId, ct);
        
        // Get version to install
        ModVersion version;
        if (versionId != null)
        {
            var versions = await provider.GetVersionsAsync(modId, ct: ct);
            version = versions.FirstOrDefault(v => v.Id == versionId)
                ?? throw new InvalidOperationException($"Version {versionId} not found");
        }
        else
        {
            // Get latest compatible version
            var gameVersion = providerConfig.GameVersion;
            var versions = await provider.GetVersionsAsync(modId, gameVersion, ct);
            version = versions.FirstOrDefault()
                ?? throw new InvalidOperationException("No compatible version found");
        }
        
        // Calculate destination path
        var installPath = Path.Combine(server.InstallPath, providerConfig.ModInstallPath);
        Directory.CreateDirectory(installPath);
        
        // Download the mod
        var downloadResult = await provider.DownloadAsync(
            modId, version.Id, installPath, progress, ct);
        
        if (!downloadResult.Success)
        {
            return new InstallResult { Success = false, Error = downloadResult.Error };
        }
        
        // Record installation
        var serverMod = new ServerMod
        {
            Id = Guid.NewGuid(),
            ServerId = server.Id,
            ModId = modId,
            Name = modDetails.Name,
            Version = version.Version,
            VersionId = version.Id,
            Provider = providerType,
            InstalledAt = DateTime.UtcNow,
            IsEnabled = true,
            LocalPath = downloadResult.FilePath,
            IconUrl = modDetails.IconUrl
        };
        
        await _modRepository.AddAsync(serverMod, ct);
        
        // Handle dependencies
        var dependencyResults = new List<InstallResult>();
        foreach (var dep in version.Dependencies.Where(d => d.Type == ModDependencyType.Required))
        {
            var depResult = await InstallAsync(server, providerType, dep.ModId, dep.VersionId, 
                progress, ct);
            dependencyResults.Add(depResult);
        }
        
        return new InstallResult
        {
            Success = true,
            InstalledMod = serverMod,
            DependenciesInstalled = dependencyResults.Where(r => r.Success).Count()
        };
    }
    
    public IReadOnlyList<ModProviderInfo> GetProvidersForGame(Game game)
    {
        return game.ModProviders
            .OrderBy(p => p.Priority)
            .Select(p => new ModProviderInfo
            {
                Type = p.Provider,
                Name = GetProviderDisplayName(p.Provider),
                IconUrl = GetProviderIconUrl(p.Provider),
                Enabled = p.Enabled,
                Priority = p.Priority
            })
            .ToList();
    }
    
    private static string GetProviderDisplayName(ModProviderType type) => type switch
    {
        ModProviderType.Modrinth => "Modrinth",
        ModProviderType.CurseForge => "CurseForge",
        ModProviderType.Thunderstore => "Thunderstore",
        ModProviderType.SteamWorkshop => "Steam Workshop",
        ModProviderType.SpigotMC => "SpigotMC",
        ModProviderType.Hangar => "Hangar",
        ModProviderType.NexusMods => "Nexus Mods",
        ModProviderType.Local => "Local",
        _ => type.ToString()
    };
}

public record InstallResult
{
    public bool Success { get; init; }
    public ServerMod? InstalledMod { get; init; }
    public string? Error { get; init; }
    public int DependenciesInstalled { get; init; }
}
```

#### Server Mod Entity

```csharp
public class ServerMod
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public string ModId { get; set; }                   // Provider-specific ID
    public string Name { get; set; }
    public string? Version { get; set; }                // Display version
    public string? VersionId { get; set; }              // Provider-specific version ID
    public ModProviderType Provider { get; set; }
    public DateTime InstalledAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? LocalPath { get; set; }              // Path within server directory
    public string? IconUrl { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? FileHash { get; set; }               // For integrity verification
    
    public GameServer Server { get; set; }
}
```

#### Provider-Specific Implementations

##### Modrinth Provider

```csharp
public class ModrinthProvider : IModProvider
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://api.modrinth.com/v2";
    
    public ModProviderType ProviderType => ModProviderType.Modrinth;
    public string DisplayName => "Modrinth";
    public string IconUrl => "https://modrinth.com/favicon.ico";
    
    public async Task<ModSearchResult> SearchAsync(ModSearchQuery query, CancellationToken ct = default)
    {
        var facets = new List<string>();
        
        if (!string.IsNullOrEmpty(query.GameSlug))
            facets.Add($"[\"project_type:{query.GameSlug}\"]");
        
        if (!string.IsNullOrEmpty(query.GameVersion))
            facets.Add($"[\"versions:{query.GameVersion}\"]");
        
        if (!string.IsNullOrEmpty(query.ModLoader))
            facets.Add($"[\"categories:{query.ModLoader}\"]");
        
        var queryParams = new Dictionary<string, string>
        {
            ["query"] = query.Query,
            ["offset"] = ((query.Page - 1) * query.PageSize).ToString(),
            ["limit"] = query.PageSize.ToString(),
            ["index"] = MapSortOrder(query.Sort)
        };
        
        if (facets.Any())
            queryParams["facets"] = $"[{string.Join(",", facets)}]";
        
        var url = $"{BaseUrl}/search?{BuildQueryString(queryParams)}";
        var response = await _http.GetFromJsonAsync<ModrinthSearchResponse>(url, ct);
        
        return new ModSearchResult
        {
            Mods = response!.Hits.Select(MapToSearchItem).ToList(),
            TotalCount = response.TotalHits,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
    
    public async Task<IReadOnlyList<ModVersion>> GetVersionsAsync(string modId, 
        string? gameVersion = null, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/project/{modId}/version";
        
        if (!string.IsNullOrEmpty(gameVersion))
            url += $"?game_versions=[\"{gameVersion}\"]";
        
        var versions = await _http.GetFromJsonAsync<List<ModrinthVersion>>(url, ct);
        return versions!.Select(MapToModVersion).ToList();
    }
    
    public async Task<ModDownloadResult> DownloadAsync(string modId, string versionId, 
        string destinationPath, IProgress<DownloadProgress>? progress = null, 
        CancellationToken ct = default)
    {
        var versions = await GetVersionsAsync(modId, ct: ct);
        var version = versions.FirstOrDefault(v => v.Id == versionId);
        
        if (version == null)
            return new ModDownloadResult { Success = false, Error = "Version not found" };
        
        var file = version.Files.FirstOrDefault(f => f.IsPrimary) ?? version.Files.First();
        var filePath = Path.Combine(destinationPath, file.FileName);
        
        using var response = await _http.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        
        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = File.Create(filePath);
        
        var buffer = new byte[81920];
        long bytesRead = 0;
        int read;
        
        while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesRead += read;
            progress?.Report(new DownloadProgress(bytesRead, totalBytes, file.FileName));
        }
        
        return new ModDownloadResult { Success = true, FilePath = filePath };
    }
    
    private static string MapSortOrder(ModSearchSort sort) => sort switch
    {
        ModSearchSort.Downloads => "downloads",
        ModSearchSort.Updated => "updated",
        ModSearchSort.Created => "newest",
        ModSearchSort.Name => "relevance",
        _ => "relevance"
    };
}
```

##### CurseForge Provider

```csharp
public class CurseForgeProvider : IModProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.curseforge.com/v1";
    
    public ModProviderType ProviderType => ModProviderType.CurseForge;
    public string DisplayName => "CurseForge";
    public string IconUrl => "https://www.curseforge.com/favicon.ico";
    
    // CurseForge game IDs
    private static readonly Dictionary<string, int> GameIds = new()
    {
        ["minecraft"] = 432,
        ["wow"] = 1,
        ["sims4"] = 78062
    };
    
    public async Task<ModSearchResult> SearchAsync(ModSearchQuery query, CancellationToken ct = default)
    {
        if (!GameIds.TryGetValue(query.GameSlug ?? "", out var gameId))
            return new ModSearchResult();
        
        var request = new HttpRequestMessage(HttpMethod.Get, 
            $"{BaseUrl}/mods/search?gameId={gameId}&searchFilter={query.Query}" +
            $"&pageSize={query.PageSize}&index={(query.Page - 1) * query.PageSize}" +
            $"&sortField={MapSortOrder(query.Sort)}");
        
        request.Headers.Add("x-api-key", _apiKey);
        
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<CurseForgeSearchResponse>(ct);
        
        return new ModSearchResult
        {
            Mods = result!.Data.Select(MapToSearchItem).ToList(),
            TotalCount = result.Pagination.TotalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
    
    // Additional implementation methods...
}
```
```

### 8. File Manager

Web-based file manager for each server:

```csharp
public interface IServerFileManager
{
    Task<FileSystemEntry> GetDirectoryContentsAsync(GameServer server, string relativePath, 
        CancellationToken ct = default);
    Task<string> ReadFileAsync(GameServer server, string relativePath, CancellationToken ct = default);
    Task WriteFileAsync(GameServer server, string relativePath, string content, 
        CancellationToken ct = default);
    Task<Stream> DownloadFileAsync(GameServer server, string relativePath, CancellationToken ct = default);
    Task UploadFileAsync(GameServer server, string relativePath, Stream content, 
        CancellationToken ct = default);
    Task DeleteAsync(GameServer server, string relativePath, CancellationToken ct = default);
    Task RenameAsync(GameServer server, string relativePath, string newName, CancellationToken ct = default);
    Task CreateDirectoryAsync(GameServer server, string relativePath, CancellationToken ct = default);
    Task<byte[]> CreateArchiveAsync(GameServer server, string relativePath, CancellationToken ct = default);
    Task ExtractArchiveAsync(GameServer server, string relativePath, Stream archive, 
        CancellationToken ct = default);
}

public record FileSystemEntry
{
    public string Name { get; init; }
    public string Path { get; init; }
    public FileEntryType Type { get; init; }
    public long? Size { get; init; }
    public DateTime ModifiedAt { get; init; }
    public string? MimeType { get; init; }
    public bool IsEditable { get; init; }
    public IReadOnlyList<FileSystemEntry>? Children { get; init; }
}
```

---

## SignalR Hubs

### Console Hub

```csharp
public interface IConsoleHubClient
{
    Task ReceiveOutput(Guid serverId, string line, ConsoleOutputType type);
    Task ServerStatusChanged(Guid serverId, ServerStatus status);
}

public class ConsoleHub : Hub<IConsoleHubClient>
{
    private readonly IServerExecutorFactory _executorFactory;
    private readonly IAuthorizationService _auth;
    
    public async Task JoinServerConsole(Guid serverId)
    {
        // Verify permission
        var server = await GetServerAsync(serverId);
        if (!await _auth.AuthorizeAsync(Context.User, server, Permissions.Servers.Console))
        {
            throw new HubException("Unauthorized");
        }
        
        await Groups.AddToGroupAsync(Context.ConnectionId, $"console:{serverId}");
        
        // Start streaming if not already
        await StartConsoleStreamingAsync(server);
    }
    
    public async Task LeaveServerConsole(Guid serverId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"console:{serverId}");
    }
    
    public async Task SendCommand(Guid serverId, string command)
    {
        var server = await GetServerAsync(serverId);
        if (!await _auth.AuthorizeAsync(Context.User, server, Permissions.Servers.Console))
        {
            throw new HubException("Unauthorized");
        }
        
        var executor = _executorFactory.GetExecutor(server.DeploymentType);
        await executor.SendCommandAsync(server, command);
    }
}
```

### Metrics Hub

```csharp
public interface IMetricsHubClient
{
    Task ReceiveHostMetrics(HostMetrics metrics);
    Task ReceiveServerMetrics(Guid serverId, ServerResourceUsage usage);
}

public class MetricsHub : Hub<IMetricsHubClient>
{
    public async Task SubscribeToHostMetrics()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "host-metrics");
    }
    
    public async Task SubscribeToServerMetrics(Guid serverId)
    {
        // Verify permission
        await Groups.AddToGroupAsync(Context.ConnectionId, $"server-metrics:{serverId}");
    }
    
    public async Task UnsubscribeFromServerMetrics(Guid serverId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"server-metrics:{serverId}");
    }
}
```

---

## UI/UX Specification

### Theming System

#### Tailwind Configuration

```javascript
// tailwind.config.js
module.exports = {
  darkMode: 'class',
  content: ['./**/*.{razor,html,cshtml}'],
  theme: {
    extend: {
      colors: {
        // Semantic color tokens - reference CSS variables
        'nebula': {
          'bg-primary': 'var(--nebula-bg-primary)',
          'bg-secondary': 'var(--nebula-bg-secondary)',
          'bg-tertiary': 'var(--nebula-bg-tertiary)',
          'bg-elevated': 'var(--nebula-bg-elevated)',
          'text-primary': 'var(--nebula-text-primary)',
          'text-secondary': 'var(--nebula-text-secondary)',
          'text-muted': 'var(--nebula-text-muted)',
          'border': 'var(--nebula-border)',
          'border-subtle': 'var(--nebula-border-subtle)',
          'accent': 'var(--nebula-accent)',
          'accent-hover': 'var(--nebula-accent-hover)',
          'accent-subtle': 'var(--nebula-accent-subtle)',
          'success': 'var(--nebula-success)',
          'warning': 'var(--nebula-warning)',
          'error': 'var(--nebula-error)',
          'info': 'var(--nebula-info)',
        }
      },
      fontFamily: {
        'sans': ['var(--nebula-font-sans)', 'system-ui', 'sans-serif'],
        'mono': ['var(--nebula-font-mono)', 'monospace'],
      },
      boxShadow: {
        'nebula': 'var(--nebula-shadow)',
        'nebula-lg': 'var(--nebula-shadow-lg)',
        'nebula-glow': 'var(--nebula-glow)',
      },
      borderRadius: {
        'nebula': 'var(--nebula-radius)',
        'nebula-lg': 'var(--nebula-radius-lg)',
      },
      backgroundImage: {
        'nebula-gradient': 'var(--nebula-gradient)',
        'nebula-gradient-subtle': 'var(--nebula-gradient-subtle)',
      }
    }
  },
  plugins: [
    require('@tailwindcss/forms'),
    require('@tailwindcss/typography'),
  ]
}
```

#### CSS Variables - Dark Theme

```css
/* themes/nebula-dark.css */
:root[data-theme="dark"], .dark {
  /* Background Colors */
  --nebula-bg-primary: #0a0a0f;
  --nebula-bg-secondary: #12121a;
  --nebula-bg-tertiary: #1a1a25;
  --nebula-bg-elevated: #222230;
  
  /* Text Colors */
  --nebula-text-primary: #f0f0f5;
  --nebula-text-secondary: #a0a0b0;
  --nebula-text-muted: #606070;
  
  /* Border Colors */
  --nebula-border: #2a2a3a;
  --nebula-border-subtle: #1f1f2a;
  
  /* Accent Colors - Purple/Blue Nebula Theme */
  --nebula-accent: #8b5cf6;
  --nebula-accent-hover: #a78bfa;
  --nebula-accent-subtle: rgba(139, 92, 246, 0.15);
  
  /* Status Colors */
  --nebula-success: #22c55e;
  --nebula-warning: #f59e0b;
  --nebula-error: #ef4444;
  --nebula-info: #3b82f6;
  
  /* Fonts */
  --nebula-font-sans: 'Inter', 'Segoe UI';
  --nebula-font-mono: 'JetBrains Mono', 'Fira Code', 'Consolas';
  
  /* Shadows & Effects */
  --nebula-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.5), 0 2px 4px -1px rgba(0, 0, 0, 0.3);
  --nebula-shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.5), 0 4px 6px -2px rgba(0, 0, 0, 0.3);
  --nebula-glow: 0 0 20px rgba(139, 92, 246, 0.3);
  
  /* Border Radius */
  --nebula-radius: 0.5rem;
  --nebula-radius-lg: 0.75rem;
  
  /* Gradients */
  --nebula-gradient: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f0f1a 100%);
  --nebula-gradient-subtle: linear-gradient(180deg, rgba(139, 92, 246, 0.05) 0%, transparent 100%);
  
  /* Specific Component Colors */
  --nebula-sidebar-bg: #0d0d12;
  --nebula-card-bg: #15151f;
  --nebula-input-bg: #1a1a25;
  --nebula-console-bg: #050508;
}
```

#### CSS Variables - Light Theme

```css
/* themes/nebula-light.css */
:root[data-theme="light"], .light {
  /* Background Colors */
  --nebula-bg-primary: #fafafc;
  --nebula-bg-secondary: #f0f0f5;
  --nebula-bg-tertiary: #e5e5ef;
  --nebula-bg-elevated: #ffffff;
  
  /* Text Colors */
  --nebula-text-primary: #1a1a2e;
  --nebula-text-secondary: #4a4a5a;
  --nebula-text-muted: #8a8a9a;
  
  /* Border Colors */
  --nebula-border: #d0d0e0;
  --nebula-border-subtle: #e0e0ea;
  
  /* Accent Colors - Deeper purple for contrast */
  --nebula-accent: #7c3aed;
  --nebula-accent-hover: #6d28d9;
  --nebula-accent-subtle: rgba(124, 58, 237, 0.1);
  
  /* Status Colors */
  --nebula-success: #16a34a;
  --nebula-warning: #d97706;
  --nebula-error: #dc2626;
  --nebula-info: #2563eb;
  
  /* Shadows & Effects */
  --nebula-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
  --nebula-shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05);
  --nebula-glow: 0 0 20px rgba(124, 58, 237, 0.2);
  
  /* Gradients */
  --nebula-gradient: linear-gradient(135deg, #f5f5ff 0%, #e8e8f5 50%, #fafaff 100%);
  --nebula-gradient-subtle: linear-gradient(180deg, rgba(124, 58, 237, 0.03) 0%, transparent 100%);
  
  /* Specific Component Colors */
  --nebula-sidebar-bg: #f5f5fa;
  --nebula-card-bg: #ffffff;
  --nebula-input-bg: #f5f5fa;
  --nebula-console-bg: #1a1a2e;
}
```

#### Theme Provider Component

```razor
<!-- ThemeProvider.razor -->
@inject IJSRuntime JS
@inject ILocalStorageService LocalStorage

<CascadingValue Value="this">
    <div class="@ThemeClass min-h-screen bg-nebula-bg-primary text-nebula-text-primary">
        @ChildContent
    </div>
</CascadingValue>

@code {
    [Parameter] public RenderFragment ChildContent { get; set; }
    
    public string CurrentTheme { get; private set; } = "dark";
    public string ThemeClass => CurrentTheme == "dark" ? "dark" : "light";
    
    protected override async Task OnInitializedAsync()
    {
        var savedTheme = await LocalStorage.GetItemAsync<string>("nebula-theme");
        if (!string.IsNullOrEmpty(savedTheme))
        {
            CurrentTheme = savedTheme;
        }
        else
        {
            // Check system preference
            var prefersDark = await JS.InvokeAsync<bool>("matchMedia", "(prefers-color-scheme: dark)");
            CurrentTheme = prefersDark ? "dark" : "light";
        }
        
        await ApplyTheme();
    }
    
    public async Task SetTheme(string theme)
    {
        CurrentTheme = theme;
        await LocalStorage.SetItemAsync("nebula-theme", theme);
        await ApplyTheme();
        StateHasChanged();
    }
    
    public async Task ToggleTheme()
    {
        await SetTheme(CurrentTheme == "dark" ? "light" : "dark");
    }
    
    private async Task ApplyTheme()
    {
        await JS.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", CurrentTheme);
    }
}
```

### Component Examples

#### Server Card Component

```razor
<!-- ServerCard.razor -->
@inject NavigationManager Nav

<div class="bg-nebula-card-bg rounded-nebula-lg border border-nebula-border 
            hover:border-nebula-accent/50 transition-all duration-200
            shadow-nebula hover:shadow-nebula-glow cursor-pointer group"
     @onclick="NavigateToServer">
    
    <!-- Header -->
    <div class="p-4 border-b border-nebula-border-subtle flex items-center gap-3">
        <div class="relative">
            <img src="@Server.Game.IconPath" alt="@Server.Game.Name" 
                 class="w-10 h-10 rounded-nebula object-cover" />
            <div class="absolute -bottom-1 -right-1 w-3 h-3 rounded-full 
                        @StatusColorClass border-2 border-nebula-card-bg"></div>
        </div>
        <div class="flex-1 min-w-0">
            <h3 class="font-semibold text-nebula-text-primary truncate 
                       group-hover:text-nebula-accent transition-colors">
                @Server.Name
            </h3>
            <p class="text-sm text-nebula-text-muted truncate">@Server.Game.Name</p>
        </div>
        <StatusBadge Status="@Server.Status" />
    </div>
    
    <!-- Metrics -->
    <div class="p-4 grid grid-cols-3 gap-4">
        <MetricDisplay Label="CPU" Value="@CpuUsage" Unit="%" Icon="cpu" />
        <MetricDisplay Label="RAM" Value="@MemoryUsage" Unit="MB" Icon="memory" />
        <MetricDisplay Label="Players" Value="@PlayerCount" Unit="" Icon="users" />
    </div>
    
    <!-- Actions -->
    <div class="px-4 pb-4 flex gap-2">
        @if (Server.Status == ServerStatus.Running)
        {
            <button @onclick="Stop" @onclick:stopPropagation="true"
                    class="flex-1 btn-secondary text-sm">
                <Icon Name="square" class="w-4 h-4 mr-1" /> Stop
            </button>
            <button @onclick="Restart" @onclick:stopPropagation="true"
                    class="flex-1 btn-secondary text-sm">
                <Icon Name="refresh-cw" class="w-4 h-4 mr-1" /> Restart
            </button>
        }
        else if (Server.Status == ServerStatus.Stopped)
        {
            <button @onclick="Start" @onclick:stopPropagation="true"
                    class="flex-1 btn-primary text-sm">
                <Icon Name="play" class="w-4 h-4 mr-1" /> Start
            </button>
        }
        else
        {
            <button disabled class="flex-1 btn-secondary text-sm opacity-50">
                <Icon Name="loader" class="w-4 h-4 mr-1 animate-spin" /> 
                @Server.Status.ToString()
            </button>
        }
    </div>
</div>

@code {
    [Parameter, EditorRequired] public GameServer Server { get; set; } = null!;
    [Parameter] public ServerResourceUsage? Metrics { get; set; }
    
    private string CpuUsage => Metrics?.CpuPercent.ToString("F1") ?? "—";
    private string MemoryUsage => Metrics?.MemoryUsedMb.ToString("F0") ?? "—";
    private string PlayerCount => "—"; // Implement player count query
    
    private string StatusColorClass => Server.Status switch
    {
        ServerStatus.Running => "bg-nebula-success",
        ServerStatus.Stopped => "bg-nebula-text-muted",
        ServerStatus.Crashed => "bg-nebula-error",
        ServerStatus.Starting or ServerStatus.Stopping => "bg-nebula-warning animate-pulse",
        _ => "bg-nebula-text-muted"
    };
    
    private void NavigateToServer() => Nav.NavigateTo($"/servers/{Server.Id}");
    
    // Action handlers...
}
```

#### Console Component

```razor
<!-- ConsoleViewer.razor -->
@implements IAsyncDisposable
@inject IConsoleHubService ConsoleHub

<div class="flex flex-col h-full bg-nebula-console-bg rounded-nebula-lg overflow-hidden 
            border border-nebula-border font-mono">
    
    <!-- Console Header -->
    <div class="flex items-center justify-between px-4 py-2 
                bg-nebula-bg-secondary border-b border-nebula-border">
        <div class="flex items-center gap-2">
            <div class="flex gap-1.5">
                <span class="w-3 h-3 rounded-full bg-nebula-error"></span>
                <span class="w-3 h-3 rounded-full bg-nebula-warning"></span>
                <span class="w-3 h-3 rounded-full bg-nebula-success"></span>
            </div>
            <span class="text-sm text-nebula-text-secondary ml-2">Console — @ServerName</span>
        </div>
        <div class="flex items-center gap-2">
            <button @onclick="ClearConsole" class="p-1.5 rounded hover:bg-nebula-bg-tertiary 
                    text-nebula-text-muted hover:text-nebula-text-primary transition-colors"
                    title="Clear">
                <Icon Name="trash-2" class="w-4 h-4" />
            </button>
            <button @onclick="ToggleAutoScroll" class="p-1.5 rounded hover:bg-nebula-bg-tertiary 
                    @(AutoScroll ? "text-nebula-accent" : "text-nebula-text-muted") 
                    hover:text-nebula-text-primary transition-colors"
                    title="Auto-scroll">
                <Icon Name="arrow-down-to-line" class="w-4 h-4" />
            </button>
        </div>
    </div>
    
    <!-- Console Output -->
    <div @ref="_consoleOutput" 
         class="flex-1 overflow-y-auto p-4 text-sm leading-relaxed scroll-smooth">
        @foreach (var line in _lines)
        {
            <div class="@GetLineClass(line.Type) whitespace-pre-wrap break-all">
                <span class="text-nebula-text-muted select-none mr-2">
                    [@line.Timestamp.ToString("HH:mm:ss")]
                </span>
                @line.Content
            </div>
        }
    </div>
    
    <!-- Command Input -->
    <div class="border-t border-nebula-border p-2">
        <div class="flex items-center gap-2 bg-nebula-input-bg rounded-nebula px-3 py-2">
            <span class="text-nebula-accent select-none">❯</span>
            <input @bind="_command" @bind:event="oninput"
                   @onkeydown="HandleKeyDown"
                   type="text"
                   placeholder="Enter command..."
                   class="flex-1 bg-transparent border-none outline-none 
                          text-nebula-text-primary placeholder:text-nebula-text-muted" />
            <button @onclick="SendCommand" 
                    class="p-1 rounded hover:bg-nebula-accent/20 text-nebula-accent 
                           transition-colors disabled:opacity-50"
                    disabled="@string.IsNullOrWhiteSpace(_command)">
                <Icon Name="send" class="w-4 h-4" />
            </button>
        </div>
        @if (_commandHistory.Any())
        {
            <div class="text-xs text-nebula-text-muted mt-1 px-3">
                ↑↓ to navigate history • Enter to send
            </div>
        }
    </div>
</div>

@code {
    [Parameter, EditorRequired] public Guid ServerId { get; set; }
    [Parameter] public string ServerName { get; set; } = "Server";
    [Parameter] public int MaxLines { get; set; } = 1000;
    
    private ElementReference _consoleOutput;
    private readonly List<ConsoleLine> _lines = new();
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;
    private string _command = "";
    private bool AutoScroll { get; set; } = true;
    
    protected override async Task OnInitializedAsync()
    {
        await ConsoleHub.JoinServerConsoleAsync(ServerId);
        ConsoleHub.OnOutput += HandleOutput;
    }
    
    private void HandleOutput(Guid serverId, string content, ConsoleOutputType type)
    {
        if (serverId != ServerId) return;
        
        InvokeAsync(() =>
        {
            _lines.Add(new ConsoleLine(DateTime.Now, content, type));
            
            // Trim old lines
            while (_lines.Count > MaxLines)
            {
                _lines.RemoveAt(0);
            }
            
            StateHasChanged();
            
            if (AutoScroll)
            {
                _ = ScrollToBottom();
            }
        });
    }
    
    private string GetLineClass(ConsoleOutputType type) => type switch
    {
        ConsoleOutputType.Error => "text-nebula-error",
        ConsoleOutputType.Warning => "text-nebula-warning",
        ConsoleOutputType.Info => "text-nebula-info",
        ConsoleOutputType.Success => "text-nebula-success",
        _ => "text-nebula-text-primary"
    };
    
    // Additional methods...
    
    public async ValueTask DisposeAsync()
    {
        ConsoleHub.OnOutput -= HandleOutput;
        await ConsoleHub.LeaveServerConsoleAsync(ServerId);
    }
    
    private record ConsoleLine(DateTime Timestamp, string Content, ConsoleOutputType Type);
}
```

#### DataGrid Component Usage

```razor
<!-- ServerList.razor -->
@page "/servers"
@inject IServerService ServerService

<PageHeader Title="Game Servers" Subtitle="Manage your game server instances">
    <Actions>
        <button @onclick="OpenCreateModal" class="btn-primary">
            <Icon Name="plus" class="w-4 h-4 mr-2" /> New Server
        </button>
    </Actions>
</PageHeader>

<div class="bg-nebula-card-bg rounded-nebula-lg border border-nebula-border overflow-hidden">
    <DataGrid TItem="GameServerDto"
              Items="_servers"
              Loading="_loading"
              Searchable="true"
              SearchPlaceholder="Search servers..."
              EmptyMessage="No servers found. Create your first server to get started."
              OnRowClick="NavigateToServer"
              Class="nebula-datagrid">
        
        <Columns>
            <DataGridColumn TItem="GameServerDto" 
                           Field="@(s => s.Name)" 
                           Title="Server"
                           Sortable="true"
                           Filterable="true">
                <Template Context="server">
                    <div class="flex items-center gap-3">
                        <img src="@server.GameIcon" alt="" class="w-8 h-8 rounded" />
                        <div>
                            <div class="font-medium text-nebula-text-primary">@server.Name</div>
                            <div class="text-sm text-nebula-text-muted">@server.GameName</div>
                        </div>
                    </div>
                </Template>
            </DataGridColumn>
            
            <DataGridColumn TItem="GameServerDto" 
                           Field="@(s => s.Status)" 
                           Title="Status"
                           Sortable="true"
                           Width="120px">
                <Template Context="server">
                    <StatusBadge Status="@server.Status" />
                </Template>
            </DataGridColumn>
            
            <DataGridColumn TItem="GameServerDto" 
                           Title="Resources"
                           Width="200px">
                <Template Context="server">
                    <div class="flex items-center gap-4 text-sm">
                        <ResourceMini Label="CPU" Value="@server.CpuPercent" Max="100" Unit="%" />
                        <ResourceMini Label="RAM" Value="@server.MemoryMb" 
                                     Max="@server.MemoryLimitMb" Unit="MB" />
                    </div>
                </Template>
            </DataGridColumn>
            
            <DataGridColumn TItem="GameServerDto" 
                           Field="@(s => s.Address)" 
                           Title="Address"
                           Sortable="true">
                <Template Context="server">
                    <code class="text-sm bg-nebula-bg-tertiary px-2 py-0.5 rounded">
                        @server.Address:@server.Port
                    </code>
                </Template>
            </DataGridColumn>
            
            <DataGridColumn TItem="GameServerDto" 
                           Field="@(s => s.PlayerCount)" 
                           Title="Players"
                           Sortable="true"
                           Width="100px">
                <Template Context="server">
                    @server.PlayerCount / @server.MaxPlayers
                </Template>
            </DataGridColumn>
            
            <DataGridColumn TItem="GameServerDto" 
                           Title=""
                           Width="150px">
                <Template Context="server">
                    <div class="flex items-center gap-1 justify-end">
                        <ServerQuickActions Server="@server" 
                                           OnStart="@(() => StartServer(server.Id))"
                                           OnStop="@(() => StopServer(server.Id))"
                                           OnRestart="@(() => RestartServer(server.Id))" />
                        <DropdownMenu>
                            <MenuTrigger>
                                <button class="p-1.5 rounded hover:bg-nebula-bg-tertiary">
                                    <Icon Name="more-vertical" class="w-4 h-4" />
                                </button>
                            </MenuTrigger>
                            <MenuItems>
                                <MenuItem Icon="terminal" OnClick="@(() => OpenConsole(server.Id))">
                                    Console
                                </MenuItem>
                                <MenuItem Icon="folder" OnClick="@(() => OpenFiles(server.Id))">
                                    Files
                                </MenuItem>
                                <MenuItem Icon="settings" OnClick="@(() => OpenSettings(server.Id))">
                                    Settings
                                </MenuItem>
                                <MenuDivider />
                                <MenuItem Icon="trash-2" Destructive="true" 
                                         OnClick="@(() => DeleteServer(server.Id))">
                                    Delete
                                </MenuItem>
                            </MenuItems>
                        </DropdownMenu>
                    </div>
                </Template>
            </DataGridColumn>
        </Columns>
        
    </DataGrid>
</div>
```

### Creating New Themes

To create a new theme, add a new CSS file following this template:

```css
/* themes/nebula-cyberpunk.css */
:root[data-theme="cyberpunk"] {
  /* Override the CSS variables with your color scheme */
  --nebula-bg-primary: #0d0d0d;
  --nebula-bg-secondary: #1a1a1a;
  --nebula-accent: #00ff9f;
  --nebula-accent-hover: #00cc7f;
  /* ... rest of variables */
}
```

Then register the theme in the theme provider:

```csharp
public static class NebulaThemes
{
    public static readonly IReadOnlyDictionary<string, ThemeInfo> Available = new Dictionary<string, ThemeInfo>
    {
        ["dark"] = new("Dark", "Default dark theme with purple accents", "nebula-dark"),
        ["light"] = new("Light", "Clean light theme", "nebula-light"),
        ["cyberpunk"] = new("Cyberpunk", "Neon green cyberpunk aesthetic", "nebula-cyberpunk"),
        // Add more themes here
    };
}
```

---

## Database Schema

The schema below uses standard SQL that works across SQLite, PostgreSQL, and MySQL. EF Core handles provider-specific translations automatically.

> **Note**: SQLite doesn't support `gen_random_uuid()` — GUIDs are generated in application code. The `TIMESTAMPTZ` type maps to `TEXT` in SQLite (ISO8601 strings), and `JSONB` maps to `TEXT` with JSON validation in EF Core.

### Entity Relationship Diagram

```
┌──────────────┐     ┌──────────────────┐     ┌──────────────────┐
│    Users     │────<│    UserRoles     │>────│      Roles       │
└──────────────┘     └──────────────────┘     └──────────────────┘
       │                                              │
       │                                              │
       │                                      ┌──────────────────┐
       │                                      │ RolePermissions  │
       │                                      └──────────────────┘
       │                                              │
       │                                      ┌──────────────────┐
       │                                      │   Permissions    │
       │                                      └──────────────────┘
       │
       │owns
       ▼
┌──────────────┐     ┌──────────────────┐     ┌──────────────────┐
│    Games     │────<│   GameServers    │>────│   ServerMods     │
└──────────────┘     └──────────────────┘     └──────────────────┘
                            │
                            │
              ┌─────────────┼─────────────┐
              │             │             │
              ▼             ▼             ▼
       ┌──────────┐  ┌──────────┐  ┌──────────────┐
       │ Backups  │  │ Tasks    │  │ServerPerms   │
       └──────────┘  └──────────┘  └──────────────┘
```

### Key Tables

```sql
-- Games table (SQLite-compatible syntax)
CREATE TABLE games (
    id TEXT PRIMARY KEY,                            -- GUID as text in SQLite
    name TEXT NOT NULL,
    slug TEXT NOT NULL UNIQUE,
    steam_app_id TEXT,
    executable_type INTEGER NOT NULL,
    default_executable_path TEXT NOT NULL,
    default_start_command TEXT NOT NULL,
    default_stop_command TEXT,
    supports_docker INTEGER DEFAULT 0,              -- Boolean as integer in SQLite
    default_docker_image TEXT,
    supports_mods INTEGER DEFAULT 0,
    mod_providers TEXT,                             -- JSON array
    icon_path TEXT,
    rcon_defaults TEXT,                             -- JSON object
    configuration_schemas TEXT,                     -- JSON object
    created_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT DEFAULT (datetime('now'))
);

-- Game Servers table
CREATE TABLE game_servers (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    game_id TEXT NOT NULL REFERENCES games(id),
    deployment_type INTEGER NOT NULL,
    install_path TEXT NOT NULL,
    docker_container_id TEXT,
    docker_config TEXT,                             -- JSON object
    native_config TEXT,                             -- JSON object
    rcon_config TEXT,                               -- JSON object
    primary_port INTEGER NOT NULL,
    additional_ports TEXT DEFAULT '{}',             -- JSON object
    bind_address TEXT DEFAULT '0.0.0.0',
    resource_limits TEXT,                           -- JSON object
    owner_id TEXT NOT NULL REFERENCES users(id),
    created_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT DEFAULT (datetime('now')),
    
    UNIQUE (owner_id, name)
);

CREATE INDEX idx_game_servers_game ON game_servers(game_id);
CREATE INDEX idx_game_servers_owner ON game_servers(owner_id);
CREATE INDEX idx_game_servers_deployment ON game_servers(deployment_type);

-- Server Mods table
CREATE TABLE server_mods (
    id TEXT PRIMARY KEY,
    server_id TEXT NOT NULL REFERENCES game_servers(id) ON DELETE CASCADE,
    mod_id TEXT NOT NULL,
    name TEXT NOT NULL,
    version TEXT,
    version_id TEXT,
    provider INTEGER NOT NULL,
    installed_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT,
    is_enabled INTEGER DEFAULT 1,
    local_path TEXT,
    icon_url TEXT,
    file_size_bytes INTEGER,
    file_hash TEXT,
    
    UNIQUE (server_id, provider, mod_id)
);

CREATE INDEX idx_server_mods_server ON server_mods(server_id);
```

#### PostgreSQL-Specific Optimizations (Optional)

If using PostgreSQL, you can leverage native features:

```sql
-- PostgreSQL version with native types
CREATE TABLE games (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(100) NOT NULL UNIQUE,
    -- ... other fields ...
    mod_providers JSONB,                            -- Native JSONB with indexing
    configuration_schemas JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- GIN index for fast JSON queries
CREATE INDEX idx_games_mod_providers ON games USING GIN (mod_providers);
```
```

---

## API Endpoints

### REST API Structure

```
/api/v1
├── /auth
│   ├── POST   /login              # Authenticate user
│   ├── POST   /logout             # Invalidate session
│   ├── POST   /refresh            # Refresh JWT token
│   └── GET    /me                 # Get current user
│
├── /games
│   ├── GET    /                   # List all games
│   ├── GET    /{id}               # Get game details
│   ├── POST   /                   # Create game (admin)
│   ├── PUT    /{id}               # Update game (admin)
│   ├── DELETE /{id}               # Delete game (admin)
│   ├── GET    /{id}/providers     # List mod providers for game
│   ├── POST   /{id}/providers     # Add mod provider to game
│   ├── PUT    /{id}/providers/{provider} # Update provider config
│   └── DELETE /{id}/providers/{provider} # Remove provider from game
│
├── /servers
│   ├── GET    /                   # List user's servers
│   ├── GET    /{id}               # Get server details
│   ├── POST   /                   # Create server
│   ├── PUT    /{id}               # Update server config
│   ├── DELETE /{id}               # Delete server
│   │
│   ├── POST   /{id}/start         # Start server
│   ├── POST   /{id}/stop          # Stop server
│   ├── POST   /{id}/restart       # Restart server
│   ├── POST   /{id}/kill          # Force kill server
│   │
│   ├── GET    /{id}/status        # Get server status
│   ├── GET    /{id}/metrics       # Get current metrics
│   ├── GET    /{id}/metrics/history # Get metrics history
│   │
│   ├── POST   /{id}/command       # Send console command
│   │
│   ├── /files
│   │   ├── GET    /{id}/files         # List directory
│   │   ├── GET    /{id}/files/content # Read file
│   │   ├── PUT    /{id}/files/content # Write file
│   │   ├── POST   /{id}/files/upload  # Upload file
│   │   ├── GET    /{id}/files/download # Download file
│   │   ├── DELETE /{id}/files         # Delete file/folder
│   │   └── POST   /{id}/files/rename  # Rename file/folder
│   │
│   ├── /mods
│   │   ├── GET    /{id}/mods          # List installed mods
│   │   ├── GET    /{id}/mods/providers # List available providers for this server
│   │   ├── POST   /{id}/mods/search   # Unified search across all providers
│   │   ├── POST   /{id}/mods/search/{provider} # Search specific provider
│   │   ├── GET    /{id}/mods/details/{provider}/{modId} # Get mod details
│   │   ├── GET    /{id}/mods/versions/{provider}/{modId} # Get mod versions
│   │   ├── POST   /{id}/mods/install  # Install mod (provider, modId, versionId)
│   │   ├── PUT    /{id}/mods/{installedModId}/toggle # Enable/disable mod
│   │   ├── POST   /{id}/mods/{installedModId}/update # Update mod
│   │   ├── DELETE /{id}/mods/{installedModId} # Uninstall mod
│   │   └── GET    /{id}/mods/updates  # Check for available updates
│   │
│   ├── /backups
│   │   ├── GET    /{id}/backups       # List backups
│   │   ├── POST   /{id}/backups       # Create backup
│   │   ├── POST   /{id}/backups/{backupId}/restore # Restore backup
│   │   └── DELETE /{id}/backups/{backupId} # Delete backup
│   │
│   └── /schedule
│       ├── GET    /{id}/schedule      # List scheduled tasks
│       ├── POST   /{id}/schedule      # Create task
│       ├── PUT    /{id}/schedule/{taskId} # Update task
│       └── DELETE /{id}/schedule/{taskId} # Delete task
│
├── /users
│   ├── GET    /                   # List users (admin)
│   ├── GET    /{id}               # Get user details
│   ├── POST   /                   # Create user (admin)
│   ├── PUT    /{id}               # Update user
│   └── DELETE /{id}               # Delete user (admin)
│
├── /roles
│   ├── GET    /                   # List roles
│   ├── GET    /{id}               # Get role details
│   ├── POST   /                   # Create role
│   ├── PUT    /{id}               # Update role
│   └── DELETE /{id}               # Delete role
│
└── /system
    ├── GET    /metrics            # Host system metrics
    ├── GET    /health             # Health check
    └── GET    /info               # System information
```

---

## Scheduled Tasks

```csharp
public class ScheduledTask
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public string Name { get; set; }
    public ScheduledTaskType Type { get; set; }
    public string CronExpression { get; set; }           // "0 4 * * *" = 4 AM daily
    public Dictionary<string, object> Parameters { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public TaskRunStatus LastRunStatus { get; set; }
    
    public GameServer Server { get; set; }
}

public enum ScheduledTaskType
{
    Restart,            // Restart the server
    Backup,             // Create a backup
    Update,             // Update server/mods
    Command,            // Send console command
    Start,              // Start server
    Stop                // Stop server
}
```

---

## Security Considerations

### Authentication Flow

1. User submits credentials → Backend validates → Issues JWT + Refresh Token
2. JWT stored in memory, Refresh Token in HttpOnly cookie
3. JWT expires in 15 minutes, Refresh Token in 7 days
4. SignalR connections authenticated via JWT

### Authorization

1. Global permissions checked via role membership
2. Per-server permissions checked via `ServerPermission` overrides
3. Resource ownership always grants full access
4. Admin role bypasses all permission checks

### Input Validation

1. All file paths validated to prevent directory traversal
2. Console commands sanitized based on game-specific rules
3. Docker configurations validated before container creation
4. Rate limiting on sensitive endpoints

---

## Deployment

### Docker Compose Example

#### Simple Deployment (SQLite - Recommended for most users)

```yaml
version: '3.8'

services:
  nebula-panel:
    image: ghcr.io/nebula-codes/nebula-panel:latest
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      # SQLite is used by default - no configuration needed
      # Database file stored at /app/data/nebula.db
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ./data:/app/data              # SQLite database + app data
      - ./servers:/app/servers        # Game server files
      - ./backups:/app/backups        # Backup storage
    restart: unless-stopped
```

#### Advanced Deployment (PostgreSQL - For large-scale hosting)

```yaml
version: '3.8'

services:
  nebula-panel:
    image: ghcr.io/nebula-codes/nebula-panel:latest
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Database__Provider=PostgreSQL
      - Database__ConnectionString=Host=postgres;Database=nebula;Username=nebula;Password=secret
      - Redis__Connection=redis:6379
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ./data:/app/data
      - ./servers:/app/servers
      - ./backups:/app/backups
    depends_on:
      - postgres
      - redis
    restart: unless-stopped

  postgres:
    image: postgres:16-alpine
    environment:
      - POSTGRES_USER=nebula
      - POSTGRES_PASSWORD=secret
      - POSTGRES_DB=nebula
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: unless-stopped

  redis:
    image: redis:7-alpine
    volumes:
      - redis_data:/data
    restart: unless-stopped

volumes:
  postgres_data:
  redis_data:
```

---

### Database Configuration

Nebula Panel supports multiple database providers through Entity Framework Core. SQLite is the default for simplicity.

#### appsettings.json

```json
{
  "Database": {
    "Provider": "SQLite",
    "ConnectionString": "Data Source=data/nebula.db"
  }
}
```

#### Supported Providers

| Provider | Use Case | Configuration |
|----------|----------|---------------|
| **SQLite** (default) | Single-instance deployments, most self-hosted users | `"Provider": "SQLite"` |
| **PostgreSQL** | Large-scale hosting, multiple instances, high concurrency | `"Provider": "PostgreSQL"` |
| **MySQL/MariaDB** | Alternative for users with existing MySQL infrastructure | `"Provider": "MySQL"` |

#### Provider-Specific Connection Strings

```json
// SQLite (default)
{
  "Database": {
    "Provider": "SQLite",
    "ConnectionString": "Data Source=data/nebula.db"
  }
}

// PostgreSQL
{
  "Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "Host=localhost;Database=nebula;Username=nebula;Password=secret"
  }
}

// MySQL/MariaDB
{
  "Database": {
    "Provider": "MySQL",
    "ConnectionString": "Server=localhost;Database=nebula;User=nebula;Password=secret"
  }
}
```

#### Database Provider Factory

```csharp
public static class DatabaseConfiguration
{
    public static IServiceCollection AddNebulaDatabase(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var dbConfig = configuration.GetSection("Database");
        var provider = dbConfig.GetValue<string>("Provider") ?? "SQLite";
        var connectionString = dbConfig.GetValue<string>("ConnectionString") 
            ?? "Data Source=data/nebula.db";
        
        services.AddDbContext<NebulaPanelDbContext>(options =>
        {
            _ = provider.ToLowerInvariant() switch
            {
                "sqlite" => options.UseSqlite(connectionString, o => 
                    o.MigrationsAssembly("NebulaPanel.Infrastructure")),
                    
                "postgresql" or "postgres" => options.UseNpgsql(connectionString, o => 
                    o.MigrationsAssembly("NebulaPanel.Infrastructure")),
                    
                "mysql" or "mariadb" => options.UseMySql(
                    connectionString, 
                    ServerVersion.AutoDetect(connectionString),
                    o => o.MigrationsAssembly("NebulaPanel.Infrastructure")),
                    
                _ => throw new InvalidOperationException($"Unsupported database provider: {provider}")
            };
        });
        
        return services;
    }
}
```

#### SQLite Considerations

SQLite is ideal for Nebula Panel because:

- **Zero configuration**: Works out of the box
- **Low resource usage**: No separate database process
- **Simple backups**: Just copy the `.db` file
- **Sufficient performance**: Handles hundreds of servers and users easily
- **WAL mode**: Enabled by default for better concurrent read performance

```csharp
// SQLite-specific optimizations applied automatically
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    if (optionsBuilder.IsConfigured) return;
    
    optionsBuilder.UseSqlite("Data Source=data/nebula.db", options =>
    {
        options.CommandTimeout(30);
    });
}

// In NebulaPanelDbContext.OnModelCreating or via raw SQL on startup
// Enable WAL mode for better concurrency
// PRAGMA journal_mode=WAL;
// PRAGMA synchronous=NORMAL;
// PRAGMA cache_size=-64000; // 64MB cache
```

#### When to Use PostgreSQL

Consider PostgreSQL if you:

- Run a game server hosting company with 1000+ servers
- Need multiple Nebula Panel instances sharing one database
- Require advanced full-text search capabilities
- Need fine-grained row-level locking for high write concurrency
- Want to use PostgreSQL-specific features like LISTEN/NOTIFY for real-time events

---

### Caching Configuration

Nebula Panel uses in-memory caching by default. Redis is optional for distributed deployments.

```json
{
  "Cache": {
    "Provider": "Memory"
  }
}

// Or for distributed deployments:
{
  "Cache": {
    "Provider": "Redis",
    "ConnectionString": "localhost:6379"
  }
}
```

| Provider | Use Case |
|----------|----------|
| **Memory** (default) | Single-instance deployments |
| **Redis** | Multiple instances, horizontal scaling, persistent cache |

---

## Self-Update System

Nebula Panel includes a built-in update system that allows administrators to update the panel directly from the web UI with zero data loss.

### Update Flow Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           UPDATE FLOW                                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  1. CHECK          2. NOTIFY           3. CONFIRM         4. PREPARE        │
│  ┌─────────┐      ┌─────────────┐      ┌──────────┐      ┌─────────────┐   │
│  │ GitHub  │ ──── │ UI Banner   │ ──── │  Modal   │ ──── │ Stop Game   │   │
│  │ Release │      │ "Update     │      │ "Update  │      │ Servers     │   │
│  │ Check   │      │ Available"  │      │ Now?"    │      │ (Optional)  │   │
│  └─────────┘      └─────────────┘      └──────────┘      └─────────────┘   │
│                                                                  │          │
│  8. COMPLETE       7. RESTART          6. APPLY          5. BACKUP         │
│  ┌─────────────┐  ┌─────────────┐      ┌──────────┐      ┌─────────────┐   │
│  │ Show "What's│  │  Updater    │ ──── │ Download │ ──── │ Database +  │   │
│  │ New" + Ver  │  │  Restarts   │      │ Extract  │      │ Config +    │   │
│  │             │◄─│  Main App   │      │ Verify   │      │ Custom Data │   │
│  └─────────────┘  └─────────────┘      └──────────┘      └─────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Architecture

The update system uses a two-process architecture:
1. **Main Application** (`NebulaPanel.Web`) - The panel itself
2. **Updater Service** (`NebulaPanel.Updater`) - Lightweight process that performs updates

```
┌─────────────────────────────────────────────────────────────────┐
│                         Host System                              │
│                                                                  │
│  ┌──────────────────────┐      ┌──────────────────────┐        │
│  │   NebulaPanel.Web    │      │  NebulaPanel.Updater │        │
│  │   (Main App)         │◄────►│  (Background Service)│        │
│  │                      │ IPC  │                      │        │
│  │  - Web UI            │      │  - Version checking  │        │
│  │  - API               │      │  - Download updates  │        │
│  │  - SignalR           │      │  - Backup creation   │        │
│  │  - Game management   │      │  - File replacement  │        │
│  └──────────────────────┘      │  - App restart       │        │
│                                └──────────────────────┘        │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    Shared Data                            │  │
│  │  /app/data/nebula.db    - Database                       │  │
│  │  /app/data/appsettings  - Configuration                  │  │
│  │  /app/data/backups/     - Update backups                 │  │
│  │  /app/data/updates/     - Downloaded updates             │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### Core Components

#### Update Configuration

```csharp
public class UpdateConfiguration
{
    public bool Enabled { get; set; } = true;
    public bool AutoCheck { get; set; } = true;
    public int CheckIntervalHours { get; set; } = 6;
    public UpdateChannel Channel { get; set; } = UpdateChannel.Stable;
    public string UpdateServerUrl { get; set; } = "https://api.github.com/repos/nebula-codes/nebula-panel/releases";
    public bool AutoBackup { get; set; } = true;
    public int KeepBackupCount { get; set; } = 5;
    public bool StopServersBeforeUpdate { get; set; } = false;  // User preference
    public List<string> AdditionalBackupPaths { get; set; } = new();
}

public enum UpdateChannel
{
    Stable,     // Only stable releases
    Preview,    // Include pre-releases
    Development // Include all builds (for testing)
}
```

#### Version & Release Models

```csharp
public record VersionInfo
{
    public string Version { get; init; }              // "1.2.3"
    public string? PreRelease { get; init; }          // "beta.1", "rc.2", null for stable
    public int Major => int.Parse(Version.Split('.')[0]);
    public int Minor => int.Parse(Version.Split('.')[1]);
    public int Patch => int.Parse(Version.Split('.')[2]);
    public bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);
    
    public string FullVersion => IsPreRelease ? $"{Version}-{PreRelease}" : Version;
    
    public static VersionInfo Parse(string version)
    {
        var parts = version.TrimStart('v').Split('-', 2);
        return new VersionInfo
        {
            Version = parts[0],
            PreRelease = parts.Length > 1 ? parts[1] : null
        };
    }
    
    public bool IsNewerThan(VersionInfo other)
    {
        if (Major != other.Major) return Major > other.Major;
        if (Minor != other.Minor) return Minor > other.Minor;
        if (Patch != other.Patch) return Patch > other.Patch;
        
        // Both stable = equal
        if (!IsPreRelease && !other.IsPreRelease) return false;
        // Stable > PreRelease
        if (!IsPreRelease && other.IsPreRelease) return true;
        if (IsPreRelease && !other.IsPreRelease) return false;
        // Compare pre-release strings
        return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal) > 0;
    }
}

public record ReleaseInfo
{
    public string Version { get; init; }
    public string Name { get; init; }
    public string Body { get; init; }                 // Changelog markdown
    public DateTime PublishedAt { get; init; }
    public bool IsPreRelease { get; init; }
    public List<ReleaseAsset> Assets { get; init; } = new();
    public string? MinimumUpgradeVersion { get; init; } // Minimum version to upgrade from
    public bool RequiresDatabaseMigration { get; init; }
    public List<string> BreakingChanges { get; init; } = new();
}

public record ReleaseAsset
{
    public string Name { get; init; }                 // "nebula-panel-linux-x64.tar.gz"
    public string DownloadUrl { get; init; }
    public long Size { get; init; }
    public string? Sha256 { get; init; }
    public PlatformTarget Platform { get; init; }
}

public enum PlatformTarget
{
    LinuxX64,
    LinuxArm64,
    WindowsX64,
    Docker
}
```

#### Update Service Interface

```csharp
public interface IUpdateService
{
    /// <summary>
    /// Get current application version.
    /// </summary>
    VersionInfo CurrentVersion { get; }
    
    /// <summary>
    /// Get the current update status.
    /// </summary>
    UpdateStatus Status { get; }
    
    /// <summary>
    /// Check for available updates.
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get detailed information about an available update.
    /// </summary>
    Task<ReleaseInfo?> GetReleaseInfoAsync(string version, CancellationToken ct = default);
    
    /// <summary>
    /// Download an update (does not apply it).
    /// </summary>
    Task<UpdateDownloadResult> DownloadUpdateAsync(string version, 
        IProgress<UpdateProgress>? progress = null, CancellationToken ct = default);
    
    /// <summary>
    /// Apply a downloaded update. This will trigger app shutdown and restart.
    /// </summary>
    Task<UpdateApplyResult> ApplyUpdateAsync(string version, UpdateOptions options,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get list of available backups.
    /// </summary>
    Task<IReadOnlyList<UpdateBackup>> GetBackupsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Restore from a backup.
    /// </summary>
    Task<RestoreResult> RestoreBackupAsync(string backupId, CancellationToken ct = default);
    
    /// <summary>
    /// Event fired when update status changes.
    /// </summary>
    event EventHandler<UpdateStatusChangedEventArgs> StatusChanged;
}

public record UpdateCheckResult
{
    public bool UpdateAvailable { get; init; }
    public VersionInfo? LatestVersion { get; init; }
    public ReleaseInfo? LatestRelease { get; init; }
    public bool IsSecurityUpdate { get; init; }
    public bool IsMajorUpdate { get; init; }
    public DateTime CheckedAt { get; init; }
}

public record UpdateOptions
{
    public bool CreateBackup { get; init; } = true;
    public bool StopGameServers { get; init; } = false;
    public bool RestartGameServers { get; init; } = true;
    public List<string> AdditionalBackupPaths { get; init; } = new();
}

public enum UpdateStatus
{
    Idle,
    Checking,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    CreatingBackup,
    StoppingServers,
    Installing,
    Restarting,
    Failed
}

public record UpdateProgress
{
    public UpdateProgressPhase Phase { get; init; }
    public double Percentage { get; init; }
    public string Message { get; init; }
    public long? BytesDownloaded { get; init; }
    public long? TotalBytes { get; init; }
}

public enum UpdateProgressPhase
{
    Preparing,
    Downloading,
    Verifying,
    CreatingBackup,
    StoppingServers,
    ExtractingFiles,
    UpdatingDatabase,
    StartingServices,
    Completing
}
```

#### Update Service Implementation

```csharp
public class UpdateService : IUpdateService, IHostedService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly IOptions<UpdateConfiguration> _config;
    private readonly HttpClient _http;
    private readonly IGameServerService _serverService;
    private readonly IBackupService _backupService;
    private readonly IHubContext<UpdateHub, IUpdateHubClient> _hubContext;
    
    private readonly string _updateDir;
    private readonly string _backupDir;
    private readonly Timer _checkTimer;
    
    public VersionInfo CurrentVersion { get; }
    public UpdateStatus Status { get; private set; } = UpdateStatus.Idle;
    
    public event EventHandler<UpdateStatusChangedEventArgs>? StatusChanged;
    
    public UpdateService(
        ILogger<UpdateService> logger,
        IOptions<UpdateConfiguration> config,
        IHttpClientFactory httpFactory,
        IGameServerService serverService,
        IBackupService backupService,
        IHubContext<UpdateHub, IUpdateHubClient> hubContext)
    {
        _logger = logger;
        _config = config;
        _http = httpFactory.CreateClient("GitHub");
        _serverService = serverService;
        _backupService = backupService;
        _hubContext = hubContext;
        
        _updateDir = Path.Combine(AppContext.BaseDirectory, "data", "updates");
        _backupDir = Path.Combine(AppContext.BaseDirectory, "data", "backups", "updates");
        
        CurrentVersion = VersionInfo.Parse(
            Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.0.0");
        
        Directory.CreateDirectory(_updateDir);
        Directory.CreateDirectory(_backupDir);
    }
    
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        await SetStatusAsync(UpdateStatus.Checking);
        
        try
        {
            var releases = await _http.GetFromJsonAsync<List<GitHubRelease>>(
                _config.Value.UpdateServerUrl, ct);
            
            var eligibleReleases = releases!
                .Where(r => _config.Value.Channel != UpdateChannel.Stable || !r.Prerelease)
                .Where(r => _config.Value.Channel == UpdateChannel.Development || !r.Draft)
                .ToList();
            
            var latest = eligibleReleases.FirstOrDefault();
            if (latest == null)
            {
                await SetStatusAsync(UpdateStatus.Idle);
                return new UpdateCheckResult { UpdateAvailable = false, CheckedAt = DateTime.UtcNow };
            }
            
            var latestVersion = VersionInfo.Parse(latest.TagName);
            var updateAvailable = latestVersion.IsNewerThan(CurrentVersion);
            
            await SetStatusAsync(updateAvailable ? UpdateStatus.UpdateAvailable : UpdateStatus.Idle);
            
            var result = new UpdateCheckResult
            {
                UpdateAvailable = updateAvailable,
                LatestVersion = latestVersion,
                LatestRelease = MapToReleaseInfo(latest),
                IsSecurityUpdate = latest.Body?.Contains("[SECURITY]", StringComparison.OrdinalIgnoreCase) ?? false,
                IsMajorUpdate = latestVersion.Major > CurrentVersion.Major,
                CheckedAt = DateTime.UtcNow
            };
            
            // Notify all connected clients
            await _hubContext.Clients.All.UpdateCheckCompleted(result);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            await SetStatusAsync(UpdateStatus.Idle);
            throw;
        }
    }
    
    public async Task<UpdateDownloadResult> DownloadUpdateAsync(string version,
        IProgress<UpdateProgress>? progress = null, CancellationToken ct = default)
    {
        await SetStatusAsync(UpdateStatus.Downloading);
        
        try
        {
            var release = await GetReleaseInfoAsync(version, ct);
            if (release == null)
            {
                return new UpdateDownloadResult { Success = false, Error = "Release not found" };
            }
            
            // Find appropriate asset for this platform
            var platform = GetCurrentPlatform();
            var asset = release.Assets.FirstOrDefault(a => a.Platform == platform)
                ?? release.Assets.FirstOrDefault(a => a.Name.Contains("linux-x64"));
            
            if (asset == null)
            {
                return new UpdateDownloadResult { Success = false, Error = "No compatible download found" };
            }
            
            var downloadPath = Path.Combine(_updateDir, $"{version}.tar.gz");
            
            // Download with progress
            using var response = await _http.GetAsync(asset.DownloadUrl, 
                HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? asset.Size;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = File.Create(downloadPath);
            
            var buffer = new byte[81920];
            long bytesDownloaded = 0;
            int read;
            
            while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesDownloaded += read;
                
                var progressReport = new UpdateProgress
                {
                    Phase = UpdateProgressPhase.Downloading,
                    Percentage = (double)bytesDownloaded / totalBytes * 100,
                    Message = $"Downloading... {bytesDownloaded / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB",
                    BytesDownloaded = bytesDownloaded,
                    TotalBytes = totalBytes
                };
                
                progress?.Report(progressReport);
                await _hubContext.Clients.All.UpdateProgressChanged(progressReport);
            }
            
            // Verify checksum
            if (!string.IsNullOrEmpty(asset.Sha256))
            {
                progress?.Report(new UpdateProgress 
                { 
                    Phase = UpdateProgressPhase.Verifying, 
                    Message = "Verifying download..." 
                });
                
                fileStream.Position = 0;
                using var sha256 = SHA256.Create();
                var hash = await sha256.ComputeHashAsync(fileStream, ct);
                var hashString = Convert.ToHexString(hash);
                
                if (!hashString.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(downloadPath);
                    return new UpdateDownloadResult 
                    { 
                        Success = false, 
                        Error = "Checksum verification failed" 
                    };
                }
            }
            
            await SetStatusAsync(UpdateStatus.ReadyToInstall);
            
            return new UpdateDownloadResult
            {
                Success = true,
                DownloadPath = downloadPath,
                Version = version
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download update {Version}", version);
            await SetStatusAsync(UpdateStatus.Failed);
            return new UpdateDownloadResult { Success = false, Error = ex.Message };
        }
    }
    
    public async Task<UpdateApplyResult> ApplyUpdateAsync(string version, UpdateOptions options,
        CancellationToken ct = default)
    {
        var downloadPath = Path.Combine(_updateDir, $"{version}.tar.gz");
        if (!File.Exists(downloadPath))
        {
            return new UpdateApplyResult { Success = false, Error = "Update not downloaded" };
        }
        
        try
        {
            // Phase 1: Create backup
            if (options.CreateBackup)
            {
                await SetStatusAsync(UpdateStatus.CreatingBackup);
                await _hubContext.Clients.All.UpdateProgressChanged(new UpdateProgress
                {
                    Phase = UpdateProgressPhase.CreatingBackup,
                    Message = "Creating backup..."
                });
                
                var backup = await CreateUpdateBackupAsync(version, options, ct);
                _logger.LogInformation("Created backup: {BackupId}", backup.Id);
            }
            
            // Phase 2: Stop game servers (optional)
            List<Guid>? runningServerIds = null;
            if (options.StopGameServers)
            {
                await SetStatusAsync(UpdateStatus.StoppingServers);
                await _hubContext.Clients.All.UpdateProgressChanged(new UpdateProgress
                {
                    Phase = UpdateProgressPhase.StoppingServers,
                    Message = "Stopping game servers..."
                });
                
                var runningServers = await _serverService.GetRunningServersAsync(ct);
                runningServerIds = runningServers.Select(s => s.Id).ToList();
                
                foreach (var server in runningServers)
                {
                    await _serverService.StopAsync(server.Id, ct);
                }
            }
            
            // Phase 3: Signal updater to take over
            await SetStatusAsync(UpdateStatus.Installing);
            await _hubContext.Clients.All.UpdateProgressChanged(new UpdateProgress
            {
                Phase = UpdateProgressPhase.ExtractingFiles,
                Message = "Installing update... The panel will restart shortly."
            });
            
            // Write update manifest for the updater process
            var manifest = new UpdateManifest
            {
                Version = version,
                DownloadPath = downloadPath,
                BackupId = options.CreateBackup ? GetLatestBackupId() : null,
                RestartServerIds = options.RestartGameServers ? runningServerIds : null,
                Timestamp = DateTime.UtcNow
            };
            
            var manifestPath = Path.Combine(_updateDir, "pending-update.json");
            await File.WriteAllTextAsync(manifestPath, 
                JsonSerializer.Serialize(manifest), ct);
            
            // Signal the updater and initiate graceful shutdown
            _logger.LogInformation("Initiating update to version {Version}", version);
            
            // Give clients time to receive the final message
            await Task.Delay(1000, ct);
            
            // Request application shutdown - the updater will take over
            Environment.Exit(100); // Exit code 100 = update requested
            
            return new UpdateApplyResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply update {Version}", version);
            await SetStatusAsync(UpdateStatus.Failed);
            return new UpdateApplyResult { Success = false, Error = ex.Message };
        }
    }
    
    private async Task<UpdateBackup> CreateUpdateBackupAsync(string forVersion, 
        UpdateOptions options, CancellationToken ct)
    {
        var backupId = $"pre-update-{forVersion}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var backupPath = Path.Combine(_backupDir, backupId);
        Directory.CreateDirectory(backupPath);
        
        // Backup database
        var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "nebula.db");
        if (File.Exists(dbPath))
        {
            File.Copy(dbPath, Path.Combine(backupPath, "nebula.db"));
            
            // Also copy WAL and SHM files if they exist
            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";
            if (File.Exists(walPath)) File.Copy(walPath, Path.Combine(backupPath, "nebula.db-wal"));
            if (File.Exists(shmPath)) File.Copy(shmPath, Path.Combine(backupPath, "nebula.db-shm"));
        }
        
        // Backup configuration
        var configDir = Path.Combine(AppContext.BaseDirectory, "data");
        foreach (var configFile in Directory.GetFiles(configDir, "appsettings*.json"))
        {
            File.Copy(configFile, Path.Combine(backupPath, Path.GetFileName(configFile)));
        }
        
        // Backup additional paths
        foreach (var additionalPath in options.AdditionalBackupPaths)
        {
            if (Directory.Exists(additionalPath))
            {
                CopyDirectory(additionalPath, Path.Combine(backupPath, Path.GetFileName(additionalPath)));
            }
            else if (File.Exists(additionalPath))
            {
                File.Copy(additionalPath, Path.Combine(backupPath, Path.GetFileName(additionalPath)));
            }
        }
        
        // Write backup metadata
        var backup = new UpdateBackup
        {
            Id = backupId,
            CreatedAt = DateTime.UtcNow,
            FromVersion = CurrentVersion.FullVersion,
            ForVersion = forVersion,
            Path = backupPath,
            SizeBytes = GetDirectorySize(backupPath)
        };
        
        await File.WriteAllTextAsync(
            Path.Combine(backupPath, "backup.json"),
            JsonSerializer.Serialize(backup),
            ct);
        
        // Cleanup old backups
        await CleanupOldBackupsAsync(ct);
        
        return backup;
    }
    
    private async Task SetStatusAsync(UpdateStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, new UpdateStatusChangedEventArgs(status));
        await _hubContext.Clients.All.UpdateStatusChanged(status);
    }
}
```

#### Updater Process

The updater is a separate lightweight process that handles the actual file replacement:

```csharp
// NebulaPanel.Updater/Program.cs
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var appDir = args.Length > 0 ? args[0] : FindAppDirectory();
        var manifestPath = Path.Combine(appDir, "data", "updates", "pending-update.json");
        
        if (!File.Exists(manifestPath))
        {
            Console.WriteLine("No pending update found.");
            return 0;
        }
        
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(
            await File.ReadAllTextAsync(manifestPath));
        
        Console.WriteLine($"Applying update to version {manifest.Version}...");
        
        try
        {
            // Wait for main process to fully exit
            await WaitForProcessExitAsync("NebulaPanel.Web", TimeSpan.FromSeconds(30));
            
            // Extract update
            Console.WriteLine("Extracting files...");
            var extractDir = Path.Combine(appDir, "data", "updates", "extracted");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            Directory.CreateDirectory(extractDir);
            
            await ExtractTarGzAsync(manifest.DownloadPath, extractDir);
            
            // Replace files (preserve data directory)
            Console.WriteLine("Replacing application files...");
            var filesToPreserve = new[] { "data", "appsettings.local.json" };
            
            foreach (var file in Directory.GetFiles(appDir))
            {
                var fileName = Path.GetFileName(file);
                if (!filesToPreserve.Contains(fileName))
                {
                    File.Delete(file);
                }
            }
            
            foreach (var dir in Directory.GetDirectories(appDir))
            {
                var dirName = Path.GetFileName(dir);
                if (!filesToPreserve.Contains(dirName))
                {
                    Directory.Delete(dir, true);
                }
            }
            
            // Copy new files
            CopyDirectory(extractDir, appDir, overwrite: true);
            
            // Run database migrations if needed
            Console.WriteLine("Running database migrations...");
            await RunMigrationsAsync(appDir);
            
            // Clean up
            File.Delete(manifestPath);
            File.Delete(manifest.DownloadPath);
            Directory.Delete(extractDir, true);
            
            // Write update completion marker
            var completionMarker = new UpdateCompletionMarker
            {
                Version = manifest.Version,
                CompletedAt = DateTime.UtcNow,
                RestartServerIds = manifest.RestartServerIds
            };
            
            await File.WriteAllTextAsync(
                Path.Combine(appDir, "data", "updates", "completed-update.json"),
                JsonSerializer.Serialize(completionMarker));
            
            // Start the main application
            Console.WriteLine("Starting Nebula Panel...");
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(appDir, "NebulaPanel.Web"),
                WorkingDirectory = appDir,
                UseShellExecute = false
            };
            
            Process.Start(startInfo);
            
            Console.WriteLine($"Update to {manifest.Version} completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update failed: {ex.Message}");
            
            // Attempt rollback if backup exists
            if (manifest.BackupId != null)
            {
                Console.WriteLine("Attempting rollback...");
                await RollbackAsync(appDir, manifest.BackupId);
            }
            
            return 1;
        }
    }
    
    private static async Task RollbackAsync(string appDir, string backupId)
    {
        var backupPath = Path.Combine(appDir, "data", "backups", "updates", backupId);
        
        if (!Directory.Exists(backupPath))
        {
            Console.WriteLine("Backup not found, cannot rollback.");
            return;
        }
        
        // Restore database
        var dbBackup = Path.Combine(backupPath, "nebula.db");
        if (File.Exists(dbBackup))
        {
            File.Copy(dbBackup, Path.Combine(appDir, "data", "nebula.db"), overwrite: true);
        }
        
        // Restore config files
        foreach (var configFile in Directory.GetFiles(backupPath, "appsettings*.json"))
        {
            File.Copy(configFile, Path.Combine(appDir, "data", Path.GetFileName(configFile)), 
                overwrite: true);
        }
        
        Console.WriteLine("Rollback completed. Starting previous version...");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(appDir, "NebulaPanel.Web"),
            WorkingDirectory = appDir,
            UseShellExecute = false
        };
        
        Process.Start(startInfo);
    }
}
```

#### Wrapper Script (Linux/Docker)

For proper process management, use a wrapper script:

```bash
#!/bin/bash
# /app/start.sh - Entry point wrapper

APP_DIR="/app"
MAIN_APP="$APP_DIR/NebulaPanel.Web"
UPDATER="$APP_DIR/NebulaPanel.Updater"

while true; do
    # Run the main application
    "$MAIN_APP"
    EXIT_CODE=$?
    
    if [ $EXIT_CODE -eq 100 ]; then
        # Exit code 100 = update requested
        echo "Update requested, running updater..."
        "$UPDATER" "$APP_DIR"
        UPDATER_EXIT=$?
        
        if [ $UPDATER_EXIT -ne 0 ]; then
            echo "Updater failed with code $UPDATER_EXIT"
            sleep 5
        fi
        
        # Loop continues, starting updated app
    elif [ $EXIT_CODE -eq 0 ]; then
        # Clean exit
        echo "Application exited normally."
        exit 0
    else
        # Crash - restart after delay
        echo "Application crashed with code $EXIT_CODE, restarting in 5s..."
        sleep 5
    fi
done
```

### SignalR Hub for Real-Time Updates

```csharp
public interface IUpdateHubClient
{
    Task UpdateStatusChanged(UpdateStatus status);
    Task UpdateProgressChanged(UpdateProgress progress);
    Task UpdateCheckCompleted(UpdateCheckResult result);
    Task UpdateAvailable(ReleaseInfo release);
}

public class UpdateHub : Hub<IUpdateHubClient>
{
    private readonly IUpdateService _updateService;
    
    public UpdateHub(IUpdateService updateService)
    {
        _updateService = updateService;
    }
    
    public override async Task OnConnectedAsync()
    {
        // Send current status to newly connected client
        await Clients.Caller.UpdateStatusChanged(_updateService.Status);
        await base.OnConnectedAsync();
    }
    
    public async Task CheckForUpdates()
    {
        var result = await _updateService.CheckForUpdatesAsync();
        await Clients.Caller.UpdateCheckCompleted(result);
    }
}
```

### UI Components

#### Update Notification Banner

```razor
<!-- UpdateBanner.razor -->
@implements IAsyncDisposable
@inject IUpdateService UpdateService
@inject NavigationManager Nav

@if (_updateAvailable && _latestRelease != null)
{
    <div class="fixed top-0 left-0 right-0 z-50 bg-gradient-to-r from-nebula-accent to-purple-600 
                text-white px-4 py-2 shadow-lg">
        <div class="max-w-7xl mx-auto flex items-center justify-between">
            <div class="flex items-center gap-3">
                <Icon Name="download" class="w-5 h-5" />
                <span>
                    <strong>Nebula Panel @_latestRelease.Version</strong> is available!
                    @if (_latestRelease.IsSecurityUpdate)
                    {
                        <span class="ml-2 px-2 py-0.5 bg-red-500 rounded text-xs font-bold">
                            SECURITY UPDATE
                        </span>
                    }
                </span>
            </div>
            <div class="flex items-center gap-2">
                <button @onclick="ViewChangelog" 
                        class="px-3 py-1 text-sm bg-white/20 hover:bg-white/30 rounded transition">
                    What's New
                </button>
                <button @onclick="OpenUpdateModal"
                        class="px-3 py-1 text-sm bg-white text-nebula-accent font-medium rounded 
                               hover:bg-white/90 transition">
                    Update Now
                </button>
                <button @onclick="Dismiss" class="p-1 hover:bg-white/20 rounded">
                    <Icon Name="x" class="w-4 h-4" />
                </button>
            </div>
        </div>
    </div>
}

@code {
    private bool _updateAvailable;
    private ReleaseInfo? _latestRelease;
    private HubConnection? _hubConnection;
    
    protected override async Task OnInitializedAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(Nav.ToAbsoluteUri("/hubs/update"))
            .WithAutomaticReconnect()
            .Build();
        
        _hubConnection.On<UpdateCheckResult>("UpdateCheckCompleted", result =>
        {
            _updateAvailable = result.UpdateAvailable;
            _latestRelease = result.LatestRelease;
            InvokeAsync(StateHasChanged);
        });
        
        await _hubConnection.StartAsync();
    }
    
    // Additional methods...
}
```

#### Update Modal

```razor
<!-- UpdateModal.razor -->
@inject IUpdateService UpdateService

<Modal IsOpen="@IsOpen" OnClose="@OnClose" Size="ModalSize.Large">
    <Header>
        <div class="flex items-center gap-3">
            <div class="p-2 bg-nebula-accent/20 rounded-lg">
                <Icon Name="download" class="w-6 h-6 text-nebula-accent" />
            </div>
            <div>
                <h2 class="text-xl font-semibold">Update Nebula Panel</h2>
                <p class="text-sm text-nebula-text-muted">
                    @CurrentVersion → @Release?.Version
                </p>
            </div>
        </div>
    </Header>
    
    <Body>
        @switch (_phase)
        {
            case UpdatePhase.Confirm:
                <div class="space-y-4">
                    @if (Release?.BreakingChanges?.Any() == true)
                    {
                        <div class="p-4 bg-nebula-warning/10 border border-nebula-warning/30 rounded-lg">
                            <h4 class="font-medium text-nebula-warning flex items-center gap-2">
                                <Icon Name="alert-triangle" class="w-4 h-4" />
                                Breaking Changes
                            </h4>
                            <ul class="mt-2 text-sm space-y-1">
                                @foreach (var change in Release.BreakingChanges)
                                {
                                    <li>• @change</li>
                                }
                            </ul>
                        </div>
                    }
                    
                    <div class="prose prose-invert max-w-none">
                        <h4>Changelog</h4>
                        @((MarkupString)Markdig.Markdown.ToHtml(Release?.Body ?? ""))
                    </div>
                    
                    <div class="space-y-3 pt-4 border-t border-nebula-border">
                        <h4 class="font-medium">Update Options</h4>
                        
                        <label class="flex items-center gap-3">
                            <input type="checkbox" @bind="_createBackup" 
                                   class="rounded bg-nebula-input-bg border-nebula-border" />
                            <span>Create backup before updating (recommended)</span>
                        </label>
                        
                        <label class="flex items-center gap-3">
                            <input type="checkbox" @bind="_stopServers"
                                   class="rounded bg-nebula-input-bg border-nebula-border" />
                            <span>Stop game servers during update</span>
                        </label>
                        
                        @if (_stopServers)
                        {
                            <label class="flex items-center gap-3 ml-6">
                                <input type="checkbox" @bind="_restartServers"
                                       class="rounded bg-nebula-input-bg border-nebula-border" />
                                <span>Restart servers after update</span>
                            </label>
                        }
                    </div>
                </div>
                break;
                
            case UpdatePhase.Downloading:
            case UpdatePhase.Installing:
                <div class="py-8 text-center space-y-4">
                    <div class="relative w-24 h-24 mx-auto">
                        <svg class="w-24 h-24 transform -rotate-90">
                            <circle cx="48" cy="48" r="44" fill="none" 
                                    stroke="currentColor" stroke-width="8"
                                    class="text-nebula-bg-tertiary" />
                            <circle cx="48" cy="48" r="44" fill="none"
                                    stroke="currentColor" stroke-width="8"
                                    stroke-dasharray="@(276.46)" 
                                    stroke-dashoffset="@(276.46 * (1 - _progress.Percentage / 100))"
                                    class="text-nebula-accent transition-all duration-300" />
                        </svg>
                        <div class="absolute inset-0 flex items-center justify-center">
                            <span class="text-xl font-bold">@(_progress.Percentage.ToString("F0"))%</span>
                        </div>
                    </div>
                    
                    <div>
                        <p class="font-medium">@GetPhaseTitle(_progress.Phase)</p>
                        <p class="text-sm text-nebula-text-muted">@_progress.Message</p>
                    </div>
                    
                    @if (_progress.Phase == UpdateProgressPhase.ExtractingFiles)
                    {
                        <p class="text-sm text-nebula-warning">
                            <Icon Name="alert-circle" class="w-4 h-4 inline mr-1" />
                            Do not close this window. The panel will restart automatically.
                        </p>
                    }
                </div>
                break;
                
            case UpdatePhase.Complete:
                <div class="py-8 text-center space-y-4">
                    <div class="w-16 h-16 mx-auto bg-nebula-success/20 rounded-full 
                                flex items-center justify-center">
                        <Icon Name="check" class="w-8 h-8 text-nebula-success" />
                    </div>
                    <div>
                        <p class="text-xl font-medium">Update Complete!</p>
                        <p class="text-nebula-text-muted">
                            Nebula Panel has been updated to version @Release?.Version
                        </p>
                    </div>
                </div>
                break;
                
            case UpdatePhase.Failed:
                <div class="py-8 text-center space-y-4">
                    <div class="w-16 h-16 mx-auto bg-nebula-error/20 rounded-full 
                                flex items-center justify-center">
                        <Icon Name="x" class="w-8 h-8 text-nebula-error" />
                    </div>
                    <div>
                        <p class="text-xl font-medium">Update Failed</p>
                        <p class="text-nebula-text-muted">@_error</p>
                    </div>
                    @if (_backupAvailable)
                    {
                        <button @onclick="Rollback" class="btn-secondary">
                            <Icon Name="rotate-ccw" class="w-4 h-4 mr-2" />
                            Restore Backup
                        </button>
                    }
                </div>
                break;
        }
    </Body>
    
    <Footer>
        @if (_phase == UpdatePhase.Confirm)
        {
            <button @onclick="OnClose" class="btn-secondary">Cancel</button>
            <button @onclick="StartUpdate" class="btn-primary">
                <Icon Name="download" class="w-4 h-4 mr-2" />
                Download & Install
            </button>
        }
        else if (_phase == UpdatePhase.Complete)
        {
            <button @onclick="OnClose" class="btn-primary">Close</button>
        }
    </Footer>
</Modal>

@code {
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public ReleaseInfo? Release { get; set; }
    
    private string CurrentVersion => UpdateService.CurrentVersion.FullVersion;
    
    private UpdatePhase _phase = UpdatePhase.Confirm;
    private UpdateProgress _progress = new();
    private bool _createBackup = true;
    private bool _stopServers = false;
    private bool _restartServers = true;
    private string? _error;
    private bool _backupAvailable;
    
    private async Task StartUpdate()
    {
        if (Release == null) return;
        
        _phase = UpdatePhase.Downloading;
        StateHasChanged();
        
        // Download
        var downloadResult = await UpdateService.DownloadUpdateAsync(
            Release.Version,
            new Progress<UpdateProgress>(p =>
            {
                _progress = p;
                InvokeAsync(StateHasChanged);
            }));
        
        if (!downloadResult.Success)
        {
            _phase = UpdatePhase.Failed;
            _error = downloadResult.Error;
            return;
        }
        
        // Apply
        _phase = UpdatePhase.Installing;
        StateHasChanged();
        
        var applyResult = await UpdateService.ApplyUpdateAsync(
            Release.Version,
            new UpdateOptions
            {
                CreateBackup = _createBackup,
                StopGameServers = _stopServers,
                RestartGameServers = _restartServers
            });
        
        if (!applyResult.Success)
        {
            _phase = UpdatePhase.Failed;
            _error = applyResult.Error;
            _backupAvailable = _createBackup;
        }
        
        // If successful, the app will restart and we won't reach here
    }
    
    private enum UpdatePhase { Confirm, Downloading, Installing, Complete, Failed }
}
```

#### Post-Update Welcome Screen

```razor
<!-- WelcomeBack.razor - Shown after update completes -->
@page "/updated"
@inject IUpdateService UpdateService
@inject NavigationManager Nav

<div class="min-h-screen flex items-center justify-center bg-nebula-bg-primary">
    <div class="max-w-lg w-full mx-4">
        <div class="bg-nebula-card-bg rounded-nebula-lg border border-nebula-border p-8 text-center">
            <div class="w-20 h-20 mx-auto bg-gradient-to-br from-nebula-accent to-purple-600 
                        rounded-full flex items-center justify-center mb-6">
                <Icon Name="sparkles" class="w-10 h-10 text-white" />
            </div>
            
            <h1 class="text-2xl font-bold mb-2">Welcome to Nebula Panel @_version!</h1>
            <p class="text-nebula-text-muted mb-6">
                Your panel has been successfully updated.
            </p>
            
            @if (_changelog != null)
            {
                <div class="text-left bg-nebula-bg-secondary rounded-nebula p-4 mb-6 
                            max-h-64 overflow-y-auto prose prose-invert prose-sm">
                    <h4 class="text-sm font-medium text-nebula-text-muted mb-2">What's New</h4>
                    @((MarkupString)Markdig.Markdown.ToHtml(_changelog))
                </div>
            }
            
            @if (_restartedServers?.Any() == true)
            {
                <div class="text-left bg-nebula-success/10 border border-nebula-success/30 
                            rounded-nebula p-4 mb-6">
                    <p class="text-sm text-nebula-success">
                        <Icon Name="check-circle" class="w-4 h-4 inline mr-1" />
                        @_restartedServers.Count game server(s) have been restarted.
                    </p>
                </div>
            }
            
            <button @onclick="GoToDashboard" class="btn-primary w-full">
                Go to Dashboard
                <Icon Name="arrow-right" class="w-4 h-4 ml-2" />
            </button>
        </div>
    </div>
</div>

@code {
    private string? _version;
    private string? _changelog;
    private List<string>? _restartedServers;
    
    protected override async Task OnInitializedAsync()
    {
        // Read completion marker
        var markerPath = Path.Combine(AppContext.BaseDirectory, "data", "updates", "completed-update.json");
        if (File.Exists(markerPath))
        {
            var marker = JsonSerializer.Deserialize<UpdateCompletionMarker>(
                await File.ReadAllTextAsync(markerPath));
            
            _version = marker?.Version;
            
            // Get changelog for this version
            var release = await UpdateService.GetReleaseInfoAsync(_version);
            _changelog = release?.Body;
            
            // Clean up marker
            File.Delete(markerPath);
        }
        else
        {
            _version = UpdateService.CurrentVersion.FullVersion;
        }
    }
    
    private void GoToDashboard() => Nav.NavigateTo("/");
}
```

### API Endpoints

```
/api/v1/system/update
├── GET    /check              # Check for updates
├── GET    /current            # Get current version info
├── GET    /releases           # List available releases
├── GET    /releases/{version} # Get specific release info
├── POST   /download           # Download an update
├── POST   /apply              # Apply downloaded update
├── GET    /status             # Get current update status
├── GET    /backups            # List update backups
├── POST   /backups/{id}/restore # Restore from backup
└── DELETE /backups/{id}       # Delete a backup
```

---

## Development Milestones

### Phase 1: Foundation (Weeks 1-3)
- [ ] Project setup and architecture
- [ ] Database schema and migrations (SQLite default)
- [ ] User authentication and authorization
- [ ] Basic Blazor layout and theming system

### Phase 2: Core Server Management (Weeks 4-6)
- [ ] Game CRUD operations
- [ ] Server CRUD operations
- [ ] Native process executor
- [ ] Docker executor
- [ ] SteamCMD integration
- [ ] Server start/stop/restart

### Phase 3: Real-time Features (Weeks 7-8)
- [ ] SignalR console streaming
- [ ] Live metrics collection
- [ ] Resource monitoring (host + per-server)
- [ ] Status change notifications

### Phase 4: File & Mod Management (Weeks 9-10)
- [ ] Web-based file manager
- [ ] File upload/download
- [ ] Code editor integration
- [ ] Mod provider integrations (Modrinth, CurseForge, etc.)
- [ ] Unified mod search and installation

### Phase 5: Advanced Features (Weeks 11-12)
- [ ] Scheduled tasks (Hangfire)
- [ ] Backup system
- [ ] Configuration templates
- [ ] User management UI
- [ ] Role/permission management UI
- [ ] RCON integration

### Phase 6: Update System (Week 13)
- [ ] Version checking service
- [ ] Update download with progress
- [ ] Backup creation before updates
- [ ] Updater process implementation
- [ ] Post-update welcome screen
- [ ] Rollback functionality

### Phase 7: Polish & Testing (Weeks 14-15)
- [ ] UI/UX refinements
- [ ] Performance optimization
- [ ] Comprehensive testing
- [ ] Documentation
- [ ] Deployment guides

---

## Game Configuration Examples

### Configuration System Deep Dive

Games often have multiple configuration files in different formats. The configuration system supports this through:

```csharp
public class Game
{
    // ... other properties ...
    
    /// <summary>
    /// Dictionary of configuration file schemas, keyed by relative file path.
    /// Supports multiple files per game with different formats.
    /// </summary>
    public Dictionary<string, ConfigurationSchema> ConfigurationSchemas { get; set; }
}

public class ConfigurationSchema
{
    public string FilePath { get; set; }              // Relative path: "server.properties", "config/settings.json"
    public string DisplayName { get; set; }           // "Server Settings", "World Configuration"
    public string? Description { get; set; }
    public ConfigFileType FileType { get; set; }
    public bool CreateIfMissing { get; set; } = true;
    public string? Template { get; set; }             // Default file content template
    public List<ConfigField> Fields { get; set; }
    public List<ConfigSection>? Sections { get; set; } // For grouping fields in UI
}

public enum ConfigFileType
{
    Properties,     // Java .properties format (key=value)
    Json,           // JSON format
    Yaml,           // YAML format  
    Ini,            // INI format with [sections]
    Xml,            // XML format
    Toml,           // TOML format
    KeyValue,       // Simple key=value or key value (space separated)
    LineBased,      // Each line is a value (e.g., ban lists, whitelists)
    Custom          // Custom parser needed - provide regex or handler
}

public class ConfigField
{
    public string Key { get; set; }                   // The actual key in the file
    public string DisplayName { get; set; }           // Human-readable name
    public string? Description { get; set; }          // Help text
    public string? Category { get; set; }             // For grouping: "Network", "Gameplay", etc.
    public ConfigFieldType Type { get; set; }
    public object? DefaultValue { get; set; }
    public bool Required { get; set; }
    public bool Advanced { get; set; }                // Hidden unless "show advanced" enabled
    public List<SelectOption>? Options { get; set; } // For Select/MultiSelect types
    public ValidationRule? Validation { get; set; }
    public List<string>? DependsOn { get; set; }      // Only show if other fields have certain values
    public Dictionary<string, object>? ShowWhen { get; set; } // Conditional visibility
    
    // For nested configs (JSON/YAML)
    public string? JsonPath { get; set; }             // e.g., "server.network.port"
}

public enum ConfigFieldType
{
    String,
    Int,
    Float,
    Bool,
    Select,         // Single selection dropdown
    MultiSelect,    // Multiple selection (comma-separated or array)
    Text,           // Multi-line text
    Password,       // Hidden input
    Path,           // File/folder path with browser
    Color,          // Color picker
    DateTime,       // Date/time picker
    Duration,       // Time duration (e.g., "1h30m")
    ByteSize,       // Size with units (e.g., "4GB", "512MB")
    IpAddress,      // IP address validation
    Port,           // Port number with validation
    Slider,         // Numeric slider with min/max
    Toggle,         // On/Off toggle (alias for Bool)
    List,           // List of strings
    KeyValuePairs   // Dictionary/map editor
}

public class ValidationRule
{
    public int? Min { get; set; }
    public int? Max { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }              // Regex pattern
    public string? PatternError { get; set; }         // Custom error for pattern mismatch
    public List<object>? AllowedValues { get; set; }
    public List<object>? ForbiddenValues { get; set; }
}

public class ConfigSection
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public bool DefaultExpanded { get; set; } = true;
    public List<string> Fields { get; set; }          // Field keys in this section
}
```

#### Configuration File Parsers

```csharp
public interface IConfigFileParser
{
    ConfigFileType FileType { get; }
    Task<Dictionary<string, object>> ParseAsync(Stream stream, CancellationToken ct = default);
    Task WriteAsync(Stream stream, Dictionary<string, object> values, 
        ConfigurationSchema schema, CancellationToken ct = default);
    object? GetValue(Dictionary<string, object> data, string key, string? jsonPath = null);
    void SetValue(Dictionary<string, object> data, string key, object value, string? jsonPath = null);
}

public class ConfigParserFactory
{
    public IConfigFileParser GetParser(ConfigFileType type) => type switch
    {
        ConfigFileType.Properties => new PropertiesFileParser(),
        ConfigFileType.Json => new JsonFileParser(),
        ConfigFileType.Yaml => new YamlFileParser(),
        ConfigFileType.Ini => new IniFileParser(),
        ConfigFileType.Xml => new XmlFileParser(),
        ConfigFileType.Toml => new TomlFileParser(),
        ConfigFileType.KeyValue => new KeyValueFileParser(),
        ConfigFileType.LineBased => new LineBasedFileParser(),
        _ => throw new NotSupportedException($"No parser for {type}")
    };
}
```

---

### Example: Minecraft Java Edition (Multiple Config Files)

```json
{
  "name": "Minecraft Java Edition",
  "slug": "minecraft-java",
  "executableType": "Jar",
  "defaultExecutablePath": "server.jar",
  "defaultStartCommand": "-Xmx{maxMemory}M -Xms{minMemory}M -jar server.jar nogui",
  "supportsDocker": true,
  "defaultDockerImage": "itzg/minecraft-server",
  "supportsMods": true,
  "modProviders": [
    {
      "provider": "Modrinth",
      "enabled": true,
      "priority": 1,
      "gameSlug": "minecraft",
      "modInstallPath": "mods/",
      "providerSettings": {}
    },
    {
      "provider": "CurseForge",
      "enabled": true,
      "priority": 2,
      "gameSlug": "minecraft",
      "modInstallPath": "mods/",
      "providerSettings": {
        "gameId": "432"
      }
    },
    {
      "provider": "Hangar",
      "enabled": true,
      "priority": 3,
      "gameSlug": "paper",
      "modInstallPath": "plugins/",
      "providerSettings": {}
    },
    {
      "provider": "SpigotMC",
      "enabled": true,
      "priority": 4,
      "gameSlug": "spigot",
      "modInstallPath": "plugins/",
      "providerSettings": {}
    },
    {
      "provider": "Local",
      "enabled": true,
      "priority": 99,
      "modInstallPath": "mods/",
      "providerSettings": {}
    }
  ],
  "rconConfig": {
    "defaultEnabled": true,
    "protocol": "Minecraft",
    "defaultPort": 25575
  },
  "configurationSchemas": {
    "server.properties": {
      "displayName": "Server Settings",
      "description": "Main server configuration",
      "fileType": "Properties",
      "sections": [
        { "id": "network", "name": "Network", "icon": "globe", "fields": ["server-port", "server-ip", "query.port", "enable-query", "enable-rcon", "rcon.port", "rcon.password"] },
        { "id": "gameplay", "name": "Gameplay", "icon": "gamepad", "fields": ["gamemode", "difficulty", "pvp", "hardcore", "max-players"] },
        { "id": "world", "name": "World", "icon": "earth", "fields": ["level-name", "level-seed", "level-type", "generate-structures", "spawn-protection"] },
        { "id": "performance", "name": "Performance", "icon": "gauge", "fields": ["view-distance", "simulation-distance", "max-tick-time", "network-compression-threshold"] }
      ],
      "fields": [
        {
          "key": "server-port",
          "displayName": "Server Port",
          "type": "Port",
          "defaultValue": 25565,
          "category": "network"
        },
        {
          "key": "server-ip",
          "displayName": "Bind Address",
          "description": "Leave empty to bind to all interfaces",
          "type": "IpAddress",
          "defaultValue": "",
          "category": "network"
        },
        {
          "key": "enable-rcon",
          "displayName": "Enable RCON",
          "type": "Bool",
          "defaultValue": true,
          "category": "network"
        },
        {
          "key": "rcon.port",
          "displayName": "RCON Port",
          "type": "Port",
          "defaultValue": 25575,
          "category": "network",
          "showWhen": { "enable-rcon": true }
        },
        {
          "key": "rcon.password",
          "displayName": "RCON Password",
          "type": "Password",
          "required": true,
          "category": "network",
          "showWhen": { "enable-rcon": true },
          "validation": { "minLength": 8 }
        },
        {
          "key": "max-players",
          "displayName": "Max Players",
          "type": "Slider",
          "defaultValue": 20,
          "validation": { "min": 1, "max": 1000 },
          "category": "gameplay"
        },
        {
          "key": "gamemode",
          "displayName": "Default Game Mode",
          "type": "Select",
          "defaultValue": "survival",
          "options": [
            { "value": "survival", "label": "Survival" },
            { "value": "creative", "label": "Creative" },
            { "value": "adventure", "label": "Adventure" },
            { "value": "spectator", "label": "Spectator" }
          ],
          "category": "gameplay"
        },
        {
          "key": "difficulty",
          "displayName": "Difficulty",
          "type": "Select",
          "defaultValue": "normal",
          "options": [
            { "value": "peaceful", "label": "Peaceful" },
            { "value": "easy", "label": "Easy" },
            { "value": "normal", "label": "Normal" },
            { "value": "hard", "label": "Hard" }
          ],
          "category": "gameplay"
        },
        {
          "key": "pvp",
          "displayName": "PvP Enabled",
          "type": "Toggle",
          "defaultValue": true,
          "category": "gameplay"
        },
        {
          "key": "level-name",
          "displayName": "World Name",
          "type": "String",
          "defaultValue": "world",
          "category": "world"
        },
        {
          "key": "level-seed",
          "displayName": "World Seed",
          "description": "Leave empty for random",
          "type": "String",
          "defaultValue": "",
          "category": "world"
        },
        {
          "key": "view-distance",
          "displayName": "View Distance",
          "description": "Chunks sent to clients",
          "type": "Slider",
          "defaultValue": 10,
          "validation": { "min": 3, "max": 32 },
          "category": "performance"
        },
        {
          "key": "simulation-distance",
          "displayName": "Simulation Distance",
          "description": "Chunks that are actively ticked",
          "type": "Slider",
          "defaultValue": 10,
          "validation": { "min": 3, "max": 32 },
          "category": "performance",
          "advanced": true
        }
      ]
    },
    "eula.txt": {
      "displayName": "EULA Agreement",
      "fileType": "Properties",
      "fields": [
        {
          "key": "eula",
          "displayName": "Accept Minecraft EULA",
          "description": "You must accept the Minecraft EULA to run the server",
          "type": "Bool",
          "defaultValue": false,
          "required": true
        }
      ]
    },
    "whitelist.json": {
      "displayName": "Whitelist",
      "description": "Players allowed to join when whitelist is enabled",
      "fileType": "Json",
      "fields": [
        {
          "key": "players",
          "displayName": "Whitelisted Players",
          "type": "List",
          "defaultValue": []
        }
      ]
    },
    "ops.json": {
      "displayName": "Operators",
      "description": "Players with operator permissions",
      "fileType": "Json",
      "fields": [
        {
          "key": "operators",
          "displayName": "Server Operators",
          "type": "List",
          "defaultValue": []
        }
      ]
    },
    "banned-players.json": {
      "displayName": "Banned Players",
      "fileType": "Json",
      "fields": [
        {
          "key": "banned",
          "displayName": "Banned Players",
          "type": "List",
          "defaultValue": []
        }
      ]
    }
  }
}
```

---

### Example: ARK Survival Evolved (Complex Multi-File INI Configuration)

```json
{
  "name": "ARK: Survival Evolved",
  "slug": "ark-survival-evolved",
  "steamAppId": "376030",
  "executableType": "Exe",
  "defaultExecutablePath": "ShooterGame/Binaries/Win64/ShooterGameServer.exe",
  "defaultStartCommand": "{map}?listen?SessionName={sessionName}?ServerPassword={serverPassword}?ServerAdminPassword={adminPassword}?Port={port}?QueryPort={queryPort}?MaxPlayers={maxPlayers} -server -log",
  "supportsDocker": true,
  "defaultDockerImage": "hermsi/ark-server",
  "supportsMods": true,
  "modProviders": [
    {
      "provider": "SteamWorkshop",
      "enabled": true,
      "priority": 1,
      "gameSlug": "ark",
      "modInstallPath": "ShooterGame/Content/Mods/",
      "providerSettings": {
        "workshopAppId": "346110"
      }
    },
    {
      "provider": "Local",
      "enabled": true,
      "priority": 99,
      "modInstallPath": "ShooterGame/Content/Mods/",
      "providerSettings": {}
    }
  ],
  "rconConfig": {
    "defaultEnabled": true,
    "protocol": "Source",
    "defaultPort": 27020
  },
  "configurationSchemas": {
    "ShooterGame/Saved/Config/WindowsServer/GameUserSettings.ini": {
      "displayName": "Game User Settings",
      "description": "Primary server configuration",
      "fileType": "Ini",
      "sections": [
        { "id": "server", "name": "Server Settings", "fields": ["ServerSettings.ServerPassword", "ServerSettings.ServerAdminPassword", "ServerSettings.MaxPlayers"] },
        { "id": "rates", "name": "Rates & Multipliers", "fields": ["ServerSettings.TamingSpeedMultiplier", "ServerSettings.HarvestAmountMultiplier", "ServerSettings.XPMultiplier"] },
        { "id": "rules", "name": "Game Rules", "fields": ["ServerSettings.bAllowFlyerCarryPvE", "ServerSettings.bDisableStructureDecayPvE"] }
      ],
      "fields": [
        {
          "key": "ServerSettings.ServerPassword",
          "displayName": "Server Password",
          "description": "Password required to join (leave empty for no password)",
          "type": "Password",
          "defaultValue": "",
          "category": "server"
        },
        {
          "key": "ServerSettings.ServerAdminPassword",
          "displayName": "Admin Password",
          "description": "Password for admin commands",
          "type": "Password",
          "required": true,
          "category": "server",
          "validation": { "minLength": 6 }
        },
        {
          "key": "ServerSettings.MaxPlayers",
          "displayName": "Max Players",
          "type": "Slider",
          "defaultValue": 70,
          "validation": { "min": 1, "max": 127 },
          "category": "server"
        },
        {
          "key": "ServerSettings.TamingSpeedMultiplier",
          "displayName": "Taming Speed",
          "type": "Float",
          "defaultValue": 1.0,
          "validation": { "min": 0.1, "max": 100 },
          "category": "rates"
        },
        {
          "key": "ServerSettings.HarvestAmountMultiplier",
          "displayName": "Harvest Amount",
          "type": "Float",
          "defaultValue": 1.0,
          "validation": { "min": 0.1, "max": 100 },
          "category": "rates"
        },
        {
          "key": "ServerSettings.XPMultiplier",
          "displayName": "XP Multiplier",
          "type": "Float",
          "defaultValue": 1.0,
          "validation": { "min": 0.1, "max": 100 },
          "category": "rates"
        },
        {
          "key": "ServerSettings.DifficultyOffset",
          "displayName": "Difficulty",
          "type": "Float",
          "defaultValue": 1.0,
          "validation": { "min": 0.1, "max": 1.0 },
          "category": "rules"
        },
        {
          "key": "ServerSettings.bAllowFlyerCarryPvE",
          "displayName": "Allow Flyer Carry (PvE)",
          "type": "Bool",
          "defaultValue": false,
          "category": "rules"
        },
        {
          "key": "ServerSettings.bDisableStructureDecayPvE",
          "displayName": "Disable Structure Decay",
          "type": "Bool",
          "defaultValue": false,
          "category": "rules"
        },
        {
          "key": "SessionSettings.SessionName",
          "displayName": "Server Name",
          "type": "String",
          "defaultValue": "ARK Server",
          "category": "server"
        },
        {
          "key": "MessageOfTheDay.Message",
          "displayName": "Message of the Day",
          "type": "Text",
          "defaultValue": "",
          "category": "server"
        }
      ]
    },
    "ShooterGame/Saved/Config/WindowsServer/Game.ini": {
      "displayName": "Game Configuration",
      "description": "Advanced game settings and overrides",
      "fileType": "Ini",
      "sections": [
        { "id": "engrams", "name": "Engram Overrides", "fields": [] },
        { "id": "levels", "name": "Level Overrides", "fields": [] }
      ],
      "fields": [
        {
          "key": "ShooterGameMode.bDisableLootCrates",
          "displayName": "Disable Loot Crates",
          "type": "Bool",
          "defaultValue": false,
          "advanced": true
        },
        {
          "key": "ShooterGameMode.MaxTribeLogs",
          "displayName": "Max Tribe Logs",
          "type": "Int",
          "defaultValue": 100,
          "validation": { "min": 10, "max": 1000 },
          "advanced": true
        }
      ]
    },
    "ShooterGame/Saved/Config/WindowsServer/Engine.ini": {
      "displayName": "Engine Settings",
      "description": "Performance and networking settings",
      "fileType": "Ini",
      "fields": [
        {
          "key": "OnlineSubsystemSteam.bVACEnabled",
          "displayName": "Enable VAC",
          "type": "Bool",
          "defaultValue": true
        },
        {
          "key": "OnlineSubsystemSteam.bUseSteamNetworking",
          "displayName": "Use Steam Networking",
          "type": "Bool",
          "defaultValue": true
        }
      ]
    }
  }
}
```

---

### Example: Palworld (JSON Configuration)

```json
{
  "name": "Palworld",
  "slug": "palworld",
  "steamAppId": "2394010",
  "executableType": "Exe",
  "defaultExecutablePath": "PalServer.exe",
  "defaultStartCommand": "-port={port} -players={maxPlayers} EpicApp=PalServer",
  "supportsDocker": true,
  "defaultDockerImage": "thijsvanloef/palworld-server-docker",
  "supportsMods": true,
  "modProviders": [
    {
      "provider": "NexusMods",
      "enabled": true,
      "priority": 1,
      "gameSlug": "palworld",
      "modInstallPath": "Pal/Content/Paks/~mods/",
      "providerSettings": {
        "gameId": "6063"
      }
    },
    {
      "provider": "Thunderstore",
      "enabled": true,
      "priority": 2,
      "gameSlug": "palworld",
      "modInstallPath": "Pal/Content/Paks/~mods/",
      "providerSettings": {
        "communityId": "palworld"
      }
    },
    {
      "provider": "Local",
      "enabled": true,
      "priority": 99,
      "modInstallPath": "Pal/Content/Paks/~mods/",
      "providerSettings": {}
    }
  ],
  "rconConfig": {
    "defaultEnabled": true,
    "protocol": "Source",
    "defaultPort": 25575
  },
  "configurationSchemas": {
    "Pal/Saved/Config/WindowsServer/PalWorldSettings.ini": {
      "displayName": "Server Settings",
      "fileType": "Ini",
      "fields": [
        {
          "key": "/Script/Pal.PalGameWorldSettings.OptionSettings",
          "displayName": "World Settings (JSON)",
          "description": "This field contains embedded JSON - edit individual settings below",
          "type": "Custom",
          "advanced": true
        }
      ]
    },
    "settings.json": {
      "displayName": "World Configuration",
      "description": "Game world and gameplay settings",
      "fileType": "Json",
      "sections": [
        { "id": "server", "name": "Server", "fields": ["ServerName", "AdminPassword", "ServerPassword", "PublicPort", "RCONEnabled", "RCONPort"] },
        { "id": "rates", "name": "Rates", "fields": ["ExpRate", "PalCaptureRate", "PalSpawnNumRate", "DamageToPlayerMultiplier"] },
        { "id": "gameplay", "name": "Gameplay", "fields": ["DeathPenalty", "bEnablePlayerToPlayerDamage", "bEnableFriendlyFire"] }
      ],
      "fields": [
        {
          "key": "ServerName",
          "jsonPath": "$.ServerName",
          "displayName": "Server Name",
          "type": "String",
          "defaultValue": "Palworld Server",
          "category": "server"
        },
        {
          "key": "AdminPassword",
          "jsonPath": "$.AdminPassword",
          "displayName": "Admin Password",
          "type": "Password",
          "required": true,
          "category": "server"
        },
        {
          "key": "ServerPassword",
          "jsonPath": "$.ServerPassword",
          "displayName": "Server Password",
          "type": "Password",
          "category": "server"
        },
        {
          "key": "PublicPort",
          "jsonPath": "$.PublicPort",
          "displayName": "Game Port",
          "type": "Port",
          "defaultValue": 8211,
          "category": "server"
        },
        {
          "key": "RCONEnabled",
          "jsonPath": "$.RCONEnabled",
          "displayName": "Enable RCON",
          "type": "Bool",
          "defaultValue": true,
          "category": "server"
        },
        {
          "key": "RCONPort",
          "jsonPath": "$.RCONPort",
          "displayName": "RCON Port",
          "type": "Port",
          "defaultValue": 25575,
          "showWhen": { "RCONEnabled": true },
          "category": "server"
        },
        {
          "key": "ExpRate",
          "jsonPath": "$.ExpRate",
          "displayName": "Experience Rate",
          "type": "Float",
          "defaultValue": 1.0,
          "validation": { "min": 0.1, "max": 20.0 },
          "category": "rates"
        },
        {
          "key": "PalCaptureRate",
          "jsonPath": "$.PalCaptureRate",
          "displayName": "Pal Capture Rate",
          "type": "Float",
          "defaultValue": 1.0,
          "validation": { "min": 0.1, "max": 10.0 },
          "category": "rates"
        },
        {
          "key": "DeathPenalty",
          "jsonPath": "$.DeathPenalty",
          "displayName": "Death Penalty",
          "type": "Select",
          "defaultValue": "All",
          "options": [
            { "value": "None", "label": "None" },
            { "value": "Item", "label": "Drop Items" },
            { "value": "ItemAndEquipment", "label": "Drop Items & Equipment" },
            { "value": "All", "label": "Drop Everything" }
          ],
          "category": "gameplay"
        },
        {
          "key": "bEnablePlayerToPlayerDamage",
          "jsonPath": "$.bEnablePlayerToPlayerDamage",
          "displayName": "PvP Damage",
          "type": "Bool",
          "defaultValue": false,
          "category": "gameplay"
        }
      ]
    }
  }
}
```

---

### Example: Rust (Steam with WebRCON)

```json
{
  "name": "Rust",
  "slug": "rust",
  "steamAppId": "258550",
  "executableType": "Exe",
  "defaultExecutablePath": "RustDedicated.exe",
  "defaultStartCommand": "-batchmode +server.port {port} +server.level \"{map}\" +server.seed {seed} +server.maxplayers {maxPlayers} +server.hostname \"{hostname}\" +rcon.port {rconPort} +rcon.password \"{rconPassword}\" +rcon.web 1",
  "defaultStopCommand": "quit",
  "supportsDocker": true,
  "defaultDockerImage": "didstopia/rust-server",
  "supportsMods": true,
  "modProviders": [
    {
      "provider": "Local",
      "enabled": true,
      "priority": 1,
      "modInstallPath": "server/rust-oxide/plugins/",
      "providerSettings": {
        "description": "Oxide/uMod plugins - download from umod.org"
      }
    }
  ],
  "rconConfig": {
    "defaultEnabled": true,
    "protocol": "WebRcon",
    "defaultPort": 28016,
    "useWebSocket": true
  },
  "configurationSchemas": {
    "startup": {
      "displayName": "Startup Configuration",
      "description": "Command line parameters for server startup",
      "fileType": "Custom",
      "sections": [
        { "id": "server", "name": "Server Identity", "fields": ["hostname", "description", "headerimage", "url"] },
        { "id": "network", "name": "Network", "fields": ["port", "rconPort", "rconPassword", "rconWeb"] },
        { "id": "world", "name": "World Settings", "fields": ["map", "seed", "worldsize", "saveinterval"] },
        { "id": "players", "name": "Players", "fields": ["maxPlayers", "tickrate"] }
      ],
      "fields": [
        {
          "key": "hostname",
          "displayName": "Server Name",
          "type": "String",
          "defaultValue": "Rust Server",
          "category": "server"
        },
        {
          "key": "description",
          "displayName": "Description",
          "type": "Text",
          "defaultValue": "A Rust Server",
          "category": "server"
        },
        {
          "key": "maxPlayers",
          "displayName": "Max Players",
          "type": "Slider",
          "defaultValue": 100,
          "validation": { "min": 1, "max": 500 },
          "category": "players"
        },
        {
          "key": "map",
          "displayName": "Map",
          "type": "Select",
          "defaultValue": "Procedural Map",
          "options": [
            { "value": "Procedural Map", "label": "Procedural Map" },
            { "value": "Barren", "label": "Barren" },
            { "value": "HapisIsland", "label": "Hapis Island" },
            { "value": "SavasIsland", "label": "Savas Island" },
            { "value": "CraggyIsland", "label": "Craggy Island" }
          ],
          "category": "world"
        },
        {
          "key": "seed",
          "displayName": "World Seed",
          "type": "Int",
          "defaultValue": 12345,
          "category": "world"
        },
        {
          "key": "worldsize",
          "displayName": "World Size",
          "type": "Slider",
          "defaultValue": 4000,
          "validation": { "min": 1000, "max": 6000 },
          "category": "world"
        },
        {
          "key": "saveinterval",
          "displayName": "Save Interval (seconds)",
          "type": "Int",
          "defaultValue": 300,
          "validation": { "min": 60, "max": 3600 },
          "category": "world"
        },
        {
          "key": "rconPort",
          "displayName": "RCON Port",
          "type": "Port",
          "defaultValue": 28016,
          "category": "network"
        },
        {
          "key": "rconPassword",
          "displayName": "RCON Password",
          "type": "Password",
          "required": true,
          "category": "network",
          "validation": { "minLength": 8 }
        },
        {
          "key": "rconWeb",
          "displayName": "Web RCON",
          "description": "Enable WebSocket RCON (required for most RCON tools)",
          "type": "Bool",
          "defaultValue": true,
          "category": "network"
        },
        {
          "key": "tickrate",
          "displayName": "Tick Rate",
          "type": "Select",
          "defaultValue": 30,
          "options": [
            { "value": 10, "label": "10 (Low)" },
            { "value": 30, "label": "30 (Default)" },
            { "value": 60, "label": "60 (High)" },
            { "value": 128, "label": "128 (Maximum)" }
          ],
          "category": "players",
          "advanced": true
        }
      ]
    },
    "server/rust-oxide/config/oxide.config.json": {
      "displayName": "Oxide Configuration",
      "description": "Oxide mod framework settings",
      "fileType": "Json",
      "createIfMissing": false,
      "fields": [
        {
          "key": "OxideConsole",
          "jsonPath": "$.Options.OxideConsole",
          "displayName": "Oxide Console",
          "type": "Bool",
          "defaultValue": true
        },
        {
          "key": "PluginWatchers",
          "jsonPath": "$.Options.PluginWatchers",
          "displayName": "Auto-reload Plugins",
          "type": "Bool",
          "defaultValue": true
        },
        {
          "key": "DefaultGroups",
          "jsonPath": "$.Options.DefaultGroups.Players",
          "displayName": "Default Player Group",
          "type": "String",
          "defaultValue": "default"
        }
      ]
    },
    "server/rust-oxide/data/cfg/serverauto.cfg": {
      "displayName": "Server Auto Config",
      "description": "Commands executed on server start",
      "fileType": "LineBased",
      "fields": []
    }
  }
}
```

---

### Example: Valheim (Multi-Config with Mods)

```json
{
  "name": "Valheim",
  "slug": "valheim",
  "steamAppId": "896660",
  "executableType": "Exe",
  "defaultExecutablePath": "valheim_server.exe",
  "defaultStartCommand": "-name \"{serverName}\" -port {port} -world \"{world}\" -password \"{password}\" -public {public} -savedir \"{saveDir}\"",
  "supportsDocker": true,
  "defaultDockerImage": "lloesche/valheim-server",
  "supportsMods": true,
  "modProviders": [
    {
      "provider": "Thunderstore",
      "enabled": true,
      "priority": 1,
      "gameSlug": "valheim",
      "modInstallPath": "BepInEx/plugins/",
      "providerSettings": {
        "communityId": "valheim"
      }
    },
    {
      "provider": "NexusMods",
      "enabled": true,
      "priority": 2,
      "gameSlug": "valheim",
      "modInstallPath": "BepInEx/plugins/",
      "providerSettings": {
        "gameId": "3667"
      }
    },
    {
      "provider": "Local",
      "enabled": true,
      "priority": 99,
      "modInstallPath": "BepInEx/plugins/",
      "providerSettings": {}
    }
  ],
  "rconConfig": {
    "defaultEnabled": false,
    "protocol": "Custom"
  },
  "configurationSchemas": {
    "startup": {
      "displayName": "Server Startup",
      "description": "Command line configuration",
      "fileType": "Custom",
      "sections": [
        { "id": "server", "name": "Server Settings", "fields": ["serverName", "password", "public"] },
        { "id": "world", "name": "World Settings", "fields": ["world", "saveDir", "backups", "backupShort", "backupLong"] },
        { "id": "network", "name": "Network", "fields": ["port"] },
        { "id": "crossplay", "name": "Crossplay", "fields": ["crossplay", "instanceId"] }
      ],
      "fields": [
        {
          "key": "serverName",
          "displayName": "Server Name",
          "type": "String",
          "defaultValue": "Valheim Server",
          "category": "server"
        },
        {
          "key": "password",
          "displayName": "Password",
          "description": "Minimum 5 characters, cannot contain server name",
          "type": "Password",
          "required": true,
          "category": "server",
          "validation": { "minLength": 5 }
        },
        {
          "key": "public",
          "displayName": "Public Server",
          "description": "List on community servers",
          "type": "Bool",
          "defaultValue": true,
          "category": "server"
        },
        {
          "key": "world",
          "displayName": "World Name",
          "type": "String",
          "defaultValue": "Dedicated",
          "category": "world"
        },
        {
          "key": "saveDir",
          "displayName": "Save Directory",
          "type": "Path",
          "defaultValue": "",
          "category": "world",
          "advanced": true
        },
        {
          "key": "backups",
          "displayName": "Backup Count",
          "type": "Int",
          "defaultValue": 4,
          "validation": { "min": 1, "max": 100 },
          "category": "world"
        },
        {
          "key": "port",
          "displayName": "Port",
          "description": "Server uses port, port+1, and port+2",
          "type": "Port",
          "defaultValue": 2456,
          "category": "network"
        },
        {
          "key": "crossplay",
          "displayName": "Enable Crossplay",
          "description": "Allow Xbox/PC crossplay",
          "type": "Bool",
          "defaultValue": false,
          "category": "crossplay"
        }
      ]
    },
    "BepInEx/config/BepInEx.cfg": {
      "displayName": "BepInEx Configuration",
      "description": "Mod loader settings (requires BepInEx)",
      "fileType": "Ini",
      "createIfMissing": false,
      "fields": [
        {
          "key": "Logging.Console.Enabled",
          "displayName": "Console Logging",
          "type": "Bool",
          "defaultValue": true
        },
        {
          "key": "Logging.Console.LogLevels",
          "displayName": "Log Levels",
          "type": "MultiSelect",
          "defaultValue": ["Fatal", "Error", "Warning", "Message", "Info"],
          "options": [
            { "value": "Fatal", "label": "Fatal" },
            { "value": "Error", "label": "Error" },
            { "value": "Warning", "label": "Warning" },
            { "value": "Message", "label": "Message" },
            { "value": "Info", "label": "Info" },
            { "value": "Debug", "label": "Debug" },
            { "value": "All", "label": "All" }
          ]
        }
      ]
    },
    "BepInEx/config/valheim_plus.cfg": {
      "displayName": "Valheim Plus",
      "description": "Valheim Plus mod configuration (if installed)",
      "fileType": "Ini",
      "createIfMissing": false,
      "sections": [
        { "id": "server", "name": "Server", "fields": ["Server.enabled", "Server.enforceMod", "Server.serverSyncHotkeys"] },
        { "id": "stamina", "name": "Stamina", "fields": ["Stamina.enabled", "Stamina.dodgeStaminaUsage", "Stamina.sneakStaminaDrain"] },
        { "id": "workbench", "name": "Workbench", "fields": ["Workbench.enabled", "Workbench.workbenchRange"] }
      ],
      "fields": [
        {
          "key": "Server.enabled",
          "displayName": "Enable Server Section",
          "type": "Bool",
          "defaultValue": true,
          "category": "server"
        },
        {
          "key": "Server.enforceMod",
          "displayName": "Enforce Mod",
          "description": "Require clients to have Valheim Plus",
          "type": "Bool",
          "defaultValue": true,
          "category": "server"
        },
        {
          "key": "Stamina.enabled",
          "displayName": "Enable Stamina Changes",
          "type": "Bool",
          "defaultValue": false,
          "category": "stamina"
        },
        {
          "key": "Stamina.dodgeStaminaUsage",
          "displayName": "Dodge Stamina Usage",
          "type": "Float",
          "defaultValue": 1.0,
          "validation": { "min": 0, "max": 2 },
          "category": "stamina",
          "showWhen": { "Stamina.enabled": true }
        },
        {
          "key": "Workbench.enabled",
          "displayName": "Enable Workbench Changes",
          "type": "Bool",
          "defaultValue": false,
          "category": "workbench"
        },
        {
          "key": "Workbench.workbenchRange",
          "displayName": "Workbench Range",
          "type": "Float",
          "defaultValue": 20,
          "validation": { "min": 5, "max": 100 },
          "category": "workbench",
          "showWhen": { "Workbench.enabled": true }
        }
      ]
    },
    "adminlist.txt": {
      "displayName": "Admin List",
      "description": "Steam IDs of server admins (one per line)",
      "fileType": "LineBased",
      "fields": [
        {
          "key": "admins",
          "displayName": "Admin Steam IDs",
          "type": "List",
          "defaultValue": []
        }
      ]
    },
    "bannedlist.txt": {
      "displayName": "Ban List",
      "description": "Steam IDs of banned players",
      "fileType": "LineBased",
      "fields": [
        {
          "key": "banned",
          "displayName": "Banned Steam IDs",
          "type": "List",
          "defaultValue": []
        }
      ]
    },
    "permittedlist.txt": {
      "displayName": "Whitelist",
      "description": "Steam IDs allowed when server is not public",
      "fileType": "LineBased",
      "fields": [
        {
          "key": "permitted",
          "displayName": "Whitelisted Steam IDs",
          "type": "List",
          "defaultValue": []
        }
      ]
    }
  }
}
```

---

## Conclusion

This specification provides a comprehensive foundation for building Nebula Panel. The architecture emphasizes:

1. **Extensibility**: New games, executors, and mod providers can be added easily
2. **Scalability**: Separation of concerns allows horizontal scaling
3. **Maintainability**: Clean architecture with clear boundaries
4. **User Experience**: Real-time updates, intuitive UI, comprehensive theming
5. **Security**: Role-based access control with per-resource permissions

Begin development with Phase 1, establishing the foundation before moving to more complex features. The modular design allows parallel development of different components once the core is in place.
