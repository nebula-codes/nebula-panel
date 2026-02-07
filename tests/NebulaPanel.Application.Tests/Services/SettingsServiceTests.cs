using System.Text.Json;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Application.Tests.Services;

public class SettingsServiceTests
{
    private readonly Mock<ISystemSettingsRepository> _mockSettingsRepo;
    private readonly Mock<IDatabaseInfoService> _mockDbInfoService;
    private readonly SettingsService _sut;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public SettingsServiceTests()
    {
        _mockSettingsRepo = new Mock<ISystemSettingsRepository>();
        _mockDbInfoService = new Mock<IDatabaseInfoService>();
        _sut = new SettingsService(
            _mockSettingsRepo.Object,
            _mockDbInfoService.Object,
            Mock.Of<IEncryptionService>(),
            Mock.Of<IIntegrationSettingsProvider>(),
            Mock.Of<ILogger<SettingsService>>());
    }

    #region General Settings

    [Fact]
    public async Task GetGeneralSettingsAsync_ReturnsDeserializedSettings()
    {
        var general = new GeneralSettings
        {
            ApplicationName = "My Panel",
            ApplicationUrl = "https://panel.local",
            SupportEmail = "support@test.com",
            MaintenanceMode = true,
            MaintenanceMessage = "Down for updates"
        };
        SetupSettings(generalJson: JsonSerializer.Serialize(general, JsonOptions));

        var result = await _sut.GetGeneralSettingsAsync();

        result.ApplicationName.Should().Be("My Panel");
        result.ApplicationUrl.Should().Be("https://panel.local");
        result.SupportEmail.Should().Be("support@test.com");
        result.MaintenanceMode.Should().BeTrue();
        result.MaintenanceMessage.Should().Be("Down for updates");
    }

    [Fact]
    public async Task GetGeneralSettingsAsync_WithEmptyJson_ReturnsDefaults()
    {
        SetupSettings(generalJson: "{}");

        var result = await _sut.GetGeneralSettingsAsync();

        result.ApplicationName.Should().Be("Nebula Panel");
        result.MaintenanceMode.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateGeneralSettingsAsync_WithValidRequest_UpdatesAndReturnsDto()
    {
        SetupSettings();
        var userId = Guid.NewGuid();
        var request = new UpdateGeneralSettingsRequest("Updated Panel", "https://new.url", "new@test.com", false, null);

        var result = await _sut.UpdateGeneralSettingsAsync(request, userId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApplicationName.Should().Be("Updated Panel");
        result.Value.ApplicationUrl.Should().Be("https://new.url");
        _mockSettingsRepo.Verify(r => r.UpdateAsync(It.IsAny<SystemSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateGeneralSettingsAsync_WithEmptyName_ReturnsFailure()
    {
        var request = new UpdateGeneralSettingsRequest("", null, null, false, null);

        var result = await _sut.UpdateGeneralSettingsAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("required");
    }

    [Fact]
    public async Task UpdateGeneralSettingsAsync_WithWhitespaceName_ReturnsFailure()
    {
        var request = new UpdateGeneralSettingsRequest("   ", null, null, false, null);

        var result = await _sut.UpdateGeneralSettingsAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Update Settings

    [Fact]
    public async Task GetUpdateSettingsAsync_ReturnsDeserializedSettings()
    {
        var update = new UpdateSettings
        {
            Enabled = true,
            AutoCheck = false,
            CheckIntervalHours = 12,
            Channel = UpdateChannel.Preview,
            LatestAvailableVersion = "2.0.0"
        };
        SetupSettings(updateJson: JsonSerializer.Serialize(update, JsonOptions));

        var result = await _sut.GetUpdateSettingsAsync();

        result.Enabled.Should().BeTrue();
        result.AutoCheck.Should().BeFalse();
        result.CheckIntervalHours.Should().Be(12);
        result.Channel.Should().Be("Preview");
        result.LatestAvailableVersion.Should().Be("2.0.0");
    }

    [Fact]
    public async Task UpdateUpdateSettingsAsync_WithValidRequest_UpdatesAndReturnsDto()
    {
        SetupSettings();
        var request = new UpdateUpdateSettingsRequest(true, true, 24, "Stable");

        var result = await _sut.UpdateUpdateSettingsAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Enabled.Should().BeTrue();
        result.Value.CheckIntervalHours.Should().Be(24);
        result.Value.Channel.Should().Be("Stable");
    }

    [Fact]
    public async Task UpdateUpdateSettingsAsync_WithInvalidInterval_ReturnsFailure()
    {
        var request = new UpdateUpdateSettingsRequest(true, true, 0, "Stable");

        var result = await _sut.UpdateUpdateSettingsAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("between 1 and 168");
    }

    [Fact]
    public async Task UpdateUpdateSettingsAsync_WithTooHighInterval_ReturnsFailure()
    {
        var request = new UpdateUpdateSettingsRequest(true, true, 200, "Stable");

        var result = await _sut.UpdateUpdateSettingsAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("between 1 and 168");
    }

    [Fact]
    public async Task UpdateUpdateSettingsAsync_WithInvalidChannel_ReturnsFailure()
    {
        var request = new UpdateUpdateSettingsRequest(true, true, 6, "InvalidChannel");

        var result = await _sut.UpdateUpdateSettingsAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid update channel");
    }

    #endregion

    #region Appearance Settings

    [Fact]
    public async Task GetAppearanceSettingsAsync_ReturnsSettingsWithAvailableOptions()
    {
        SetupSettings();

        var result = await _sut.GetAppearanceSettingsAsync();

        result.DefaultTheme.Should().Be("dark");
        result.AccentColor.Should().Be("purple");
        result.AvailableThemes.Should().NotBeEmpty();
        result.AvailableAccentColors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateAppearanceSettingsAsync_WithValidThemeAndColor_Updates()
    {
        SetupSettings();
        var request = new UpdateAppearanceSettingsRequest("light", true, "blue");

        var result = await _sut.UpdateAppearanceSettingsAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultTheme.Should().Be("light");
        result.Value.AccentColor.Should().Be("blue");
    }

    [Fact]
    public async Task UpdateAppearanceSettingsAsync_WithInvalidTheme_ReturnsFailure()
    {
        var request = new UpdateAppearanceSettingsRequest("neon", true, "purple");

        var result = await _sut.UpdateAppearanceSettingsAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid theme");
    }

    [Fact]
    public async Task UpdateAppearanceSettingsAsync_WithInvalidAccentColor_ReturnsFailure()
    {
        var request = new UpdateAppearanceSettingsRequest("dark", true, "rainbow");

        var result = await _sut.UpdateAppearanceSettingsAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid accent color");
    }

    #endregion

    #region Database Info

    [Fact]
    public async Task GetDatabaseInfoAsync_DelegatesToService()
    {
        _mockDbInfoService.Setup(s => s.GetDatabaseInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DatabaseInfo("SQLite", "Data Source=test.db", "/path/test.db", 1048576, 10, null));

        var result = await _sut.GetDatabaseInfoAsync();

        result.Provider.Should().Be("SQLite");
        result.SizeBytes.Should().Be(1048576);
        result.SizeFormatted.Should().Be("1 MB");
        result.TableCount.Should().Be(10);
    }

    [Fact]
    public async Task CreateDatabaseBackupAsync_OnSuccess_ReturnsStream()
    {
        var stream = new MemoryStream();
        _mockDbInfoService.Setup(s => s.CreateBackupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        var result = await _sut.CreateDatabaseBackupAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(stream);
    }

    [Fact]
    public async Task CreateDatabaseBackupAsync_OnNotSupported_ReturnsFailure()
    {
        _mockDbInfoService.Setup(s => s.CreateBackupAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException("PostgreSQL backup not supported"));

        var result = await _sut.CreateDatabaseBackupAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("PostgreSQL backup not supported");
    }

    [Fact]
    public async Task CreateDatabaseBackupAsync_OnGenericError_ReturnsFailure()
    {
        _mockDbInfoService.Setup(s => s.CreateBackupAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk full"));

        var result = await _sut.CreateDatabaseBackupAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to create database backup");
    }

    #endregion

    #region Export Settings

    [Fact]
    public async Task ExportSettingsAsync_AggregatesAllSettings()
    {
        SetupSettings();

        var result = await _sut.ExportSettingsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().Be("1.0");
        result.Value.General.Should().NotBeNull();
        result.Value.Update.Should().NotBeNull();
        result.Value.Appearance.Should().NotBeNull();
    }

    #endregion

    #region Preview Import

    [Fact]
    public async Task PreviewImportAsync_WithValidJson_ReturnsPreview()
    {
        var exportDto = CreateValidExportDto();
        var json = JsonSerializer.Serialize(exportDto, JsonOptions);

        var result = await _sut.PreviewImportAsync(json);

        result.IsSuccess.Should().BeTrue();
        result.Value!.General.Should().NotBeNull();
        result.Value.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task PreviewImportAsync_WithInvalidJson_ReturnsFailure()
    {
        var result = await _sut.PreviewImportAsync("not valid json {{{");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid JSON");
    }

    [Fact]
    public async Task PreviewImportAsync_WithWrongVersion_AddsWarning()
    {
        var exportDto = CreateValidExportDto() with { Version = "2.0" };
        var json = JsonSerializer.Serialize(exportDto, JsonOptions);

        var result = await _sut.PreviewImportAsync(json);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Warnings.Should().ContainSingle()
            .Which.Should().Contain("may not be fully compatible");
    }

    #endregion

    #region Import Settings

    [Fact]
    public async Task ImportSettingsAsync_WithValidJson_ImportsSelectedSections()
    {
        SetupSettings();
        var exportDto = CreateValidExportDto();
        var json = JsonSerializer.Serialize(exportDto, JsonOptions);
        var request = new SettingsImportRequest(
            ImportGeneral: true,
            ImportUpdate: true,
            ImportAppearance: true,
            JsonContent: json);

        var result = await _sut.ImportSettingsAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        _mockSettingsRepo.Verify(r => r.UpdateAsync(It.IsAny<SystemSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportSettingsAsync_WithInvalidJson_ReturnsFailure()
    {
        var request = new SettingsImportRequest(true, true, true, "bad json");

        var result = await _sut.ImportSettingsAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid JSON");
    }

    [Fact]
    public async Task ImportSettingsAsync_WithInvalidTheme_FallsBackToDefault()
    {
        SetupSettings();
        var exportDto = CreateValidExportDto() with
        {
            Appearance = new AppearanceSettingsDto("invalidtheme", true, "invalidcolor", [], [])
        };
        var json = JsonSerializer.Serialize(exportDto, JsonOptions);
        var request = new SettingsImportRequest(false, false, true, json);

        var result = await _sut.ImportSettingsAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        // Verify the update was called - the service falls back to "dark" and "purple"
        _mockSettingsRepo.Verify(r => r.UpdateAsync(
            It.Is<SystemSettings>(s => s.AppearanceSettingsJson.Contains("dark") && s.AppearanceSettingsJson.Contains("purple")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportSettingsAsync_OnlyImportsSelectedSections()
    {
        SetupSettings();
        var exportDto = CreateValidExportDto();
        var json = JsonSerializer.Serialize(exportDto, JsonOptions);
        // Only import general, not update or appearance
        var request = new SettingsImportRequest(true, false, false, json);

        var result = await _sut.ImportSettingsAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        _mockSettingsRepo.Verify(r => r.UpdateAsync(
            It.Is<SystemSettings>(s =>
                s.GeneralSettingsJson.Contains("Imported Panel") &&
                s.UpdateSettingsJson == "{}" &&
                s.AppearanceSettingsJson == "{}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    private void SetupSettings(string? generalJson = null, string? updateJson = null, string? appearanceJson = null)
    {
        var settings = new SystemSettings
        {
            GeneralSettingsJson = generalJson ?? "{}",
            UpdateSettingsJson = updateJson ?? "{}",
            AppearanceSettingsJson = appearanceJson ?? "{}"
        };
        _mockSettingsRepo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
    }

    private static SettingsExportDto CreateValidExportDto()
    {
        return new SettingsExportDto(
            Version: "1.0",
            ExportedAt: DateTime.UtcNow,
            PanelVersion: "1.0.0",
            General: new GeneralSettingsDto("Imported Panel", null, null, false, null),
            Update: new UpdateSettingsDto(true, true, 6, "Stable", null, null, null),
            Appearance: new AppearanceSettingsDto("dark", true, "purple", [], []));
    }
}
