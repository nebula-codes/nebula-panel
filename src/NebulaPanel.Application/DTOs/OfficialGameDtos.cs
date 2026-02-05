using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Version information for display in the UI.
/// </summary>
public record GameVersionDto(
    string Version,
    string DisplayName,
    DateTime? ReleaseDate,
    GameVersionType VersionType,
    MinecraftLoader? LoaderType,
    string? MinecraftVersion,
    bool IsRecommended
);

/// <summary>
/// Summary of an official game for listing.
/// </summary>
public record OfficialGameListItemDto(
    string Name,
    string Slug,
    string? IconPath,
    bool SupportsMods,
    bool SupportsDocker
);

/// <summary>
/// Request to install an official game server.
/// </summary>
public record InstallOfficialServerRequest(
    string GameSlug,
    string Version,
    string ServerName,
    string InstallPath,
    ServerDeploymentType DeploymentType,
    Guid OwnerId
);

/// <summary>
/// Request to update an official game server.
/// </summary>
public record UpdateOfficialServerRequest(
    Guid ServerId,
    string TargetVersion
);

// =====================================================
// Admin UI DTOs for Official Games Management
// =====================================================

/// <summary>
/// Summary status of an official game for admin list views.
/// </summary>
public record OfficialGameStatusDto(
    string Slug,
    string Name,
    string? IconPath,
    string ProviderType,
    bool IsEnabled,
    int ServerCount,
    int? VersionCount,
    DateTime? LastVersionCheck,
    string? SchemaVersion);

/// <summary>
/// Detailed information about an official game for admin view.
/// </summary>
public record OfficialGameDetailDto(
    string Slug,
    string Name,
    string? IconPath,
    string ProviderType,
    bool IsEnabled,
    int ServerCount,
    int VersionCount,
    DateTime? LastVersionCheck,
    bool SupportsMods,
    bool SupportsDocker,
    string? SteamAppId,
    string ExecutableType,
    string DefaultExecutablePath,
    string DefaultStartCommand,
    string? DefaultStopCommand,
    string? DefaultDockerImage,
    RconDefaultsDto? RconDefaults,
    List<ModProviderConfigurationDto> ModProviders,
    List<ConfigSchemaInfoDto> ConfigSchemas,
    List<GameVersionDto> RecentVersions);

/// <summary>
/// RCON defaults information.
/// </summary>
public record RconDefaultsDto(
    bool DefaultEnabled,
    string Protocol,
    int DefaultPort);

/// <summary>
/// Configuration schema summary information.
/// </summary>
public record ConfigSchemaInfoDto(
    string FileName,
    string FileType,
    int FieldCount,
    List<string> Categories);

/// <summary>
/// Mod provider configuration info for display.
/// </summary>
public record ModProviderConfigurationDto(
    string Provider,
    bool Enabled,
    int Priority,
    string ModInstallPath);

/// <summary>
/// Request to enable/disable an official game.
/// </summary>
public record SetOfficialGameEnabledRequest(string Slug, bool Enabled);

/// <summary>
/// Result of refreshing versions for an official game.
/// </summary>
public record RefreshVersionsResultDto(int VersionCount, DateTime RefreshTime);
