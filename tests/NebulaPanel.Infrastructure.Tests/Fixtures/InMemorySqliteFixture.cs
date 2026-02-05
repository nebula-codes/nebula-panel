using Microsoft.Data.Sqlite;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Tests.Fixtures;

/// <summary>
/// Provides an in-memory SQLite database for testing repository operations.
/// </summary>
public class InMemorySqliteFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    public NebulaPanelDbContext Context { get; }

    public InMemorySqliteFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<NebulaPanelDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new NebulaPanelDbContext(options);
        Context.Database.EnsureCreated();
    }

    /// <summary>
    /// Creates a new DbContext using the same connection (for testing concurrent access).
    /// </summary>
    public NebulaPanelDbContext CreateNewContext()
    {
        var options = new DbContextOptionsBuilder<NebulaPanelDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new NebulaPanelDbContext(options);
    }

    /// <summary>
    /// Seeds a default user for testing.
    /// </summary>
    public async Task<User> SeedUserAsync(string? username = null, string? email = null)
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username ?? $"testuser_{uniqueId}",
            Email = email ?? $"test_{uniqueId}@example.com",
            PasswordHash = "hashedpassword",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Seeds a default game for testing.
    /// </summary>
    public async Task<Game> SeedGameAsync(string? name = null, string? slug = null)
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = name ?? $"Test Game {uniqueId}",
            Slug = slug ?? $"test-game-{uniqueId}",
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "test.exe",
            DefaultStartCommand = "",
            SupportsDocker = true,
            IsEnabled = true
        };

        Context.Games.Add(game);
        await Context.SaveChangesAsync();
        return game;
    }

    /// <summary>
    /// Seeds a game server for testing.
    /// </summary>
    public async Task<GameServer> SeedGameServerAsync(User? owner = null, Game? game = null, string name = "Test Server")
    {
        owner ??= await SeedUserAsync();
        game ??= await SeedGameAsync();

        var server = new GameServer
        {
            Id = Guid.NewGuid(),
            Name = name,
            GameId = game.Id,
            OwnerId = owner.Id,
            DeploymentType = ServerDeploymentType.Native,
            InstallPath = Path.Combine(Path.GetTempPath(), "test-server"),
            PrimaryPort = 25565 + Context.GameServers.Count(),
            BindAddress = "0.0.0.0",
            Status = ServerStatus.Stopped,
            NativeConfig = new NativeConfiguration
            {
                WorkingDirectory = Path.Combine(Path.GetTempPath(), "test-server"),
                ExecutablePath = "test.exe",
                Arguments = ""
            }
        };

        Context.GameServers.Add(server);
        await Context.SaveChangesAsync();
        return server;
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
