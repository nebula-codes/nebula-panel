using System.ComponentModel.DataAnnotations;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.DTOs;

public record GameServerDto(
    Guid Id,
    string Name,
    string? Description,
    string? IconPath,       // Server-specific icon (if set)
    string? EffectiveIcon,  // Server icon if set, otherwise game icon
    Guid GameId,
    string GameName,
    string? GameIconPath,   // Game's default icon
    ServerDeploymentType DeploymentType,
    string InstallPath,
    string? DockerContainerId,
    DockerConfiguration? DockerConfig,
    NativeConfiguration? NativeConfig,
    RconConfiguration? RconConfig,
    int PrimaryPort,
    Dictionary<string, int> AdditionalPorts,
    string BindAddress,
    ServerStatus Status,
    DateTime? LastStarted,
    DateTime? LastStopped,
    int? ProcessId,
    ResourceLimits? ResourceLimits,
    Guid OwnerId,
    string OwnerUsername,
    InstalledModpackInfo? InstalledModpack,
    string? InstalledVersion,
    HytaleServerInfo? HytaleInfo,
    bool IsPinned,
    List<string> Tags
);

public record GameServerListItemDto(
    Guid Id,
    string Name,
    string? Description,
    string? EffectiveIcon,  // Server icon if set, otherwise game icon
    Guid GameId,
    string GameName,
    ServerDeploymentType DeploymentType,
    int PrimaryPort,
    string BindAddress,
    ServerStatus Status,
    DateTime? LastStarted,
    bool HytaleUpdateAvailable,  // Whether a Hytale update is available
    bool IsPinned,
    List<string> Tags
);

public record CreateGameServerRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [StringLength(500)] string? Description,
    string? IconUrl,
    [Required] Guid GameId,
    ServerDeploymentType DeploymentType,
    [Required, StringLength(500)] string InstallPath,
    [Range(1024, 65535)] int PrimaryPort,
    Dictionary<string, int>? AdditionalPorts,
    string? BindAddress,
    DockerConfigurationRequest? DockerConfig,
    NativeConfigurationRequest? NativeConfig,
    RconConfigurationRequest? RconConfig,
    ResourceLimitsRequest? ResourceLimits
);

public record UpdateGameServerRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [StringLength(500)] string? Description,
    string? IconUrl,
    [Required, StringLength(500)] string InstallPath,
    [Range(1024, 65535)] int PrimaryPort,
    Dictionary<string, int>? AdditionalPorts,
    string? BindAddress,
    DockerConfigurationRequest? DockerConfig,
    NativeConfigurationRequest? NativeConfig,
    RconConfigurationRequest? RconConfig,
    ResourceLimitsRequest? ResourceLimits
);

public record DockerConfigurationRequest(
    string Image,
    string? Tag,
    Dictionary<string, string>? EnvironmentVariables,
    List<VolumeMountRequest>? Volumes,
    List<PortMappingRequest>? Ports,
    string? Network,
    ResourceLimitsRequest? Limits,
    RestartPolicy RestartPolicy,
    string? Command = null,
    string? WorkingDirectory = null,
    bool RunAsRoot = false,
    DockerCommandMode? CommandMode = null
);

public record NativeConfigurationRequest(
    string WorkingDirectory,
    string ExecutablePath,
    string? Arguments,
    Dictionary<string, string>? EnvironmentVariables,
    string? JavaPath,
    string? JavaArguments,
    bool RunAsService,
    string? RunAsUser,
    bool UseStartupScript = false,
    string? StartupScriptPath = null
);

public record RconConfigurationRequest(
    bool Enabled,
    RconProtocolType Protocol,
    string? Host,
    int Port,
    string? Password,
    bool UseWebSocket,
    string? WebRconPath,
    int? TimeoutSeconds
);

public record ResourceLimitsRequest(
    int? MaxMemoryMb,
    int? MaxCpuPercent,
    int? MaxDiskMb,
    int? MaxNetworkMbps
);

public record VolumeMountRequest(
    string HostPath,
    string ContainerPath,
    bool ReadOnly
);

public record PortMappingRequest(
    int HostPort,
    int ContainerPort,
    string? Protocol
);
