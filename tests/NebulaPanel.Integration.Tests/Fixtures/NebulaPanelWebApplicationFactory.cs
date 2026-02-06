using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NebulaPanel.Infrastructure.Persistence;
using static NebulaPanel.Infrastructure.Persistence.DataSeeder;

namespace NebulaPanel.Integration.Tests.Fixtures;

/// <summary>
/// WebApplicationFactory for integration tests with in-memory SQLite database.
/// </summary>
public class NebulaPanelWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (!_initialized)
            {
                // Create and open the SQLite connection before starting the server
                _connection = new SqliteConnection("DataSource=:memory:");
                await _connection.OpenAsync();
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public new async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Ensure initialization has happened
        InitializeAsync().GetAwaiter().GetResult();

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<NebulaPanelDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove the existing NebulaPanelDbContext registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(NebulaPanelDbContext));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Add in-memory SQLite DbContext
            services.AddDbContext<NebulaPanelDbContext>(options =>
            {
                if (_connection != null)
                {
                    options.UseSqlite(_connection);
                }
                else
                {
                    options.UseSqlite("DataSource=:memory:");
                }
            });

            // Build service provider to create database and seed data
            var sp = services.BuildServiceProvider();

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();
            db.Database.EnsureCreated();

            // Seed roles, permissions, and default admin user
            DataSeeder.SeedDataAsync(sp).GetAwaiter().GetResult();
        });
    }

    /// <summary>
    /// Seeds a test user and returns their credentials.
    /// </summary>
    public async Task<(Guid UserId, string Username, string Email)> SeedTestUserAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return (user.Id, user.Username, user.Email);
    }

    /// <summary>
    /// Seeds a test game and returns its ID.
    /// </summary>
    public async Task<Guid> SeedTestGameAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = "Test Game",
            Slug = "test-game",
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
}
