using NebulaPanel.Application.Tests.Builders;

namespace NebulaPanel.Application.Tests.Fixtures;

/// <summary>
/// Common mock setup for service tests.
/// </summary>
public class ServiceTestFixture
{
    public Mock<IGameServerRepository> GameServerRepository { get; }
    public Mock<IGameRepository> GameRepository { get; }
    public Mock<IServerExecutorFactory> ExecutorFactory { get; }
    public Mock<IServerExecutor> NativeExecutor { get; }
    public Mock<IServerExecutor> DockerExecutor { get; }
    public Mock<IImageService> ImageService { get; }
    public Mock<ILogger<GameServerService>> Logger { get; }

    public ServiceTestFixture()
    {
        GameServerRepository = new Mock<IGameServerRepository>();
        GameRepository = new Mock<IGameRepository>();
        ExecutorFactory = new Mock<IServerExecutorFactory>();
        NativeExecutor = new Mock<IServerExecutor>();
        DockerExecutor = new Mock<IServerExecutor>();
        ImageService = new Mock<IImageService>();
        Logger = new Mock<ILogger<GameServerService>>();

        // Setup executor factory to return appropriate executors
        NativeExecutor.Setup(e => e.DeploymentType).Returns(ServerDeploymentType.Native);
        DockerExecutor.Setup(e => e.DeploymentType).Returns(ServerDeploymentType.Docker);

        ExecutorFactory.Setup(f => f.GetExecutor(ServerDeploymentType.Native)).Returns(NativeExecutor.Object);
        ExecutorFactory.Setup(f => f.GetExecutor(ServerDeploymentType.Docker)).Returns(DockerExecutor.Object);
    }

    public GameServerBuilder CreateServerBuilder() => new();
    public GameBuilder CreateGameBuilder() => new();
    public UserBuilder CreateUserBuilder() => new();

    /// <summary>
    /// Sets up repository to return a server by ID.
    /// </summary>
    public void SetupServerExists(GameServer server)
    {
        GameServerRepository.Setup(r => r.GetByIdAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);
        GameServerRepository.Setup(r => r.GetByIdWithGameAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);
        GameServerRepository.Setup(r => r.ExistsAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Sets up repository to return null for a server ID (not found).
    /// </summary>
    public void SetupServerNotFound(Guid serverId)
    {
        GameServerRepository.Setup(r => r.GetByIdAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameServer?)null);
        GameServerRepository.Setup(r => r.GetByIdWithGameAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameServer?)null);
        GameServerRepository.Setup(r => r.ExistsAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    /// <summary>
    /// Sets up game repository to return a game by ID.
    /// </summary>
    public void SetupGameExists(Game game)
    {
        GameRepository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
    }

    /// <summary>
    /// Sets up game repository to return null for a game ID (not found).
    /// </summary>
    public void SetupGameNotFound(Guid gameId)
    {
        GameRepository.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);
    }

    /// <summary>
    /// Sets up the executor to successfully start a server.
    /// </summary>
    public void SetupExecutorStartSuccess(ServerDeploymentType deploymentType = ServerDeploymentType.Native)
    {
        var executor = deploymentType == ServerDeploymentType.Native ? NativeExecutor : DockerExecutor;
        executor.Setup(e => e.StartAsync(It.IsAny<GameServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Sets up the executor to fail when starting a server.
    /// </summary>
    public void SetupExecutorStartFailure(ServerDeploymentType deploymentType = ServerDeploymentType.Native)
    {
        var executor = deploymentType == ServerDeploymentType.Native ? NativeExecutor : DockerExecutor;
        executor.Setup(e => e.StartAsync(It.IsAny<GameServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    /// <summary>
    /// Sets up the executor to successfully stop a server.
    /// </summary>
    public void SetupExecutorStopSuccess(ServerDeploymentType deploymentType = ServerDeploymentType.Native)
    {
        var executor = deploymentType == ServerDeploymentType.Native ? NativeExecutor : DockerExecutor;
        executor.Setup(e => e.StopAsync(It.IsAny<GameServer>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Sets up the executor to successfully restart a server.
    /// </summary>
    public void SetupExecutorRestartSuccess(ServerDeploymentType deploymentType = ServerDeploymentType.Native)
    {
        var executor = deploymentType == ServerDeploymentType.Native ? NativeExecutor : DockerExecutor;
        executor.Setup(e => e.RestartAsync(It.IsAny<GameServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Sets up repository to allow server name (not duplicate).
    /// </summary>
    public void SetupServerNameAvailable()
    {
        GameServerRepository.Setup(r => r.NameExistsForOwnerAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    /// <summary>
    /// Sets up repository to indicate server name is taken.
    /// </summary>
    public void SetupServerNameTaken()
    {
        GameServerRepository.Setup(r => r.NameExistsForOwnerAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Sets up repository to indicate port is available.
    /// </summary>
    public void SetupPortAvailable()
    {
        GameServerRepository.Setup(r => r.IsPortInUseAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    /// <summary>
    /// Sets up repository to indicate port is in use.
    /// </summary>
    public void SetupPortInUse()
    {
        GameServerRepository.Setup(r => r.IsPortInUseAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }
}
