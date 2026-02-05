namespace NebulaPanel.Application.DTOs;

/// <summary>
/// DTO for Hytale user preferences.
/// </summary>
public record HytaleUserPreferencesDto(
    bool AutoRefreshToken,
    bool NotifyAt24Hours,
    bool NotifyAt1Hour,
    bool EmailOnExpiry);

/// <summary>
/// Request to update Hytale user preferences.
/// </summary>
public record UpdateHytalePreferencesRequest(
    bool AutoRefreshToken,
    bool NotifyAt24Hours,
    bool NotifyAt1Hour,
    bool EmailOnExpiry);
