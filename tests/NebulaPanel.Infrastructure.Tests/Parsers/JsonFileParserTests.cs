using NebulaPanel.Infrastructure.Configuration.Parsers;

namespace NebulaPanel.Infrastructure.Tests.Parsers;

public class JsonFileParserTests
{
    private readonly JsonFileParser _sut = new();

    #region ParseAsync

    [Fact]
    public async Task ParseAsync_WithValidJson_ReturnsValues()
    {
        // Arrange
        var content = """
            {
                "port": 25565,
                "name": "My Server",
                "enabled": true
            }
            """;

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().ContainKey("port");
        result["port"].Should().Be(25565);
        result.Should().ContainKey("name");
        result["name"].Should().Be("My Server");
        result.Should().ContainKey("enabled");
        result["enabled"].Should().Be(true);
    }

    [Fact]
    public async Task ParseAsync_WithNestedJson_FlattensWithDotNotation()
    {
        // Arrange
        var content = """
            {
                "server": {
                    "network": {
                        "port": 25565
                    }
                }
            }
            """;

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().ContainKey("server.network.port");
        result["server.network.port"].Should().Be(25565);
    }

    [Fact]
    public async Task ParseAsync_WithEmptyContent_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _sut.ParseAsync(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithNullValue_ReturnsNull()
    {
        // Arrange
        var content = """
            {
                "value": null
            }
            """;

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().ContainKey("value");
        result["value"].Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_WithArray_ReturnsAsList()
    {
        // Arrange
        var content = """
            {
                "items": [1, 2, 3]
            }
            """;

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().ContainKey("items");
        result["items"].Should().BeOfType<List<object?>>();
        ((List<object?>)result["items"]!).Should().BeEquivalentTo(new List<int> { 1, 2, 3 });
    }

    [Fact]
    public async Task ParseAsync_WithComments_IgnoresComments()
    {
        // Arrange - JSON5 style comments are skipped
        var content = """
            {
                // This is a comment
                "port": 25565
            }
            """;

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().ContainKey("port");
    }

    [Fact]
    public async Task ParseAsync_WithTrailingComma_ParsesSuccessfully()
    {
        // Arrange
        var content = """
            {
                "port": 25565,
            }
            """;

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().ContainKey("port");
    }

    [Fact]
    public async Task ParseAsync_WithInvalidJson_ReturnsEmptyDictionary()
    {
        // Arrange
        var content = "{ invalid json }";

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithDifferentNumberTypes_ParsesCorrectly()
    {
        // Arrange
        var content = """
            {
                "intValue": 42,
                "doubleValue": 3.14,
                "bigValue": 9999999999
            }
            """;

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result["intValue"].Should().Be(42);
        result["doubleValue"].Should().Be(3.14);
        result["bigValue"].Should().Be(9999999999L);
    }

    #endregion

    #region SerializeAsync

    [Fact]
    public async Task SerializeAsync_WithValues_WritesValidJson()
    {
        // Arrange
        var values = new Dictionary<string, object?>
        {
            ["port"] = 25565,
            ["name"] = "My Server"
        };

        // Act
        var result = await _sut.SerializeAsync(values);

        // Assert
        result.Should().Contain("\"port\": 25565");
        result.Should().Contain("\"name\": \"My Server\"");
    }

    [Fact]
    public async Task SerializeAsync_WithNestedPath_CreatesNestedStructure()
    {
        // Arrange
        var values = new Dictionary<string, object?>
        {
            ["server.network.port"] = 25565
        };

        // Act
        var result = await _sut.SerializeAsync(values);

        // Assert
        result.Should().Contain("\"server\"");
        result.Should().Contain("\"network\"");
        result.Should().Contain("\"port\": 25565");
    }

    [Fact]
    public async Task SerializeAsync_WithExistingContent_PreservesStructure()
    {
        // Arrange
        var existingContent = """
            {
                "existingKey": "existingValue",
                "port": 25565
            }
            """;

        var values = new Dictionary<string, object?>
        {
            ["port"] = 25566,
            ["existingKey"] = "existingValue"
        };

        // Act
        var result = await _sut.SerializeAsync(values, existingContent);

        // Assert
        result.Should().Contain("\"port\": 25566");
        result.Should().Contain("\"existingKey\": \"existingValue\"");
    }

    [Fact]
    public async Task SerializeAsync_WithBooleanValue_WritesCorrectly()
    {
        // Arrange
        var values = new Dictionary<string, object?>
        {
            ["enabled"] = true,
            ["disabled"] = false
        };

        // Act
        var result = await _sut.SerializeAsync(values);

        // Assert
        result.Should().Contain("\"enabled\": true");
        result.Should().Contain("\"disabled\": false");
    }

    [Fact]
    public async Task SerializeAsync_WithNullValue_WritesNull()
    {
        // Arrange
        var values = new Dictionary<string, object?>
        {
            ["nullValue"] = null
        };

        // Act
        var result = await _sut.SerializeAsync(values);

        // Assert
        result.Should().Contain("\"nullValue\": null");
    }

    #endregion

    #region GetValue and SetValue

    [Fact]
    public void GetValue_WithExistingKey_ReturnsValue()
    {
        // Arrange
        var data = new Dictionary<string, object?>
        {
            ["server.port"] = 25565
        };

        // Act
        var result = _sut.GetValue(data, "server.port");

        // Assert
        result.Should().Be(25565);
    }

    [Fact]
    public void GetValue_WithJsonPath_UsesJsonPath()
    {
        // Arrange
        var data = new Dictionary<string, object?>
        {
            ["server.network.port"] = 25565
        };

        // Act
        var result = _sut.GetValue(data, "port", "server.network.port");

        // Assert
        result.Should().Be(25565);
    }

    [Fact]
    public void SetValue_WithJsonPath_SetsAtPath()
    {
        // Arrange
        var data = new Dictionary<string, object?>();

        // Act
        _sut.SetValue(data, "port", 25565, "server.network.port");

        // Assert
        data.Should().ContainKey("server.network.port");
        data["server.network.port"].Should().Be(25565);
    }

    #endregion
}
