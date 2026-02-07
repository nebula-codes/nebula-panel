using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

public interface ISettingsService
{
    // General Settings
    Task<GeneralSettingsDto> GetGeneralSettingsAsync(CancellationToken cancellationToken = default);
    Task<Result<GeneralSettingsDto>> UpdateGeneralSettingsAsync(
        UpdateGeneralSettingsRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    // Update Settings
    Task<UpdateSettingsDto> GetUpdateSettingsAsync(CancellationToken cancellationToken = default);
    Task<Result<UpdateSettingsDto>> UpdateUpdateSettingsAsync(
        UpdateUpdateSettingsRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    // Appearance Settings
    Task<AppearanceSettingsDto> GetAppearanceSettingsAsync(CancellationToken cancellationToken = default);
    Task<Result<AppearanceSettingsDto>> UpdateAppearanceSettingsAsync(
        UpdateAppearanceSettingsRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    // Database Info
    Task<DatabaseInfoDto> GetDatabaseInfoAsync(CancellationToken cancellationToken = default);
    Task<Result<Stream>> CreateDatabaseBackupAsync(CancellationToken cancellationToken = default);
    string GetBackupFileName();

    // Integration Settings
    Task<IntegrationSettingsDto> GetIntegrationSettingsAsync(CancellationToken cancellationToken = default);
    Task<Result<IntegrationSettingsDto>> UpdateIntegrationSettingsAsync(
        UpdateIntegrationSettingsRequest request, Guid userId, CancellationToken cancellationToken = default);

    // Import/Export
    Task<Result<SettingsExportDto>> ExportSettingsAsync(CancellationToken cancellationToken = default);
    Task<Result<SettingsImportPreviewDto>> PreviewImportAsync(string jsonContent, CancellationToken cancellationToken = default);
    Task<Result> ImportSettingsAsync(SettingsImportRequest request, Guid userId, CancellationToken cancellationToken = default);
}
