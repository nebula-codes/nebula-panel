using NebulaPanel.Application.Tests.Builders;
using NebulaPanel.Application.Tests.Fixtures;

namespace NebulaPanel.Application.Tests.Services;

public class GameServerServiceTests
{
    private readonly ServiceTestFixture _fixture;
    private readonly Mock<ISteamCmdService> _steamCmdServiceMock;
    private readonly GameServerService _sut;

    public GameServerServiceTests()
    {
        _fixture = new ServiceTestFixture();
        _steamCmdServiceMock = new Mock<ISteamCmdService>();

        _sut = new GameServerService(
            _fixture.GameServerRepository.Object,
            _fixture.GameRepository.Object,
            _steamCmdServiceMock.Object,
            _fixture.NodeExecutorFactory.Object,
            rconService: null,
            _fixture.ImageService.Object,
            serverActivityService: null,
            _fixture.EncryptionService.Object,
            _fixture.Logger.Object);
    }

    #region GetServerByIdAsync

    [Fact]
    public async Task GetServerByIdAsync_WithValidId_ReturnsServer()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().Build();
        _fixture.SetupServerExists(server);

        // Act
        var result = await _sut.GetServerByIdAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(server.Id);
        result.Value.Name.Should().Be(server.Name);
    }

    [Fact]
    public async Task GetServerByIdAsync_WithInvalidId_ReturnsFailure()
    {
        // Arrange
        var invalidId = Guid.NewGuid();
        _fixture.SetupServerNotFound(invalidId);

        // Act
        var result = await _sut.GetServerByIdAsync(invalidId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region CreateServerAsync

    [Fact]
    public async Task CreateServerAsync_WithValidRequest_CreatesServer()
    {
        // Arrange
        var game = _fixture.CreateGameBuilder().Build();
        var ownerId = Guid.NewGuid();
        _fixture.SetupGameExists(game);
        _fixture.SetupServerNameAvailable();
        _fixture.SetupPortAvailable();
        _fixture.GameServerRepository
            .Setup(r => r.AddAsync(It.IsAny<GameServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameServer s, CancellationToken _) => s);
        _fixture.GameServerRepository
            .Setup(r => r.GetByIdWithGameAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new GameServerBuilder()
                .WithId(id)
                .WithGame(game)
                .Build());

        var request = new CreateGameServerRequest(
            Name: "Test Server",
            Description: "A test server",
            IconUrl: null,
            GameId: game.Id,
            DeploymentType: ServerDeploymentType.Native,
            InstallPath: Path.Combine(Path.GetTempPath(), "test-server"),
            DockerConfig: null,
            NativeConfig: new NativeConfigurationRequest(
                WorkingDirectory: Path.Combine(Path.GetTempPath(), "test-server"),
                ExecutablePath: "test.exe",
                Arguments: "",
                EnvironmentVariables: null,
                JavaPath: null,
                JavaArguments: null,
                RunAsService: false,
                RunAsUser: null,
                UseStartupScript: false,
                StartupScriptPath: null
            ),
            RconConfig: null,
            PrimaryPort: 25565,
            AdditionalPorts: null,
            BindAddress: "0.0.0.0",
            ResourceLimits: null
        );

        // Act
        var result = await _sut.CreateServerAsync(request, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fixture.GameServerRepository.Verify(
            r => r.AddAsync(It.IsAny<GameServer>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateServerAsync_WithDuplicateName_ReturnsFailure()
    {
        // Arrange
        var game = _fixture.CreateGameBuilder().Build();
        var ownerId = Guid.NewGuid();
        _fixture.SetupGameExists(game);
        _fixture.SetupServerNameTaken();
        _fixture.SetupPortAvailable();

        var request = new CreateGameServerRequest(
            Name: "Duplicate Server",
            Description: null,
            IconUrl: null,
            GameId: game.Id,
            DeploymentType: ServerDeploymentType.Native,
            InstallPath: "/tmp/test",
            DockerConfig: null,
            NativeConfig: new NativeConfigurationRequest("/tmp/test", "test.exe", "", null, null, null, false, null, false, null),
            RconConfig: null,
            PrimaryPort: 25565,
            AdditionalPorts: null,
            BindAddress: "0.0.0.0",
            ResourceLimits: null
        );

        // Act
        var result = await _sut.CreateServerAsync(request, ownerId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorInfo?.Code.Should().Be(ErrorCode.AlreadyExists);
    }

    [Fact]
    public async Task CreateServerAsync_WithPortInUse_ReturnsFailure()
    {
        // Arrange
        var game = _fixture.CreateGameBuilder().Build();
        var ownerId = Guid.NewGuid();
        _fixture.SetupGameExists(game);
        _fixture.SetupServerNameAvailable();
        _fixture.SetupPortInUse();

        var request = new CreateGameServerRequest(
            Name: "New Server",
            Description: null,
            IconUrl: null,
            GameId: game.Id,
            DeploymentType: ServerDeploymentType.Native,
            InstallPath: "/tmp/test",
            DockerConfig: null,
            NativeConfig: new NativeConfigurationRequest("/tmp/test", "test.exe", "", null, null, null, false, null, false, null),
            RconConfig: null,
            PrimaryPort: 25565,
            AdditionalPorts: null,
            BindAddress: "0.0.0.0",
            ResourceLimits: null
        );

        // Act
        var result = await _sut.CreateServerAsync(request, ownerId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already in use");
    }

    [Fact]
    public async Task CreateServerAsync_WithNonExistentGame_ReturnsFailure()
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _fixture.SetupGameNotFound(nonExistentGameId);

        var request = new CreateGameServerRequest(
            Name: "New Server",
            Description: null,
            IconUrl: null,
            GameId: nonExistentGameId,
            DeploymentType: ServerDeploymentType.Native,
            InstallPath: "/tmp/test",
            DockerConfig: null,
            NativeConfig: new NativeConfigurationRequest("/tmp/test", "test.exe", "", null, null, null, false, null, false, null),
            RconConfig: null,
            PrimaryPort: 25565,
            AdditionalPorts: null,
            BindAddress: "0.0.0.0",
            ResourceLimits: null
        );

        // Act
        var result = await _sut.CreateServerAsync(request, ownerId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Game with ID");
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region StartServerAsync

    [Fact]
    public async Task StartServerAsync_WhenStopped_StartsSuccessfully()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().AsStopped().Build();
        _fixture.SetupServerExists(server);
        _fixture.SetupExecutorStartSuccess();

        // Act
        var result = await _sut.StartServerAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fixture.NativeExecutor.Verify(
            e => e.StartAsync(It.IsAny<GameServer>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartServerAsync_WhenAlreadyRunning_ReturnsFailure()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().AsRunning().Build();
        _fixture.SetupServerExists(server);

        // Act
        var result = await _sut.StartServerAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("server is running");
    }

    [Fact]
    public async Task StartServerAsync_WhenInstalling_ReturnsFailure()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().AsInstalling().Build();
        _fixture.SetupServerExists(server);

        // Act
        var result = await _sut.StartServerAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("installing");
    }

    [Fact]
    public async Task StartServerAsync_WhenExecutorFails_ReturnsFailure()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().AsStopped().Build();
        _fixture.SetupServerExists(server);
        _fixture.SetupExecutorStartFailure();

        // Act
        var result = await _sut.StartServerAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to start");
    }

    [Fact]
    public async Task StartServerAsync_WithNonExistentServer_ReturnsFailure()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        _fixture.SetupServerNotFound(serverId);

        // Act
        var result = await _sut.StartServerAsync(serverId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region StopServerAsync

    [Fact]
    public async Task StopServerAsync_WhenRunning_StopsSuccessfully()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().AsRunning().Build();
        _fixture.SetupServerExists(server);
        _fixture.SetupExecutorStopSuccess();

        // Act
        var result = await _sut.StopServerAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fixture.NativeExecutor.Verify(
            e => e.StopAsync(It.IsAny<GameServer>(), false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopServerAsync_WhenAlreadyStopped_ReturnsFailure()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().AsStopped().Build();
        _fixture.SetupServerExists(server);

        // Act
        var result = await _sut.StopServerAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorInfo?.Code.Should().Be(ErrorCode.ServerStopped);
    }

    #endregion

    #region RestartServerAsync

    [Fact]
    public async Task RestartServerAsync_WhenRunning_RestartsSuccessfully()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().AsRunning().Build();
        _fixture.SetupServerExists(server);
        _fixture.SetupExecutorRestartSuccess();

        // Act
        var result = await _sut.RestartServerAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fixture.NativeExecutor.Verify(
            e => e.RestartAsync(It.IsAny<GameServer>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RestartServerAsync_WhenInstalling_ReturnsFailure()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().AsInstalling().Build();
        _fixture.SetupServerExists(server);

        // Act
        var result = await _sut.RestartServerAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("installing");
    }

    #endregion

    #region DeleteServerAsync

    [Fact]
    public async Task DeleteServerAsync_WithValidId_DeletesServer()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().AsStopped().Build();
        _fixture.GameServerRepository.Setup(r => r.GetByIdAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        // Act
        var result = await _sut.DeleteServerAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fixture.GameServerRepository.Verify(
            r => r.DeleteAsync(server.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteServerAsync_WithRunningServer_ReturnsFailure()
    {
        // Arrange
        var server = _fixture.CreateServerBuilder().AsRunning().Build();
        _fixture.GameServerRepository.Setup(r => r.GetByIdAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        // Act
        var result = await _sut.DeleteServerAsync(server.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorInfo?.Code.Should().Be(ErrorCode.ServerRunning);
    }

    [Fact]
    public async Task DeleteServerAsync_WithNonExistentServer_ReturnsFailure()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        _fixture.GameServerRepository.Setup(r => r.GetByIdAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameServer?)null);

        // Act
        var result = await _sut.DeleteServerAsync(serverId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region GetAllServersAsync

    [Fact]
    public async Task GetAllServersAsync_ReturnsAllServers()
    {
        // Arrange
        var servers = new List<GameServer>
        {
            _fixture.CreateServerBuilder().WithName("Server 1").Build(),
            _fixture.CreateServerBuilder().WithName("Server 2").Build()
        };
        _fixture.GameServerRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(servers);

        // Act
        var result = await _sut.GetAllServersAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllServersAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        _fixture.GameServerRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameServer>());

        // Act
        var result = await _sut.GetAllServersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetServersByOwnerAsync

    [Fact]
    public async Task GetServersByOwnerAsync_ReturnsServersForOwner()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var servers = new List<GameServer>
        {
            _fixture.CreateServerBuilder().WithOwnerId(ownerId).Build()
        };
        _fixture.GameServerRepository.Setup(r => r.GetByOwnerIdAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(servers);

        // Act
        var result = await _sut.GetServersByOwnerAsync(ownerId);

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion
}
