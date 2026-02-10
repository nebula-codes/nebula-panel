using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

public class SettingsService(
    ISystemSettingsRepository settingsRepository,
    IDatabaseInfoService databaseInfoService,
    IEncryptionService encryptionService,
    IIntegrationSettingsProvider integrationSettingsProvider,
    ILogger<SettingsService> logger) : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly IReadOnlyList<ThemeOptionDto> AvailableThemes =
    [
        new("dark", "Dark", "A dark theme that's easy on the eyes"),
        new("light", "Light", "A clean, bright theme for well-lit environments")
    ];

    private static readonly IReadOnlyList<AccentColorOptionDto> AvailableAccentColors =
    [
        new("purple", "Purple", "bg-purple-500"),
        new("blue", "Blue", "bg-blue-500"),
        new("green", "Green", "bg-green-500"),
        new("orange", "Orange", "bg-orange-500"),
        new("pink", "Pink", "bg-pink-500"),
        new("cyan", "Cyan", "bg-cyan-500")
    ];

    #region General Settings

    public async Task<GeneralSettingsDto> GetGeneralSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        var general = DeserializeOrDefault<GeneralSettings>(settings.GeneralSettingsJson);

        return new GeneralSettingsDto(
            general.ApplicationName,
            general.ApplicationUrl,
            general.SupportEmail,
            general.MaintenanceMode,
            general.MaintenanceMessage);
    }

    public async Task<Result<GeneralSettingsDto>> UpdateGeneralSettingsAsync(
        UpdateGeneralSettingsRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApplicationName))
        {
            return Result.Failure<GeneralSettingsDto>(Error.Validation("Application name is required."));
        }

        var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);

        var general = new GeneralSettings
        {
            ApplicationName = request.ApplicationName.Trim(),
            ApplicationUrl = request.ApplicationUrl?.Trim(),
            SupportEmail = request.SupportEmail?.Trim(),
            MaintenanceMode = request.MaintenanceMode,
            MaintenanceMessage = request.MaintenanceMessage?.Trim()
        };

        settings.GeneralSettingsJson = JsonSerializer.Serialize(general, JsonOptions);
        settings.LastModifiedByUserId = userId;

        await settingsRepository.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("General settings updated by user {UserId}", userId);

        return new GeneralSettingsDto(
            general.ApplicationName,
            general.ApplicationUrl,
            general.SupportEmail,
            general.MaintenanceMode,
            general.MaintenanceMessage);
    }

    #endregion

    #region Update Settings

    public async Task<UpdateSettingsDto> GetUpdateSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        var update = DeserializeOrDefault<UpdateSettings>(settings.UpdateSettingsJson);

        return new UpdateSettingsDto(
            update.Enabled,
            update.AutoCheck,
            update.CheckIntervalHours,
            update.Channel.ToString(),
            update.LastCheckAt,
            update.LatestAvailableVersion,
            GetCurrentVersion());
    }

    public async Task<Result<UpdateSettingsDto>> UpdateUpdateSettingsAsync(
        UpdateUpdateSettingsRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (request.CheckIntervalHours < 1 || request.CheckIntervalHours > 168)
        {
            return Result.Failure<UpdateSettingsDto>(Error.Validation("Check interval must be between 1 and 168 hours."));
        }

        if (!Enum.TryParse<UpdateChannel>(request.Channel, true, out var channel))
        {
            return Result.Failure<UpdateSettingsDto>(Error.InvalidInput($"Invalid update channel: {request.Channel}"));
        }

        var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        var existing = DeserializeOrDefault<UpdateSettings>(settings.UpdateSettingsJson);

        var update = new UpdateSettings
        {
            Enabled = request.Enabled,
            AutoCheck = request.AutoCheck,
            CheckIntervalHours = request.CheckIntervalHours,
            Channel = channel,
            LastCheckAt = existing.LastCheckAt,
            LatestAvailableVersion = existing.LatestAvailableVersion
        };

        settings.UpdateSettingsJson = JsonSerializer.Serialize(update, JsonOptions);
        settings.LastModifiedByUserId = userId;

        await settingsRepository.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Update settings updated by user {UserId}", userId);

        return new UpdateSettingsDto(
            update.Enabled,
            update.AutoCheck,
            update.CheckIntervalHours,
            update.Channel.ToString(),
            update.LastCheckAt,
            update.LatestAvailableVersion,
            GetCurrentVersion());
    }

    #endregion

    #region Appearance Settings

    public async Task<AppearanceSettingsDto> GetAppearanceSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        var appearance = DeserializeOrDefault<AppearanceSettings>(settings.AppearanceSettingsJson);

        return new AppearanceSettingsDto(
            appearance.DefaultTheme,
            appearance.AllowUserThemeOverride,
            appearance.AccentColor,
            AvailableThemes,
            AvailableAccentColors);
    }

    public async Task<Result<AppearanceSettingsDto>> UpdateAppearanceSettingsAsync(
        UpdateAppearanceSettingsRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!AvailableThemes.Any(t => t.Id == request.DefaultTheme))
        {
            return Result.Failure<AppearanceSettingsDto>(Error.InvalidInput($"Invalid theme: {request.DefaultTheme}"));
        }

        if (!AvailableAccentColors.Any(c => c.Id == request.AccentColor))
        {
            return Result.Failure<AppearanceSettingsDto>(Error.InvalidInput($"Invalid accent color: {request.AccentColor}"));
        }

        var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);

        var appearance = new AppearanceSettings
        {
            DefaultTheme = request.DefaultTheme,
            AllowUserThemeOverride = request.AllowUserThemeOverride,
            AccentColor = request.AccentColor
        };

        settings.AppearanceSettingsJson = JsonSerializer.Serialize(appearance, JsonOptions);
        settings.LastModifiedByUserId = userId;

        await settingsRepository.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Appearance settings updated by user {UserId}", userId);

        return new AppearanceSettingsDto(
            appearance.DefaultTheme,
            appearance.AllowUserThemeOverride,
            appearance.AccentColor,
            AvailableThemes,
            AvailableAccentColors);
    }

    #endregion

    #region Integration Settings

    public async Task<IntegrationSettingsDto> GetIntegrationSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        var integration = DeserializeOrDefault<IntegrationSettingsData>(settings.IntegrationSettingsJson);

        return new IntegrationSettingsDto(
            CurseForgeConfigured: !string.IsNullOrWhiteSpace(integration.CurseForgeApiKey),
            SteamConfigured: !string.IsNullOrWhiteSpace(integration.SteamApiKey),
            ModtaleConfigured: !string.IsNullOrWhiteSpace(integration.ModtaleApiKey),
            CurseForgeApiKeyMasked: MaskApiKey(TryDecrypt(integration.CurseForgeApiKey)),
            SteamApiKeyMasked: MaskApiKey(TryDecrypt(integration.SteamApiKey)),
            ModtaleApiKeyMasked: MaskApiKey(TryDecrypt(integration.ModtaleApiKey)));
    }

    public async Task<Result<IntegrationSettingsDto>> UpdateIntegrationSettingsAsync(
        UpdateIntegrationSettingsRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        var existing = DeserializeOrDefault<IntegrationSettingsData>(settings.IntegrationSettingsJson);

        var updated = new IntegrationSettingsData
        {
            CurseForgeApiKey = request.ClearCurseForge
                ? null
                : request.CurseForgeApiKey != null
                    ? encryptionService.Encrypt(request.CurseForgeApiKey)
                    : existing.CurseForgeApiKey,
            SteamApiKey = request.ClearSteam
                ? null
                : request.SteamApiKey != null
                    ? encryptionService.Encrypt(request.SteamApiKey)
                    : existing.SteamApiKey,
            ModtaleApiKey = request.ClearModtale
                ? null
                : request.ModtaleApiKey != null
                    ? encryptionService.Encrypt(request.ModtaleApiKey)
                    : existing.ModtaleApiKey
        };

        settings.IntegrationSettingsJson = JsonSerializer.Serialize(updated, JsonOptions);
        settings.LastModifiedByUserId = userId;

        await settingsRepository.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);

        integrationSettingsProvider.InvalidateCache();

        logger.LogInformation("Integration settings updated by user {UserId}", userId);

        return new IntegrationSettingsDto(
            CurseForgeConfigured: !string.IsNullOrWhiteSpace(updated.CurseForgeApiKey),
            SteamConfigured: !string.IsNullOrWhiteSpace(updated.SteamApiKey),
            ModtaleConfigured: !string.IsNullOrWhiteSpace(updated.ModtaleApiKey),
            CurseForgeApiKeyMasked: MaskApiKey(TryDecrypt(updated.CurseForgeApiKey)),
            SteamApiKeyMasked: MaskApiKey(TryDecrypt(updated.SteamApiKey)),
            ModtaleApiKeyMasked: MaskApiKey(TryDecrypt(updated.ModtaleApiKey)));
    }

    private string? TryDecrypt(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
            return null;

        try
        {
            return encryptionService.Decrypt(encrypted);
        }
        catch
        {
            return null;
        }
    }

    private static string? MaskApiKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (key.Length <= 8)
            return new string('*', key.Length);

        return $"{key[..4]}{"".PadRight(key.Length - 8, '*')}{key[^4..]}";
    }

    private class IntegrationSettingsData
    {
        public string? CurseForgeApiKey { get; set; }
        public string? SteamApiKey { get; set; }
        public string? ModtaleApiKey { get; set; }
    }

    #endregion

    #region Database Info

    public async Task<DatabaseInfoDto> GetDatabaseInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = await databaseInfoService.GetDatabaseInfoAsync(cancellationToken).ConfigureAwait(false);

        return new DatabaseInfoDto(
            info.Provider,
            info.ConnectionString,
            info.FilePath,
            info.SizeBytes,
            FormatFileSize(info.SizeBytes),
            info.TableCount,
            info.LastBackupAt);
    }

    public async Task<Result<Stream>> CreateDatabaseBackupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stream = await databaseInfoService.CreateBackupAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Database backup created successfully");
            return Result.Success(stream);
        }
        catch (NotSupportedException ex)
        {
            logger.LogWarning(ex, "Database backup not supported");
            return Result.Failure<Stream>(Error.InvalidOperation(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create database backup");
            return Result.Failure<Stream>(Error.ExternalService("Database", "Failed to create database backup."));
        }
    }

    public string GetBackupFileName() => databaseInfoService.GetBackupFileName();

    #endregion

    #region Import/Export

    public async Task<Result<SettingsExportDto>> ExportSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var general = await GetGeneralSettingsAsync(cancellationToken).ConfigureAwait(false);
            var update = await GetUpdateSettingsAsync(cancellationToken).ConfigureAwait(false);
            var appearance = await GetAppearanceSettingsAsync(cancellationToken).ConfigureAwait(false);

            var export = new SettingsExportDto(
                Version: "1.0",
                ExportedAt: DateTime.UtcNow,
                PanelVersion: GetCurrentVersion() ?? "unknown",
                General: general,
                Update: update,
                Appearance: appearance);

            return Result.Success(export);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export settings");
            return Result.Failure<SettingsExportDto>(Error.ExternalService("Settings", "Failed to export settings."));
        }
    }

    public async Task<Result<SettingsImportPreviewDto>> PreviewImportAsync(
        string jsonContent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var export = JsonSerializer.Deserialize<SettingsExportDto>(jsonContent, JsonOptions);
            if (export is null)
            {
                return Result.Failure<SettingsImportPreviewDto>(Error.Validation("Invalid settings file format."));
            }

            var warnings = new List<string>();

            if (export.Version != "1.0")
            {
                warnings.Add($"Settings file version ({export.Version}) may not be fully compatible.");
            }

            return Result.Success(new SettingsImportPreviewDto(
                export.General,
                export.Update,
                export.Appearance,
                warnings));
        }
        catch (JsonException)
        {
            return Result.Failure<SettingsImportPreviewDto>(Error.Validation("Invalid JSON format in settings file."));
        }
    }

    public async Task<Result> ImportSettingsAsync(
        SettingsImportRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var export = JsonSerializer.Deserialize<SettingsExportDto>(request.JsonContent, JsonOptions);
            if (export is null)
            {
                return Result.Failure(Error.Validation("Invalid settings file format."));
            }

            var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);

            if (request.ImportGeneral && export.General is not null)
            {
                var general = new GeneralSettings
                {
                    ApplicationName = export.General.ApplicationName,
                    ApplicationUrl = export.General.ApplicationUrl,
                    SupportEmail = export.General.SupportEmail,
                    MaintenanceMode = export.General.MaintenanceMode,
                    MaintenanceMessage = export.General.MaintenanceMessage
                };
                settings.GeneralSettingsJson = JsonSerializer.Serialize(general, JsonOptions);
            }

            if (request.ImportUpdate && export.Update is not null)
            {
                if (!Enum.TryParse<UpdateChannel>(export.Update.Channel, true, out var channel))
                {
                    channel = UpdateChannel.Stable;
                }

                var update = new UpdateSettings
                {
                    Enabled = export.Update.Enabled,
                    AutoCheck = export.Update.AutoCheck,
                    CheckIntervalHours = export.Update.CheckIntervalHours,
                    Channel = channel
                };
                settings.UpdateSettingsJson = JsonSerializer.Serialize(update, JsonOptions);
            }

            if (request.ImportAppearance && export.Appearance is not null)
            {
                var appearance = new AppearanceSettings
                {
                    DefaultTheme = AvailableThemes.Any(t => t.Id == export.Appearance.DefaultTheme)
                        ? export.Appearance.DefaultTheme
                        : "dark",
                    AllowUserThemeOverride = export.Appearance.AllowUserThemeOverride,
                    AccentColor = AvailableAccentColors.Any(c => c.Id == export.Appearance.AccentColor)
                        ? export.Appearance.AccentColor
                        : "purple"
                };
                settings.AppearanceSettingsJson = JsonSerializer.Serialize(appearance, JsonOptions);
            }

            settings.LastModifiedByUserId = userId;
            await settingsRepository.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Settings imported by user {UserId}", userId);

            return Result.Success();
        }
        catch (JsonException)
        {
            return Result.Failure(Error.Validation("Invalid JSON format in settings file."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to import settings");
            return Result.Failure(Error.ExternalService("Settings", "Failed to import settings."));
        }
    }

    #endregion

    #region Helpers

    private static T DeserializeOrDefault<T>(string json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    private static string? GetCurrentVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    #endregion
}
