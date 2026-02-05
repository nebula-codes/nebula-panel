namespace NebulaPanel.Application.DTOs;

// General Settings
public record GeneralSettingsDto(
    string ApplicationName,
    string? ApplicationUrl,
    string? SupportEmail,
    bool MaintenanceMode,
    string? MaintenanceMessage);

public record UpdateGeneralSettingsRequest(
    string ApplicationName,
    string? ApplicationUrl,
    string? SupportEmail,
    bool MaintenanceMode,
    string? MaintenanceMessage);

// Update Settings
public record UpdateSettingsDto(
    bool Enabled,
    bool AutoCheck,
    int CheckIntervalHours,
    string Channel,
    DateTime? LastCheckAt,
    string? LatestAvailableVersion,
    string? CurrentVersion);

public record UpdateUpdateSettingsRequest(
    bool Enabled,
    bool AutoCheck,
    int CheckIntervalHours,
    string Channel);

// Appearance Settings
public record AppearanceSettingsDto(
    string DefaultTheme,
    bool AllowUserThemeOverride,
    string AccentColor,
    IReadOnlyList<ThemeOptionDto> AvailableThemes,
    IReadOnlyList<AccentColorOptionDto> AvailableAccentColors);

public record ThemeOptionDto(string Id, string Name, string Description);

public record AccentColorOptionDto(string Id, string Name, string CssClass);

public record UpdateAppearanceSettingsRequest(
    string DefaultTheme,
    bool AllowUserThemeOverride,
    string AccentColor);

// Database Info
public record DatabaseInfoDto(
    string Provider,
    string? ConnectionString,
    string? FilePath,
    long SizeBytes,
    string SizeFormatted,
    int TableCount,
    DateTime? LastBackupAt);

// Import/Export
public record SettingsExportDto(
    string Version,
    DateTime ExportedAt,
    string PanelVersion,
    GeneralSettingsDto General,
    UpdateSettingsDto Update,
    AppearanceSettingsDto Appearance);

public record SettingsImportRequest(
    bool ImportGeneral,
    bool ImportUpdate,
    bool ImportAppearance,
    string JsonContent);

public record SettingsImportPreviewDto(
    GeneralSettingsDto? General,
    UpdateSettingsDto? Update,
    AppearanceSettingsDto? Appearance,
    IReadOnlyList<string> Warnings);
