namespace NebulaPanel.Domain.Tests.Entities;

public class GameServerTests
{
    [Fact]
    public void PreferredCommandMethod_WhenRconEnabled_ReturnsRcon()
    {
        // Arrange
        var server = CreateTestServer();
        server.RconConfig = new RconConfiguration
        {
            Enabled = true,
            Port = 25575,
            Password = "testpass",
            Host = "localhost"
        };

        // Act
        var result = server.PreferredCommandMethod;

        // Assert
        result.Should().Be(CommandMethod.Rcon);
    }

    [Fact]
    public void PreferredCommandMethod_WhenRconDisabled_ReturnsStdin()
    {
        // Arrange
        var server = CreateTestServer();
        server.RconConfig = new RconConfiguration
        {
            Enabled = false,
            Port = 25575,
            Password = "testpass",
            Host = "localhost"
        };

        // Act
        var result = server.PreferredCommandMethod;

        // Assert
        result.Should().Be(CommandMethod.Stdin);
    }

    [Fact]
    public void PreferredCommandMethod_WhenRconConfigIsNull_ReturnsStdin()
    {
        // Arrange
        var server = CreateTestServer();
        server.RconConfig = null;

        // Act
        var result = server.PreferredCommandMethod;

        // Assert
        result.Should().Be(CommandMethod.Stdin);
    }

    [Fact]
    public void AdditionalPorts_DefaultsToEmptyDictionary()
    {
        // Arrange
        var server = new GameServer();

        // Assert
        server.AdditionalPorts.Should().NotBeNull();
        server.AdditionalPorts.Should().BeEmpty();
    }

    [Fact]
    public void AdditionalPorts_CanStoreMultiplePorts()
    {
        // Arrange
        var server = CreateTestServer();
        server.AdditionalPorts = new Dictionary<string, int>
        {
            ["rcon"] = 25575,
            ["query"] = 25565,
            ["bedrock"] = 19132
        };

        // Assert
        server.AdditionalPorts.Should().HaveCount(3);
        server.AdditionalPorts["rcon"].Should().Be(25575);
        server.AdditionalPorts["query"].Should().Be(25565);
        server.AdditionalPorts["bedrock"].Should().Be(19132);
    }

    [Fact]
    public void BindAddress_DefaultsToAllInterfaces()
    {
        // Arrange
        var server = new GameServer();

        // Assert
        server.BindAddress.Should().Be("0.0.0.0");
    }

    [Fact]
    public void DeploymentType_CanBeNative()
    {
        // Arrange
        var server = CreateTestServer();
        server.DeploymentType = ServerDeploymentType.Native;

        // Assert
        server.DeploymentType.Should().Be(ServerDeploymentType.Native);
    }

    [Fact]
    public void DeploymentType_CanBeDocker()
    {
        // Arrange
        var server = CreateTestServer();
        server.DeploymentType = ServerDeploymentType.Docker;

        // Assert
        server.DeploymentType.Should().Be(ServerDeploymentType.Docker);
    }

    [Fact]
    public void NativeConfig_CanBeSet()
    {
        // Arrange
        var server = CreateTestServer();
        var config = new NativeConfiguration
        {
            WorkingDirectory = "/path/to/server",
            ExecutablePath = "server.exe",
            Arguments = "-nogui"
        };

        // Act
        server.NativeConfig = config;

        // Assert
        server.NativeConfig.Should().Be(config);
        server.NativeConfig!.ExecutablePath.Should().Be("server.exe");
    }

    [Fact]
    public void DockerConfig_CanBeSet()
    {
        // Arrange
        var server = CreateTestServer();
        var config = new DockerConfiguration
        {
            Image = "itzg/minecraft-server",
            Tag = "latest"
        };

        // Act
        server.DockerConfig = config;

        // Assert
        server.DockerConfig.Should().Be(config);
        server.DockerConfig!.Image.Should().Be("itzg/minecraft-server");
    }

    [Fact]
    public void Status_CanTransitionThroughStates()
    {
        // Arrange
        var server = CreateTestServer();

        // Assert initial state
        server.Status.Should().Be(ServerStatus.Stopped);

        // Act & Assert - starting
        server.Status = ServerStatus.Starting;
        server.Status.Should().Be(ServerStatus.Starting);

        // Act & Assert - running
        server.Status = ServerStatus.Running;
        server.Status.Should().Be(ServerStatus.Running);

        // Act & Assert - stopping
        server.Status = ServerStatus.Stopping;
        server.Status.Should().Be(ServerStatus.Stopping);

        // Act & Assert - stopped
        server.Status = ServerStatus.Stopped;
        server.Status.Should().Be(ServerStatus.Stopped);
    }

    [Fact]
    public void ProcessId_CanBeSetAndCleared()
    {
        // Arrange
        var server = CreateTestServer();

        // Act & Assert - set
        server.ProcessId = 12345;
        server.ProcessId.Should().Be(12345);

        // Act & Assert - clear
        server.ProcessId = null;
        server.ProcessId.Should().BeNull();
    }

    [Fact]
    public void LastStarted_CanBeSet()
    {
        // Arrange
        var server = CreateTestServer();
        var startTime = DateTime.UtcNow;

        // Act
        server.LastStarted = startTime;

        // Assert
        server.LastStarted.Should().Be(startTime);
    }

    [Fact]
    public void LastStopped_CanBeSet()
    {
        // Arrange
        var server = CreateTestServer();
        var stopTime = DateTime.UtcNow;

        // Act
        server.LastStopped = stopTime;

        // Assert
        server.LastStopped.Should().Be(stopTime);
    }

    [Fact]
    public void Collections_AreInitializedAsEmpty()
    {
        // Arrange
        var server = new GameServer();

        // Assert
        server.InstalledMods.Should().NotBeNull().And.BeEmpty();
        server.ScheduledTasks.Should().NotBeNull().And.BeEmpty();
        server.Backups.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ResourceLimits_CanBeSet()
    {
        // Arrange
        var server = CreateTestServer();
        var limits = new ResourceLimits
        {
            MaxMemoryMb = 4096,
            MaxCpuPercent = 80
        };

        // Act
        server.ResourceLimits = limits;

        // Assert
        server.ResourceLimits.Should().NotBeNull();
        server.ResourceLimits!.MaxMemoryMb.Should().Be(4096);
        server.ResourceLimits.MaxCpuPercent.Should().Be(80);
    }

    private static GameServer CreateTestServer()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        return new GameServer
        {
            Id = Guid.NewGuid(),
            Name = "Test Server",
            GameId = gameId,
            Game = new Game
            {
                Id = gameId,
                Name = "Test Game",
                Slug = "test",
                ExecutableType = ExecutableType.Exe,
                DefaultExecutablePath = "test.exe",
                DefaultStartCommand = ""
            },
            DeploymentType = ServerDeploymentType.Native,
            InstallPath = Path.GetTempPath(),
            PrimaryPort = 25565,
            OwnerId = userId,
            Owner = new User
            {
                Id = userId,
                Username = "testuser",
                Email = "test@example.com",
                PasswordHash = "hashedpassword"
            },
            Status = ServerStatus.Stopped
        };
    }
}
