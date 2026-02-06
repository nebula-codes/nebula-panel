using System.ComponentModel.DataAnnotations;
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
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, RegularExpression(@"^[a-z0-9\-]+$", ErrorMessage = "Slug must contain only lowercase letters, numbers, and hyphens.")]
    [StringLength(100, MinimumLength = 1)] string Slug,
    string? SteamAppId,
    ExecutableType ExecutableType,
    [Required] string DefaultExecutablePath,
    [Required] string DefaultStartCommand,
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
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, RegularExpression(@"^[a-z0-9\-]+$", ErrorMessage = "Slug must contain only lowercase letters, numbers, and hyphens.")]
    [StringLength(100, MinimumLength = 1)] string Slug,
    string? SteamAppId,
    ExecutableType ExecutableType,
    [Required] string DefaultExecutablePath,
    [Required] string DefaultStartCommand,
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
