using NebulaPanel.Infrastructure.Configuration.Parsers;

namespace NebulaPanel.Infrastructure.Tests.Parsers;

public class PropertiesFileParserTests
{
    private readonly PropertiesFileParser _sut = new();

    #region ParseAsync

    [Fact]
    public async Task ParseAsync_WithValidFile_ReturnsKeyValues()
    {
        // Arrange
        var content = """
            server-port=25565
            motd=Welcome to my server
            enable-query=true
            """;

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().ContainKey("server-port");
        result["server-port"].Should().Be("25565");
        result.Should().ContainKey("motd");
        result["motd"].Should().Be("Welcome to my server");
        result.Should().ContainKey("enable-query");
        result["enable-query"].Should().Be("true");
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
    public async Task ParseAsync_WithComments_IgnoresComments()
    {
        // Arrange
        var content = """
            # This is a comment
            server-port=25565
            ! This is also a comment
            motd=Hello
            """;

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey("server-port");
        result.Should().ContainKey("motd");
    }

    [Fact]
    public async Task ParseAsync_WithColonSeparator_ParsesCorrectly()
    {
        // Arrange
        var content = "server-port: 25565";

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().ContainKey("server-port");
        result["server-port"].Should().Be("25565");
    }

    [Fact]
    public async Task ParseAsync_WithEscapedCharacters_UnescapesCorrectly()
    {
        // Arrange
        var content = @"message=Hello\nWorld";

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result["message"].Should().Be("Hello\nWorld");
    }

    [Fact]
    public async Task ParseAsync_WithEmptyValue_ReturnsEmptyString()
    {
        // Arrange
        var content = "empty-value=";

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result.Should().ContainKey("empty-value");
        result["empty-value"].Should().Be(string.Empty);
    }

    [Fact]
    public async Task ParseAsync_WithLineContinuation_JoinsLines()
    {
        // Arrange
        var content = @"long-value=This is a very \
long line that continues";

        // Act
        var result = await _sut.ParseAsync(content);

        // Assert
        result["long-value"].Should().Be("This is a very long line that continues");
    }

    #endregion

    #region SerializeAsync

    [Fact]
    public async Task SerializeAsync_WithValues_WritesCorrectFormat()
    {
        // Arrange
        var values = new Dictionary<string, object?>
        {
            ["server-port"] = 25565,
            ["motd"] = "My Server"
        };

        // Act
        var result = await _sut.SerializeAsync(values);

        // Assert
        result.Should().Contain("server-port=25565");
        result.Should().Contain("motd=My Server");
    }

    [Fact]
    public async Task SerializeAsync_WithExistingContent_PreservesComments()
    {
        // Arrange
        var existingContent = """
            # Server Configuration
            server-port=25565
            # Message of the day
            motd=Old Message
            """;

        var values = new Dictionary<string, object?>
        {
            ["server-port"] = 25565,
            ["motd"] = "New Message"
        };

        // Act
        var result = await _sut.SerializeAsync(values, existingContent);

        // Assert
        result.Should().Contain("# Server Configuration");
        result.Should().Contain("# Message of the day");
        result.Should().Contain("motd=New Message");
    }

    [Fact]
    public async Task SerializeAsync_WithBooleanValue_WritesTrueFalse()
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
        result.Should().Contain("enabled=true");
        result.Should().Contain("disabled=false");
    }

    [Fact]
    public async Task SerializeAsync_WithNullValue_WritesEmptyString()
    {
        // Arrange
        var values = new Dictionary<string, object?>
        {
            ["empty"] = null
        };

        // Act
        var result = await _sut.SerializeAsync(values);

        // Assert
        result.Should().Contain("empty=");
    }

    [Fact]
    public async Task SerializeAsync_WithNewKey_AppendsToEnd()
    {
        // Arrange
        var existingContent = "server-port=25565";
        var values = new Dictionary<string, object?>
        {
            ["server-port"] = 25565,
            ["new-key"] = "new-value"
        };

        // Act
        var result = await _sut.SerializeAsync(values, existingContent);

        // Assert
        result.Should().Contain("new-key=new-value");
    }

    #endregion

    #region GetValue and SetValue

    [Fact]
    public void GetValue_WithExistingKey_ReturnsValue()
    {
        // Arrange
        var data = new Dictionary<string, object?>
        {
            ["test-key"] = "test-value"
        };

        // Act
        var result = _sut.GetValue(data, "test-key");

        // Assert
        result.Should().Be("test-value");
    }

    [Fact]
    public void GetValue_WithNonExistingKey_ReturnsNull()
    {
        // Arrange
        var data = new Dictionary<string, object?>();

        // Act
        var result = _sut.GetValue(data, "non-existing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SetValue_SetsKeyInDictionary()
    {
        // Arrange
        var data = new Dictionary<string, object?>();

        // Act
        _sut.SetValue(data, "new-key", "new-value");

        // Assert
        data.Should().ContainKey("new-key");
        data["new-key"].Should().Be("new-value");
    }

    #endregion
}
