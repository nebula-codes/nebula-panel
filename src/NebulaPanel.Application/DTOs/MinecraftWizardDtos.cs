using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Server setup type for Minecraft server creation.
/// </summary>
public enum MinecraftSetupType
{
    /// <summary>
    /// Start fresh with a loader (Paper, Fabric, Forge, etc.).
    /// </summary>
    Loader,

    /// <summary>
    /// Install a pre-made modpack.
    /// </summary>
    Modpack
}

/// <summary>
/// Complete form data for Minecraft server creation wizard.
/// </summary>
public class MinecraftServerWizardData
{
    // Step 0: Setup Type Selection
    public MinecraftSetupType SetupType { get; set; } = MinecraftSetupType.Loader;

    // Modpack Selection (when SetupType == Modpack)
    public ModpackInstallRequest? SelectedModpack { get; set; }

    // Step 1: Loader Selection
    public MinecraftLoader SelectedLoader { get; set; } = MinecraftLoader.Paper;

    // Step 2: Version Selection
    public string? MinecraftVersion { get; set; }
    public string? LoaderVersion { get; set; }
    public string? FullVersionString { get; set; }
    public bool IncludeSnapshots { get; set; }

    // Step 3: Server Configuration
    public string ServerName { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public int Port { get; set; } = 25565;
    public int MinMemoryMb { get; set; } = 1024;
    public int MaxMemoryMb { get; set; } = 4096;
    public bool EnableRcon { get; set; } = true;
    public int RconPort { get; set; } = 25575;
    public string? RconPassword { get; set; }
    public ServerDeploymentType DeploymentType { get; set; } = ServerDeploymentType.Native;

    // Docker-specific (if DeploymentType == Docker)
    public string? DockerImage { get; set; }
    public string? DockerTag { get; set; }

    // Step 4: World Settings (Optional)
    public string WorldName { get; set; } = "world";
    public string? WorldSeed { get; set; }
    public MinecraftGameMode GameMode { get; set; } = MinecraftGameMode.Survival;
    public MinecraftDifficulty Difficulty { get; set; } = MinecraftDifficulty.Normal;
    public bool EnablePvp { get; set; } = true;
    public int MaxPlayers { get; set; } = 20;
    public bool SpawnAnimals { get; set; } = true;
    public bool SpawnMonsters { get; set; } = true;
    public bool EnableCommandBlocks { get; set; }
}

/// <summary>
/// Minecraft game modes for server.properties.
/// </summary>
public enum MinecraftGameMode
{
    Survival = 0,
    Creative = 1,
    Adventure = 2,
    Spectator = 3
}

/// <summary>
/// Minecraft difficulty levels for server.properties.
/// </summary>
public enum MinecraftDifficulty
{
    Peaceful = 0,
    Easy = 1,
    Normal = 2,
    Hard = 3
}

/// <summary>
/// Loader information for UI display in wizard step 1.
/// </summary>
public record MinecraftLoaderInfo(
    MinecraftLoader Loader,
    string Name,
    string Description,
    string Icon,
    string[] Features,
    bool IsProxy = false,
    bool IsRecommended = false
);

/// <summary>
/// Installation progress DTO for SignalR broadcast.
/// </summary>
public record MinecraftInstallProgressDto
{
    public required Guid InstallationId { get; init; }
    public required string Phase { get; init; }
    public required string Message { get; init; }
    public double Percentage { get; init; }
    public long? BytesDownloaded { get; init; }
    public long? TotalBytes { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public bool IsComplete { get; init; }
    public bool HasError { get; init; }
    public string? ErrorMessage { get; init; }

    public static MinecraftInstallProgressDto Starting(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Preparing",
        Message = "Preparing installation...",
        Percentage = 0
    };

    public static MinecraftInstallProgressDto Progress(
        Guid installationId,
        string phase,
        string message,
        double percentage,
        long? bytesDownloaded = null,
        long? totalBytes = null) => new()
    {
        InstallationId = installationId,
        Phase = phase,
        Message = message,
        Percentage = percentage,
        BytesDownloaded = bytesDownloaded,
        TotalBytes = totalBytes
    };

    public static MinecraftInstallProgressDto Completed(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Complete",
        Message = "Installation completed successfully!",
        Percentage = 100,
        IsComplete = true
    };

    public static MinecraftInstallProgressDto Failed(Guid installationId, string error) => new()
    {
        InstallationId = installationId,
        Phase = "Failed",
        Message = error,
        Percentage = 0,
        IsComplete = true,
        HasError = true,
        ErrorMessage = error
    };
}

/// <summary>
/// Result of Minecraft server installation.
/// </summary>
public record MinecraftInstallResult
{
    public required bool Success { get; init; }
    public Guid? ServerId { get; init; }
    public string? Error { get; init; }
    public string? InstalledVersion { get; init; }
    public string? InstalledPath { get; init; }
}

/// <summary>
/// Settings to write to server.properties during Minecraft server creation.
/// </summary>
public record MinecraftServerPropertiesSettings
{
    /// <summary>
    /// Server port. For Docker containers, this should be 25565 (internal port);
    /// port mapping handles exposing on the desired host port.
    /// </summary>
    public int Port { get; init; } = 25565;
    public string WorldName { get; init; } = "world";
    public string? WorldSeed { get; init; }
    public MinecraftGameMode GameMode { get; init; } = MinecraftGameMode.Survival;
    public MinecraftDifficulty Difficulty { get; init; } = MinecraftDifficulty.Normal;
    public bool EnablePvp { get; init; } = true;
    public int MaxPlayers { get; init; } = 20;
    public bool SpawnAnimals { get; init; } = true;
    public bool SpawnMonsters { get; init; } = true;
    public bool EnableCommandBlocks { get; init; }
    public bool EnableRcon { get; init; } = true;
    /// <summary>
    /// RCON port. For Docker containers, this should be 25575 (internal port);
    /// port mapping handles exposing on the desired host port.
    /// </summary>
    public int RconPort { get; init; } = 25575;
    public string? RconPassword { get; init; }

    public static MinecraftServerPropertiesSettings FromWizard(MinecraftServerWizardData data)
    {
        // For Docker deployments, use default internal ports (25565 for game, 25575 for RCON)
        // Port mapping handles exposing on the desired host ports
        var isDocker = data.DeploymentType == NebulaPanel.Domain.Enums.ServerDeploymentType.Docker;

        return new()
        {
            Port = isDocker ? 25565 : data.Port,
            WorldName = data.WorldName,
            WorldSeed = data.WorldSeed,
            GameMode = data.GameMode,
            Difficulty = data.Difficulty,
            EnablePvp = data.EnablePvp,
            MaxPlayers = data.MaxPlayers,
            SpawnAnimals = data.SpawnAnimals,
            SpawnMonsters = data.SpawnMonsters,
            EnableCommandBlocks = data.EnableCommandBlocks,
            EnableRcon = data.EnableRcon,
            RconPort = isDocker ? 25575 : data.RconPort,
            RconPassword = data.RconPassword
        };
    }
}
