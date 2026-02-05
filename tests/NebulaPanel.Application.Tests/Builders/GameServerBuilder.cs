namespace NebulaPanel.Application.Tests.Builders;

/// <summary>
/// Fluent builder for creating GameServer test instances.
/// </summary>
public class GameServerBuilder
{
    private readonly GameServer _server;

    public GameServerBuilder()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _server = new GameServer
        {
            Id = Guid.NewGuid(),
            Name = "Test Server",
            Description = "A test server",
            GameId = gameId,
            Game = new Game
            {
                Id = gameId,
                Name = "Test Game",
                Slug = "test-game",
                ExecutableType = ExecutableType.Exe,
                DefaultExecutablePath = "test.exe",
                DefaultStartCommand = "",
                SupportsDocker = true
            },
            DeploymentType = ServerDeploymentType.Native,
            InstallPath = Path.Combine(Path.GetTempPath(), "test-server"),
            PrimaryPort = 25565,
            BindAddress = "0.0.0.0",
            Status = ServerStatus.Stopped,
            OwnerId = userId,
            Owner = new User
            {
                Id = userId,
                Username = "testuser",
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            NativeConfig = new NativeConfiguration
            {
                WorkingDirectory = Path.Combine(Path.GetTempPath(), "test-server"),
                ExecutablePath = "test.exe",
                Arguments = ""
            }
        };
    }

    public GameServerBuilder WithId(Guid id)
    {
        _server.Id = id;
        return this;
    }

    public GameServerBuilder WithName(string name)
    {
        _server.Name = name;
        return this;
    }

    public GameServerBuilder WithDescription(string? description)
    {
        _server.Description = description;
        return this;
    }

    public GameServerBuilder WithGame(Game game)
    {
        _server.Game = game;
        _server.GameId = game.Id;
        return this;
    }

    public GameServerBuilder WithOwner(User owner)
    {
        _server.Owner = owner;
        _server.OwnerId = owner.Id;
        return this;
    }

    public GameServerBuilder WithOwnerId(Guid ownerId)
    {
        _server.OwnerId = ownerId;
        _server.Owner.Id = ownerId;
        return this;
    }

    public GameServerBuilder WithStatus(ServerStatus status)
    {
        _server.Status = status;
        return this;
    }

    public GameServerBuilder WithDeploymentType(ServerDeploymentType deploymentType)
    {
        _server.DeploymentType = deploymentType;
        return this;
    }

    public GameServerBuilder WithInstallPath(string installPath)
    {
        _server.InstallPath = installPath;
        if (_server.NativeConfig != null)
        {
            _server.NativeConfig.WorkingDirectory = installPath;
        }
        return this;
    }

    public GameServerBuilder WithPrimaryPort(int port)
    {
        _server.PrimaryPort = port;
        return this;
    }

    public GameServerBuilder WithBindAddress(string bindAddress)
    {
        _server.BindAddress = bindAddress;
        return this;
    }

    public GameServerBuilder WithNativeConfig(NativeConfiguration? config)
    {
        _server.NativeConfig = config;
        return this;
    }

    public GameServerBuilder WithDockerConfig(DockerConfiguration? config)
    {
        _server.DockerConfig = config;
        return this;
    }

    public GameServerBuilder WithRconConfig(RconConfiguration? config)
    {
        _server.RconConfig = config;
        return this;
    }

    public GameServerBuilder WithRconEnabled(bool enabled, int port = 25575, string password = "testpass")
    {
        _server.RconConfig = new RconConfiguration
        {
            Enabled = enabled,
            Port = port,
            Password = password,
            Host = "localhost"
        };
        return this;
    }

    public GameServerBuilder WithProcessId(int? processId)
    {
        _server.ProcessId = processId;
        return this;
    }

    public GameServerBuilder WithLastStarted(DateTime? lastStarted)
    {
        _server.LastStarted = lastStarted;
        return this;
    }

    public GameServerBuilder WithLastStopped(DateTime? lastStopped)
    {
        _server.LastStopped = lastStopped;
        return this;
    }

    public GameServerBuilder AsRunning()
    {
        _server.Status = ServerStatus.Running;
        _server.ProcessId = 12345;
        _server.LastStarted = DateTime.UtcNow.AddMinutes(-10);
        return this;
    }

    public GameServerBuilder AsStopped()
    {
        _server.Status = ServerStatus.Stopped;
        _server.ProcessId = null;
        _server.LastStopped = DateTime.UtcNow.AddMinutes(-5);
        return this;
    }

    public GameServerBuilder AsInstalling()
    {
        _server.Status = ServerStatus.Installing;
        return this;
    }

    public GameServerBuilder AsDocker()
    {
        _server.DeploymentType = ServerDeploymentType.Docker;
        _server.DockerConfig = new DockerConfiguration
        {
            Image = "test-image",
            Tag = "latest"
        };
        _server.NativeConfig = null;
        return this;
    }

    public GameServer Build() => _server;
}
