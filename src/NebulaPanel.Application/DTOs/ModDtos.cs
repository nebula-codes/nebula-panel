using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// DTO for displaying installed server mod information.
/// </summary>
public record ServerModDto(
    Guid Id,
    Guid ServerId,
    string ModId,
    string Name,
    string? Version,
    string? VersionId,
    ModProviderType Provider,
    DateTime InstalledAt,
    DateTime? UpdatedAt,
    bool IsEnabled,
    string? LocalPath,
    string? IconUrl,
    long? FileSizeBytes
);

/// <summary>
/// Request to install a mod on a server.
/// </summary>
public record InstallModRequest(
    ModProviderType Provider,
    string ModId,
    string? VersionId
);

/// <summary>
/// Result of a mod installation attempt.
/// </summary>
public record InstallResult(
    bool Success,
    ServerModDto? InstalledMod,
    string? Error,
    int DependenciesInstalled
);

/// <summary>
/// Information about a mod provider configured for a game.
/// </summary>
public record ModProviderInfo(
    ModProviderType Type,
    string Name,
    string? IconUrl,
    bool Enabled,
    int Priority
);

/// <summary>
/// Aggregated search results from multiple providers.
/// </summary>
public record UnifiedModSearchResult(
    IReadOnlyList<Domain.Interfaces.ModSearchItem> Mods,
    Dictionary<ModProviderType, int> ResultCountByProvider,
    int TotalCount,
    bool HasMoreResults
);
