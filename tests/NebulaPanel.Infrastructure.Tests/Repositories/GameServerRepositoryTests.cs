using NebulaPanel.Infrastructure.Repositories;
using NebulaPanel.Infrastructure.Tests.Fixtures;

namespace NebulaPanel.Infrastructure.Tests.Repositories;

public class GameServerRepositoryTests : IClassFixture<InMemorySqliteFixture>, IDisposable
{
    private readonly InMemorySqliteFixture _fixture;
    private readonly GameServerRepository _sut;

    public GameServerRepositoryTests()
    {
        // Create a new fixture for each test to ensure isolation
        _fixture = new InMemorySqliteFixture();
        _sut = new GameServerRepository(_fixture.Context);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllServers()
    {
        // Arrange
        await _fixture.SeedGameServerAsync();
        await _fixture.SeedGameServerAsync(name: "Server 2");

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsServer()
    {
        // Arrange
        var server = await _fixture.SeedGameServerAsync();

        // Act
        var result = await _sut.GetByIdAsync(server.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(server.Id);
        result.Name.Should().Be(server.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        await _fixture.SeedGameServerAsync();

        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithGameAsync_ReturnsServerWithGameIncluded()
    {
        // Arrange
        var server = await _fixture.SeedGameServerAsync();

        // Act
        var result = await _sut.GetByIdWithGameAsync(server.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Game.Should().NotBeNull();
        result.Owner.Should().NotBeNull();
    }

    [Fact]
    public async Task NameExistsForOwnerAsync_WithExistingName_ReturnsTrue()
    {
        // Arrange
        var server = await _fixture.SeedGameServerAsync(name: "Unique Server");

        // Act
        var result = await _sut.NameExistsForOwnerAsync("Unique Server", server.OwnerId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsForOwnerAsync_WithNonExistingName_ReturnsFalse()
    {
        // Arrange
        var owner = await _fixture.SeedUserAsync();

        // Act
        var result = await _sut.NameExistsForOwnerAsync("Non-existent Server", owner.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsForOwnerAsync_WithExcludedId_ReturnsFalse()
    {
        // Arrange
        var server = await _fixture.SeedGameServerAsync(name: "My Server");

        // Act - excluding the server itself should return false
        var result = await _sut.NameExistsForOwnerAsync("My Server", server.OwnerId, server.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsForOwnerAsync_DifferentOwner_ReturnsFalse()
    {
        // Arrange
        await _fixture.SeedGameServerAsync(name: "Shared Name");
        var differentOwner = await _fixture.SeedUserAsync(username: "otheruser", email: "other@test.com");

        // Act
        var result = await _sut.NameExistsForOwnerAsync("Shared Name", differentOwner.Id);

        // Assert
        result.Should().BeFalse(); // Different owners can have same server name
    }

    [Fact]
    public async Task IsPortInUseAsync_WithExistingPort_ReturnsTrue()
    {
        // Arrange
        var server = await _fixture.SeedGameServerAsync();

        // Act
        var result = await _sut.IsPortInUseAsync(server.PrimaryPort, "0.0.0.0");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPortInUseAsync_WithNonExistingPort_ReturnsFalse()
    {
        // Act
        var result = await _sut.IsPortInUseAsync(99999, "0.0.0.0");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPortInUseAsync_WithExcludedId_ReturnsFalse()
    {
        // Arrange
        var server = await _fixture.SeedGameServerAsync();

        // Act - excluding the server itself should return false
        var result = await _sut.IsPortInUseAsync(server.PrimaryPort, "0.0.0.0", server.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPortInUseAsync_DifferentBindAddress_ReturnsFalse()
    {
        // Arrange
        var server = await _fixture.SeedGameServerAsync();

        // Act - different bind address should be allowed
        var result = await _sut.IsPortInUseAsync(server.PrimaryPort, "127.0.0.1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_WithValidServer_PersistsServer()
    {
        // Arrange
        var owner = await _fixture.SeedUserAsync();
        var game = await _fixture.SeedGameAsync();

        var server = new GameServer
        {
            Id = Guid.NewGuid(),
            Name = "New Server",
            GameId = game.Id,
            OwnerId = owner.Id,
            DeploymentType = ServerDeploymentType.Native,
            InstallPath = "/path/to/server",
            PrimaryPort = 25565,
            BindAddress = "0.0.0.0",
            Status = ServerStatus.Stopped
        };

        // Act
        var result = await _sut.AddAsync(server);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(server.Id);

        // Verify it was persisted
        var retrieved = await _sut.GetByIdAsync(server.Id);
        retrieved.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithValidServer_UpdatesServer()
    {
        // Arrange
        var server = await _fixture.SeedGameServerAsync();
        var newName = "Updated Server Name";
        server.Name = newName;

        // Act
        await _sut.UpdateAsync(server);

        // Assert
        var retrieved = await _sut.GetByIdAsync(server.Id);
        retrieved!.Name.Should().Be(newName);
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_RemovesServer()
    {
        // Arrange
        var server = await _fixture.SeedGameServerAsync();

        // Act
        await _sut.DeleteAsync(server.Id);

        // Assert
        var retrieved = await _sut.GetByIdAsync(server.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_DoesNotThrow()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act & Assert - should not throw
        var act = async () => await _sut.DeleteAsync(invalidId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingServer_ReturnsTrue()
    {
        // Arrange
        var server = await _fixture.SeedGameServerAsync();

        // Act
        var result = await _sut.ExistsAsync(server.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingServer_ReturnsFalse()
    {
        // Act
        var result = await _sut.ExistsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByOwnerIdAsync_ReturnsServersForOwner()
    {
        // Arrange
        var owner = await _fixture.SeedUserAsync();
        var game = await _fixture.SeedGameAsync();
        await _fixture.SeedGameServerAsync(owner, game, "Server 1");
        await _fixture.SeedGameServerAsync(owner, game, "Server 2");

        // Create another user's server
        var otherOwner = await _fixture.SeedUserAsync(username: "other", email: "other@test.com");
        await _fixture.SeedGameServerAsync(otherOwner, game, "Other's Server");

        // Act
        var result = await _sut.GetByOwnerIdAsync(owner.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(s => s.OwnerId.Should().Be(owner.Id));
    }
}
