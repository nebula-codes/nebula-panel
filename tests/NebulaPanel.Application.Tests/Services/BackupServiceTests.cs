using NebulaPanel.Application.Tests.Builders;

namespace NebulaPanel.Application.Tests.Services;

public class BackupServiceTests
{
    private readonly Mock<IBackupRepository> _backupRepositoryMock;
    private readonly Mock<IBackupFileManager> _backupFileManagerMock;
    private readonly Mock<IGameServerRepository> _serverRepositoryMock;
    private readonly Mock<IGameServerService> _serverServiceMock;
    private readonly Mock<IBackupSettings> _backupSettingsMock;
    private readonly Mock<ILogger<BackupService>> _loggerMock;
    private readonly BackupService _sut;

    public BackupServiceTests()
    {
        _backupRepositoryMock = new Mock<IBackupRepository>();
        _backupFileManagerMock = new Mock<IBackupFileManager>();
        _serverRepositoryMock = new Mock<IGameServerRepository>();
        _serverServiceMock = new Mock<IGameServerService>();
        _backupSettingsMock = new Mock<IBackupSettings>();
        _loggerMock = new Mock<ILogger<BackupService>>();

        _backupSettingsMock.Setup(s => s.GetResolvedBackupDirectory())
            .Returns(Path.Combine(Path.GetTempPath(), "backups"));
        _backupSettingsMock.Setup(s => s.CreatePreRestoreBackup).Returns(true);

        _sut = new BackupService(
            _backupRepositoryMock.Object,
            _backupFileManagerMock.Object,
            _serverRepositoryMock.Object,
            _serverServiceMock.Object,
            _backupSettingsMock.Object,
            _loggerMock.Object);
    }

    #region CreateFullBackupAsync

    [Fact]
    public async Task CreateFullBackupAsync_WithValidServer_CreatesBackup()
    {
        // Arrange
        var server = new GameServerBuilder()
            .WithInstallPath(Path.GetTempPath())
            .Build();

        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);
        _backupRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Backup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Backup b, CancellationToken _) => b);
        _backupRepositoryMock.Setup(r => r.GetByIdWithServerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => CreateTestBackup(id, server));
        _backupFileManagerMock.Setup(m => m.CreateBackupArchiveAsync(
                It.IsAny<GameServer>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/path/to/backup.zip");
        _backupFileManagerMock.Setup(m => m.GetBackupFileSize(It.IsAny<string>()))
            .Returns(1024 * 1024);

        // Act
        var result = await _sut.CreateFullBackupAsync(server.Id, "Test Backup", "Test notes");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        _backupRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Backup>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateFullBackupAsync_WithNonExistentServer_ReturnsFailure()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameServer?)null);

        // Act
        var result = await _sut.CreateFullBackupAsync(serverId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task CreateFullBackupAsync_WithNonExistentInstallPath_ReturnsFailure()
    {
        // Arrange
        var server = new GameServerBuilder()
            .WithInstallPath("/path/that/does/not/exist")
            .Build();

        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        // Act
        var result = await _sut.CreateFullBackupAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("does not exist");
    }

    #endregion

    #region CreateSelectiveBackupAsync

    [Fact]
    public async Task CreateSelectiveBackupAsync_WithValidPaths_CreatesBackup()
    {
        // Arrange
        var server = new GameServerBuilder()
            .WithInstallPath(Path.GetTempPath())
            .Build();
        var paths = new[] { "world", "server.properties" };

        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);
        _backupRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Backup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Backup b, CancellationToken _) => b);
        _backupRepositoryMock.Setup(r => r.GetByIdWithServerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => CreateTestBackup(id, server, BackupScope.Selective));
        _backupFileManagerMock.Setup(m => m.CreateBackupArchiveAsync(
                It.IsAny<GameServer>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<IEnumerable<string>>(p => p != null && p.Count() == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/path/to/selective-backup.zip");
        _backupFileManagerMock.Setup(m => m.GetBackupFileSize(It.IsAny<string>()))
            .Returns(512 * 1024);

        // Act
        var result = await _sut.CreateSelectiveBackupAsync(server.Id, paths);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSelectiveBackupAsync_WithNoPaths_ReturnsFailure()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var emptyPaths = Array.Empty<string>();

        // Act
        var result = await _sut.CreateSelectiveBackupAsync(serverId, emptyPaths);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().ContainEquivalentOf("at least one path");
    }

    [Fact]
    public async Task CreateSelectiveBackupAsync_WithNullPaths_ReturnsFailure()
    {
        // Arrange
        var serverId = Guid.NewGuid();

        // Act
        var result = await _sut.CreateSelectiveBackupAsync(serverId, null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().ContainEquivalentOf("at least one path");
    }

    #endregion

    #region RestoreBackupAsync

    [Fact]
    public async Task RestoreBackupAsync_WhenServerRunning_StopsServerFirst()
    {
        // Arrange
        var server = new GameServerBuilder().AsRunning().Build();
        var backup = CreateTestBackup(Guid.NewGuid(), server);

        _backupRepositoryMock.Setup(r => r.GetByIdWithServerAsync(backup.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backup);
        _backupFileManagerMock.Setup(m => m.BackupFileExists(It.IsAny<string>()))
            .Returns(true);
        _serverServiceMock.Setup(s => s.StopServerAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _serverServiceMock.Setup(s => s.StartServerAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.RestoreBackupAsync(backup.Id, stopServerFirst: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _serverServiceMock.Verify(s => s.StopServerAsync(server.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreBackupAsync_WithNonExistentBackup_ReturnsFailure()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        _backupRepositoryMock.Setup(r => r.GetByIdWithServerAsync(backupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Backup?)null);

        // Act
        var result = await _sut.RestoreBackupAsync(backupId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task RestoreBackupAsync_WithMissingBackupFile_ReturnsFailure()
    {
        // Arrange
        var server = new GameServerBuilder().Build();
        var backup = CreateTestBackup(Guid.NewGuid(), server);

        _backupRepositoryMock.Setup(r => r.GetByIdWithServerAsync(backup.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backup);
        _backupFileManagerMock.Setup(m => m.BackupFileExists(It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _sut.RestoreBackupAsync(backup.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorInfo?.Code.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task RestoreBackupAsync_WithFailedBackup_ReturnsFailure()
    {
        // Arrange
        var server = new GameServerBuilder().Build();
        var backup = CreateTestBackup(Guid.NewGuid(), server, status: BackupStatus.Failed);

        _backupRepositoryMock.Setup(r => r.GetByIdWithServerAsync(backup.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backup);

        // Act
        var result = await _sut.RestoreBackupAsync(backup.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Only completed backups");
    }

    #endregion

    #region ApplyRetentionPolicyAsync

    [Fact]
    public async Task ApplyRetentionPolicyAsync_DeletesOldBackups()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var server = new GameServerBuilder().WithId(serverId).Build();
        var oldBackups = new List<Backup>
        {
            CreateTestBackup(Guid.NewGuid(), server),
            CreateTestBackup(Guid.NewGuid(), server)
        };

        _backupRepositoryMock.Setup(r => r.GetBackupsToDeleteForRetentionAsync(serverId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldBackups);
        _backupFileManagerMock.Setup(m => m.BackupFileExists(It.IsAny<string>()))
            .Returns(true);

        // Act
        var result = await _sut.ApplyRetentionPolicyAsync(serverId, keepCount: 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        _backupFileManagerMock.Verify(m => m.DeleteBackupFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _backupRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithInvalidKeepCount_ReturnsFailure()
    {
        // Arrange
        var serverId = Guid.NewGuid();

        // Act
        var result = await _sut.ApplyRetentionPolicyAsync(serverId, keepCount: 0);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("at least 1");
    }

    #endregion

    #region DeleteBackupAsync

    [Fact]
    public async Task DeleteBackupAsync_WithValidId_DeletesBackup()
    {
        // Arrange
        var server = new GameServerBuilder().Build();
        var backup = CreateTestBackup(Guid.NewGuid(), server);

        _backupRepositoryMock.Setup(r => r.GetByIdAsync(backup.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backup);
        _backupFileManagerMock.Setup(m => m.BackupFileExists(backup.FilePath))
            .Returns(true);

        // Act
        var result = await _sut.DeleteBackupAsync(backup.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _backupFileManagerMock.Verify(m => m.DeleteBackupFileAsync(backup.FilePath, It.IsAny<CancellationToken>()), Times.Once);
        _backupRepositoryMock.Verify(r => r.DeleteAsync(backup.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteBackupAsync_WithNonExistentBackup_ReturnsFailure()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        _backupRepositoryMock.Setup(r => r.GetByIdAsync(backupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Backup?)null);

        // Act
        var result = await _sut.DeleteBackupAsync(backupId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region GetBackupByIdAsync

    [Fact]
    public async Task GetBackupByIdAsync_WithValidId_ReturnsBackup()
    {
        // Arrange
        var server = new GameServerBuilder().Build();
        var backup = CreateTestBackup(Guid.NewGuid(), server);

        _backupRepositoryMock.Setup(r => r.GetByIdWithServerAsync(backup.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backup);

        // Act
        var result = await _sut.GetBackupByIdAsync(backup.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(backup.Id);
    }

    [Fact]
    public async Task GetBackupByIdAsync_WithNonExistentId_ReturnsFailure()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        _backupRepositoryMock.Setup(r => r.GetByIdWithServerAsync(backupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Backup?)null);

        // Act
        var result = await _sut.GetBackupByIdAsync(backupId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region Helpers

    private static Backup CreateTestBackup(
        Guid id,
        GameServer server,
        BackupScope scope = BackupScope.Full,
        BackupStatus status = BackupStatus.Completed)
    {
        return new Backup
        {
            Id = id,
            ServerId = server.Id,
            Server = server,
            Name = "Test Backup",
            FilePath = "/path/to/backup.zip",
            FileSizeBytes = 1024 * 1024,
            CreatedAt = DateTime.UtcNow,
            Type = BackupType.Manual,
            Status = status,
            Scope = scope
        };
    }

    #endregion
}
