using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NebulaPanel.Infrastructure.Persistence;

public static class DatabaseConfiguration
{
    public static IServiceCollection AddNebulaDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbConfig = configuration.GetSection("Database");
        var provider = dbConfig["Provider"] ?? "SQLite";
        var connectionString = dbConfig["ConnectionString"] ?? "Data Source=data/nebula.db";

        services.AddDbContext<NebulaPanelDbContext>(options =>
        {
            ConfigureProvider(options, provider, connectionString);

#if DEBUG
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
#endif
        });

        return services;
    }

    private static void ConfigureProvider(
        DbContextOptionsBuilder options,
        string provider,
        string connectionString)
    {
        switch (provider.ToLowerInvariant())
        {
            case "sqlite":
                ConfigureSqlite(options, connectionString);
                break;
            case "postgresql":
            case "postgres":
                ConfigurePostgres(options, connectionString);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported database provider: {provider}. " +
                    "Supported providers: SQLite, PostgreSQL");
        }
    }

    private static DbContextOptionsBuilder ConfigureSqlite(
        DbContextOptionsBuilder options,
        string connectionString)
    {
        return options.UseSqlite(connectionString, sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly("NebulaPanel.Infrastructure");
            sqliteOptions.CommandTimeout(30);
        });
    }

    private static DbContextOptionsBuilder ConfigurePostgres(
        DbContextOptionsBuilder options,
        string connectionString)
    {
        return options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("NebulaPanel.Infrastructure");
            npgsqlOptions.CommandTimeout(30);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });
    }
}
