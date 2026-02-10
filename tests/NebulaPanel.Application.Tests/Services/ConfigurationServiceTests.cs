using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Tests.Builders;
using NebulaPanel.Domain.Interfaces.Configuration;

namespace NebulaPanel.Application.Tests.Services;

public class ConfigurationServiceTests
{
    private readonly Mock<IGameServerRepository> _serverRepositoryMock;
    private readonly Mock<IServerFileManager> _fileManagerMock;
    private readonly Mock<IConfigParserFactory> _parserFactoryMock;
    private readonly Mock<IConfigFileParser> _parserMock;
    private readonly Mock<ILogger<ConfigurationService>> _loggerMock;
    private readonly ConfigurationService _sut;

    public ConfigurationServiceTests()
    {
        _serverRepositoryMock = new Mock<IGameServerRepository>();
        _fileManagerMock = new Mock<IServerFileManager>();
        _parserFactoryMock = new Mock<IConfigParserFactory>();
        _parserMock = new Mock<IConfigFileParser>();
        _loggerMock = new Mock<ILogger<ConfigurationService>>();

        _sut = new ConfigurationService(
            _serverRepositoryMock.Object,
            _fileManagerMock.Object,
            _parserFactoryMock.Object,
            _loggerMock.Object);
    }

    #region ReadConfigAsync

    [Fact]
    public async Task ReadConfigAsync_WhenFileExists_ReturnsValues()
    {
        // Arrange
        var server = CreateServerWithConfigSchema();
        var expectedValues = new Dictionary<string, object?>
        {
            ["server-port"] = 25565,
            ["motd"] = "Welcome to my server"
        };

        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);
        _fileManagerMock.Setup(m => m.ExistsAsync(server, "server.properties", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _fileManagerMock.Setup(m => m.ReadFileAsync(server, "server.properties", It.IsAny<CancellationToken>()))
            .ReturnsAsync("server-port=25565\nmotd=Welcome to my server");
        _parserFactoryMock.Setup(f => f.GetParserOrDefault(ConfigFileType.Properties))
            .Returns(_parserMock.Object);
        _parserMock.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedValues);

        // Act
        var result = await _sut.ReadConfigAsync(server.Id, "server.properties");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Values.Should().ContainKey("server-port");
    }

    [Fact]
    public async Task ReadConfigAsync_WhenServerNotFound_ReturnsFailure()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameServer?)null);

        // Act
        var result = await _sut.ReadConfigAsync(serverId, "server.properties");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ReadConfigAsync_WhenNoSchemaForFile_ReturnsFailure()
    {
        // Arrange
        var server = CreateServerWithConfigSchema();

        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        // Act
        var result = await _sut.ReadConfigAsync(server.Id, "nonexistent.config");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ReadConfigAsync_WhenFileDoesNotExist_ReturnsDefaultValues()
    {
        // Arrange
        var server = CreateServerWithConfigSchema();

        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);
        _fileManagerMock.Setup(m => m.ExistsAsync(server, "server.properties", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _parserFactoryMock.Setup(f => f.GetParserOrDefault(ConfigFileType.Properties))
            .Returns(_parserMock.Object);

        // Act
        var result = await _sut.ReadConfigAsync(server.Id, "server.properties");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Values.Should().ContainKey("server-port");
        result.Value.Values["server-port"].Should().Be(25565); // Default value
    }

    #endregion

    #region WriteConfigAsync

    [Fact]
    public async Task WriteConfigAsync_WithValidValues_WritesFile()
    {
        // Arrange
        var server = CreateServerWithConfigSchema();
        var values = new Dictionary<string, object?>
        {
            ["server-port"] = 25566,
            ["motd"] = "Updated message"
        };
        var request = new UpdateConfigRequest("server.properties", values);

        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);
        _parserFactoryMock.Setup(f => f.GetParserOrDefault(ConfigFileType.Properties))
            .Returns(_parserMock.Object);
        _fileManagerMock.Setup(m => m.ExistsAsync(server, "server.properties", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _fileManagerMock.Setup(m => m.ReadFileAsync(server, "server.properties", It.IsAny<CancellationToken>()))
            .ReturnsAsync("server-port=25565");
        _parserMock.Setup(p => p.SerializeAsync(
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<string?>(),
                It.IsAny<ConfigurationSchema?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("server-port=25566\nmotd=Updated message");

        // Act
        var result = await _sut.WriteConfigAsync(server.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fileManagerMock.Verify(
            m => m.WriteFileAsync(server, "server.properties", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WriteConfigAsync_WithInvalidPort_ReturnsValidationError()
    {
        // Arrange
        var server = CreateServerWithConfigSchema();
        var values = new Dictionary<string, object?>
        {
            ["server-port"] = 99999 // Invalid port
        };
        var request = new UpdateConfigRequest("server.properties", values);

        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        // Act
        var result = await _sut.WriteConfigAsync(server.Id, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Validation failed");
    }

    [Fact]
    public async Task WriteConfigAsync_WithRequiredFieldMissing_ReturnsError()
    {
        // Arrange
        var server = CreateServerWithRequiredFieldSchema();
        var values = new Dictionary<string, object?>
        {
            // Missing required "server-port" field
            ["motd"] = "Test"
        };
        var request = new UpdateConfigRequest("server.properties", values);

        _serverRepositoryMock.Setup(r => r.GetByIdWithGameAsync(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        // Act
        var result = await _sut.WriteConfigAsync(server.Id, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Validation failed");
    }

    #endregion

    #region ValidateConfig

    [Fact]
    public void ValidateConfig_WithValidPort_ReturnsSuccess()
    {
        // Arrange
        var schema = CreateBasicSchema();
        var values = new Dictionary<string, object?>
        {
            ["server-port"] = 25565
        };

        // Act
        var result = _sut.ValidateConfig(schema, values);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateConfig_WithInvalidPort_ReturnsError()
    {
        // Arrange
        var schema = CreateBasicSchema();
        var values = new Dictionary<string, object?>
        {
            ["server-port"] = -1
        };

        // Act
        var result = _sut.ValidateConfig(schema, values);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FieldErrors.Should().ContainKey("server-port");
    }

    [Fact]
    public void ValidateConfig_WithRequiredFieldMissing_ReturnsError()
    {
        // Arrange
        var schema = CreateRequiredFieldSchema();
        var values = new Dictionary<string, object?>(); // Empty values

        // Act
        var result = _sut.ValidateConfig(schema, values);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FieldErrors.Should().ContainKey("server-port");
    }

    #endregion

    #region Helpers

    private static GameServer CreateServerWithConfigSchema()
    {
        var game = new GameBuilder()
            .AsMinecraft()
            .Build();

        game.ConfigurationSchemas = new Dictionary<string, ConfigurationSchema>
        {
            ["server.properties"] = new ConfigurationSchema
            {
                DisplayName = "Server Properties",
                FileType = ConfigFileType.Properties,
                Fields =
                [
                    new ConfigField
                    {
                        Key = "server-port",
                        DisplayName = "Server Port",
                        Type = ConfigFieldType.Port,
                        DefaultValue = 25565,
                        Validation = new ValidationRule
                        {
                            MinValue = 1,
                            MaxValue = 65535
                        }
                    },
                    new ConfigField
                    {
                        Key = "motd",
                        DisplayName = "Message of the Day",
                        Type = ConfigFieldType.String,
                        DefaultValue = "A Minecraft Server"
                    }
                ]
            }
        };

        return new GameServerBuilder()
            .WithGame(game)
            .Build();
    }

    private static GameServer CreateServerWithRequiredFieldSchema()
    {
        var game = new GameBuilder()
            .AsMinecraft()
            .Build();

        game.ConfigurationSchemas = new Dictionary<string, ConfigurationSchema>
        {
            ["server.properties"] = new ConfigurationSchema
            {
                DisplayName = "Server Properties",
                FileType = ConfigFileType.Properties,
                Fields =
                [
                    new ConfigField
                    {
                        Key = "server-port",
                        DisplayName = "Server Port",
                        Type = ConfigFieldType.Port,
                        Required = true,
                        Validation = new ValidationRule
                        {
                            MinValue = 1,
                            MaxValue = 65535
                        }
                    }
                ]
            }
        };

        return new GameServerBuilder()
            .WithGame(game)
            .Build();
    }

    private static ConfigurationSchema CreateBasicSchema()
    {
        return new ConfigurationSchema
        {
            DisplayName = "Server Properties",
            FileType = ConfigFileType.Properties,
            Fields =
            [
                new ConfigField
                {
                    Key = "server-port",
                    DisplayName = "Server Port",
                    Type = ConfigFieldType.Port,
                    Validation = new ValidationRule
                    {
                        MinValue = 1,
                        MaxValue = 65535
                    }
                }
            ]
        };
    }

    private static ConfigurationSchema CreateRequiredFieldSchema()
    {
        return new ConfigurationSchema
        {
            DisplayName = "Server Properties",
            FileType = ConfigFileType.Properties,
            Fields =
            [
                new ConfigField
                {
                    Key = "server-port",
                    DisplayName = "Server Port",
                    Type = ConfigFieldType.Port,
                    Required = true
                }
            ]
        };
    }

    #endregion
}
