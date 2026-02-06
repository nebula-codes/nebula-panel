using System.Text;
using System.Text.Json;
using NebulaPanel.Infrastructure.Persistence;
using NebulaPanel.Integration.Tests.Fixtures;

namespace NebulaPanel.Integration.Tests.Controllers;

[Collection("Integration")]
public class GameServersControllerTests
{
    private readonly NebulaPanelWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GameServersControllerTests(NebulaPanelWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task<(AuthenticatedClientFixture Auth, Guid GameId, string Email)> SetupTestContextAsync()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var email = $"server_{uniqueId}@example.com";

        // Seed a test game
        var gameId = await SeedTestGameAsync(uniqueId);

        // Create authenticated client
        var authFixture = new AuthenticatedClientFixture(_factory);
        await authFixture.RegisterAndAuthenticateAsync(
            username: $"serveruser_{uniqueId}",
            email: email,
            password: "Password123!");

        return (authFixture, gameId, email);
    }

    private async Task<Guid> SeedTestGameAsync(string uniqueId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = $"Test Game {uniqueId}",
            Slug = $"test-game-{uniqueId}",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "test.exe",
            DefaultStartCommand = "",
            SupportsDocker = true,
            IsEnabled = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync();

        return game.Id;
    }

    [Fact]
    public async Task GetAll_WithAuthenticatedUser_ReturnsServers()
    {
        // Arrange
        var (auth, _, _) = await SetupTestContextAsync();

        // Act
        var response = await auth.Client.GetAsync("/api/gameservers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/gameservers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_WithValidId_ReturnsServer()
    {
        // Arrange
        var (auth, gameId, email) = await SetupTestContextAsync();
        var serverId = await CreateTestServerAsync(gameId, email);

        // Act
        var response = await auth.Client.GetAsync($"/api/gameservers/{serverId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Test Server");
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var (auth, _, _) = await SetupTestContextAsync();

        // Act
        var response = await auth.Client.GetAsync($"/api/gameservers/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var (auth, gameId, _) = await SetupTestContextAsync();
        var request = new
        {
            Name = $"Created Server {Guid.NewGuid():N}",
            GameId = gameId,
            DeploymentType = 1, // Native (Docker=0, Native=1)
            InstallPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            PrimaryPort = 25567 + Random.Shared.Next(1000),
            BindAddress = "0.0.0.0",
            NativeConfig = new
            {
                WorkingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
                ExecutablePath = "test.exe",
                Arguments = "",
                RunAsService = false
            }
        };

        // Act
        var response = await auth.Client.PostAsync(
            "/api/gameservers",
            CreateJsonContent(request));

        // Assert - Include error details in failure message
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"Response: {content}");

        content.Should().Contain("Created Server");
    }

    [Fact]
    public async Task Create_WithInvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var (auth, _, _) = await SetupTestContextAsync();
        var request = new
        {
            Name = ""
        };

        // Act
        var response = await auth.Client.PostAsync(
            "/api/gameservers",
            CreateJsonContent(request));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithNonExistentGame_ReturnsNotFound()
    {
        // Arrange
        var (auth, _, _) = await SetupTestContextAsync();
        var request = new
        {
            Name = "Invalid Game Server",
            GameId = Guid.NewGuid(), // Non-existent game
            DeploymentType = 0,
            InstallPath = "/tmp/test",
            PrimaryPort = 25568 + Random.Shared.Next(1000),
            BindAddress = "0.0.0.0"
        };

        // Act
        var response = await auth.Client.PostAsync(
            "/api/gameservers",
            CreateJsonContent(request));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var (auth, gameId, email) = await SetupTestContextAsync();
        var serverId = await CreateTestServerAsync(gameId, email);

        var updateRequest = new
        {
            Name = "Updated Server Name",
            Description = "Updated description",
            InstallPath = Path.Combine(Path.GetTempPath(), "updated-server"),
            PrimaryPort = 25570,
            BindAddress = "0.0.0.0"
        };

        // Act
        var response = await auth.Client.PutAsync(
            $"/api/gameservers/{serverId}",
            CreateJsonContent(updateRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Updated Server Name");
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var (auth, _, _) = await SetupTestContextAsync();
        var updateRequest = new
        {
            Name = "Updated Name",
            InstallPath = Path.Combine(Path.GetTempPath(), "nonexistent"),
            PrimaryPort = 25571,
            BindAddress = "0.0.0.0"
        };

        // Act
        var response = await auth.Client.PutAsync(
            $"/api/gameservers/{Guid.NewGuid()}",
            CreateJsonContent(updateRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var (auth, gameId, email) = await SetupTestContextAsync();
        var serverId = await CreateTestServerAsync(gameId, email);

        // Act
        var response = await auth.Client.DeleteAsync($"/api/gameservers/{serverId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deleted
        var getResponse = await auth.Client.GetAsync($"/api/gameservers/{serverId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var (auth, _, _) = await SetupTestContextAsync();

        // Act
        var response = await auth.Client.DeleteAsync($"/api/gameservers/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyServers_ReturnsOnlyOwnedServers()
    {
        // Arrange
        var (auth, gameId, _) = await SetupTestContextAsync();
        await CreateTestServerViaApiAsync(auth, gameId);

        // Act
        var response = await auth.Client.GetAsync("/api/gameservers/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Test Server");
    }

    private async Task<Guid> CreateTestServerViaApiAsync(AuthenticatedClientFixture auth, Guid gameId)
    {
        var request = new
        {
            Name = "Test Server",
            GameId = gameId,
            DeploymentType = 1, // Native (Docker=0, Native=1)
            InstallPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            PrimaryPort = 25565 + Random.Shared.Next(10000),
            BindAddress = "0.0.0.0",
            NativeConfig = new
            {
                WorkingDirectory = Path.GetTempPath(),
                ExecutablePath = "test.exe",
                Arguments = "",
                RunAsService = false
            }
        };

        var response = await auth.Client.PostAsync(
            "/api/gameservers",
            CreateJsonContent(request));

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateTestServerAsync(Guid gameId, string ownerEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();

        // Get the authenticated user
        var user = await context.Users.FirstAsync(u => u.Email == ownerEmail);

        var server = new GameServer
        {
            Id = Guid.NewGuid(),
            Name = "Test Server",
            GameId = gameId,
            OwnerId = user.Id,
            DeploymentType = ServerDeploymentType.Native,
            InstallPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            PrimaryPort = 25565 + Random.Shared.Next(10000),
            BindAddress = "0.0.0.0",
            Status = ServerStatus.Stopped,
            NativeConfig = new NativeConfiguration
            {
                WorkingDirectory = Path.GetTempPath(),
                ExecutablePath = "test.exe",
                Arguments = ""
            }
        };

        context.GameServers.Add(server);
        await context.SaveChangesAsync();

        return server.Id;
    }

    private static StringContent CreateJsonContent(object obj)
    {
        return new StringContent(
            JsonSerializer.Serialize(obj),
            Encoding.UTF8,
            "application/json");
    }
}
