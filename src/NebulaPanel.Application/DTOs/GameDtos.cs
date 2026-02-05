using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.DTOs;

public record GameDto(
    Guid Id,
    string Name,
    string Slug,
    GameSourceType SourceType,
    string? SteamAppId,
    ExecutableType ExecutableType,
    string DefaultExecutablePath,
    string DefaultStartCommand,
    string? DefaultStopCommand,
    bool SupportsDocker,
    string? DefaultDockerImage,
    string? DockerDataPath,
    int? DefaultPort,
    string? IconPath,
    bool SupportsMods,
    List<ModProviderConfiguration> ModProviders,
    RconDefaults? RconDefaults,
    Dictionary<string, ConfigurationSchema> ConfigurationSchemas,
    int ServerCount
);

public record GameListItemDto(
    Guid Id,
    string Name,
    string Slug,
    GameSourceType SourceType,
    ExecutableType ExecutableType,
    bool SupportsDocker,
    bool SupportsMods,
    string? IconPath,
    int ServerCount
);

public record CreateGameRequest(
    string Name,
    string Slug,
    string? SteamAppId,
    ExecutableType ExecutableType,
    string DefaultExecutablePath,
    string DefaultStartCommand,
    string? DefaultStopCommand,
    bool SupportsDocker,
    string? DefaultDockerImage,
    string? DockerDataPath,
    string? IconPath,
    bool SupportsMods,
    List<ModProviderConfiguration>? ModProviders,
    RconDefaults? RconDefaults,
    Dictionary<string, ConfigurationSchema>? ConfigurationSchemas
);

public record UpdateGameRequest(
    string Name,
    string Slug,
    string? SteamAppId,
    ExecutableType ExecutableType,
    string DefaultExecutablePath,
    string DefaultStartCommand,
    string? DefaultStopCommand,
    bool SupportsDocker,
    string? DefaultDockerImage,
    string? DockerDataPath,
    string? IconPath,
    bool SupportsMods,
    List<ModProviderConfiguration>? ModProviders,
    RconDefaults? RconDefaults,
    Dictionary<string, ConfigurationSchema>? ConfigurationSchemas
);
