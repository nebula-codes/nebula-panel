namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Simple health status response for basic health checks.
/// </summary>
public record HealthStatusDto(
    string Status,
    DateTime Timestamp
);

/// <summary>
/// Detailed information about a single health check.
/// </summary>
public record HealthCheckEntryDto(
    string Name,
    string Status,
    string? Description,
    TimeSpan Duration,
    IReadOnlyDictionary<string, object>? Data
);

/// <summary>
/// Full detailed health report with all check results.
/// </summary>
public record DetailedHealthReportDto(
    string Status,
    TimeSpan TotalDuration,
    IReadOnlyList<HealthCheckEntryDto> Entries,
    DateTime Timestamp
);
