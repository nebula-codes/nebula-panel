using System.Text;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using NebulaPanel.Web.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NebulaPanel.Application;
using NebulaPanel.Application.Services;
using NebulaPanel.Infrastructure;
using NebulaPanel.Infrastructure.Auth;
using NebulaPanel.Infrastructure.Configuration;
using NebulaPanel.Infrastructure.Persistence;
using static NebulaPanel.Infrastructure.Persistence.DataSeeder;
using static NebulaPanel.Infrastructure.Persistence.OfficialGameSeeder;
using NebulaPanel.Infrastructure.Services;
using NebulaPanel.Infrastructure.OfficialGames.Hytale;
using NebulaPanel.Infrastructure.FileManagement;
using NebulaPanel.Domain.Settings;
using NebulaPanel.Web.Authorization;
using NebulaPanel.Web.Components;
using NebulaPanel.Web.Hubs;
using NebulaPanel.Web.Middleware;
using NebulaPanel.Web.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NebulaPanel.Infrastructure.Health;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure listen URLs from appsettings.json
// Development uses localhost:5000, production/Docker uses 0.0.0.0:5000
var urls = builder.Configuration["Urls"];
if (!string.IsNullOrEmpty(urls))
{
    builder.WebHost.UseUrls(urls.Split(';'));
}

// Validate JWT secret is configured in production
if (builder.Environment.IsProduction())
{
    var jwtSecret = builder.Configuration["Jwt:Secret"];
    if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Contains("CHANGE_THIS"))
    {
        throw new InvalidOperationException(
            "JWT secret must be configured for production. " +
            "Set the Jwt:Secret configuration value or JWT__SECRET environment variable.");
    }
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Persist DataProtection keys in the database so they are shared across nodes
// and survive container restarts without needing a persistent filesystem mount.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<NebulaPanelDbContext>()
    .SetApplicationName("NebulaPanel");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add API controllers
builder.Services.AddControllers();

// Add HTTP context accessor for authorization handlers
builder.Services.AddHttpContextAccessor();

// Add HttpClient for Blazor components to call local API
builder.Services.AddScoped(sp =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var request = httpContextAccessor.HttpContext?.Request;
    var baseUri = request is not null
        ? $"{request.Scheme}://{request.Host}"
        : "https://localhost";
    return new HttpClient { BaseAddress = new Uri(baseUri) };
});
builder.Services.AddScoped<AuthenticatedHttpClient>();

// Configure JWT settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

// Configure rate limiting and account lockout settings
builder.Services.Configure<RateLimitSettings>(builder.Configuration.GetSection(RateLimitSettings.SectionName));
builder.Services.Configure<AccountLockoutSettings>(builder.Configuration.GetSection(AccountLockoutSettings.SectionName));

// Configure CurseForge settings
builder.Services.Configure<CurseForgeSettings>(builder.Configuration.GetSection(CurseForgeSettings.SectionName));

// Configure Steam API settings
builder.Services.Configure<SteamApiSettings>(builder.Configuration.GetSection(SteamApiSettings.SectionName));

// Configure Modtale settings
builder.Services.Configure<ModtaleSettings>(builder.Configuration.GetSection(ModtaleSettings.SectionName));

// Configure Backup settings
builder.Services.Configure<BackupSettings>(builder.Configuration.GetSection(BackupSettings.SectionName));
builder.Services.AddScoped<IBackupSettings>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BackupSettings>>().Value);

// Configure Update settings
builder.Services.Configure<UpdateConfiguration>(builder.Configuration.GetSection(UpdateConfiguration.SectionName));

// Configure Hytale auth settings
builder.Services.Configure<HytaleAuthSettings>(builder.Configuration.GetSection(HytaleAuthSettings.SectionName));

// Add GitHub HTTP client for update checking
builder.Services.AddHttpClient("GitHub", client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Add authentication
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ClockSkew = TimeSpan.Zero
    };

    // Support JWT in SignalR and handle browser navigation to protected pages
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("JwtAuthFailed");
            logger.LogError(context.Exception,
                "[JwtAuthFailed] Token validation failed for path={Path}",
                context.HttpContext.Request.Path);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            // Blazor page routes are AllowAnonymous at the HTTP level (JWT lives in
            // localStorage, not cookies, so initial GETs never carry a token).
            // Auth is enforced by AuthorizeRouteView after the circuit connects.
            // This handler only fires for API/hub routes — let JWT return the default 401.
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("JwtOnChallenge");
            logger.LogWarning("[OnChallenge] 401 for path={Path}, method={Method}",
                context.Request.Path, context.Request.Method);
            return Task.CompletedTask;
        }
    };
})
.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
    ApiKeyAuthenticationHandler.SchemeName, _ => { });

// Add authorization
builder.Services.AddAuthorization(options => options.AddNebulaPolicies());
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ServerPermissionHandler>();

// Add database
builder.Services.AddNebulaDatabase(builder.Configuration);

// Add memory cache
builder.Services.AddMemoryCache();

// Add application and infrastructure services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices();

// Configure health checks based on database provider
var dbConfig = builder.Configuration.GetSection("Database");
var dbProvider = dbConfig["Provider"] ?? "SQLite";
var dbConnectionString = dbConfig["ConnectionString"] ?? "Data Source=data/nebula.db";

var healthChecks = builder.Services.AddHealthChecks();

// Add database health check based on provider
if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
    dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    healthChecks.AddNpgSql(dbConnectionString, name: "database", tags: ["db", "ready"]);
}
else
{
    healthChecks.AddSqlite(dbConnectionString, name: "database", tags: ["db", "ready"]);
}

// Add custom health checks
healthChecks
    .AddCheck<DiskSpaceHealthCheck>("disk_space", tags: ["ready"])
    .AddCheck<MemoryHealthCheck>("memory", tags: ["ready"])
    .AddCheck<DockerHealthCheck>("docker", tags: ["ready"])
    .AddCheck<ExternalApiHealthCheck>("external_apis", tags: ["external"]);

// Add SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<IMinecraftInstallNotifier, MinecraftInstallNotifier>();
builder.Services.AddSingleton<IHytaleInstallNotifier, HytaleInstallNotifier>();
builder.Services.AddSingleton<IHytaleUpdateNotifier, HytaleUpdateNotifier>();
builder.Services.AddSingleton<IUpdateNotifier, UpdateNotifier>();
builder.Services.AddSingleton<DashboardNotifier>();
builder.Services.AddSingleton<ISystemHealthNotifier>(sp => sp.GetRequiredService<DashboardNotifier>());
builder.Services.AddSingleton<IHytaleTokenNotifier, HytaleTokenNotifier>();
builder.Services.AddSingleton<INotificationNotifier, NotificationNotifier>();
builder.Services.AddSingleton<IServerRelocationNotifier, ServerRelocationNotifier>();
builder.Services.AddScoped<IServerRelocationService, ServerRelocationService>();

// Add Hangfire for scheduled tasks
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseInMemoryStorage());

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
});

// Add Blazor authentication services
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<IAuthClientService, AuthClientService>();

// Add toast notification service
builder.Services.AddScoped<IToastService, ToastService>();

builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Apply migrations, SQLite optimizations, and seed data on startup (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();
        await context.Database.MigrateAsync();
        await context.ApplySqliteOptimizationsAsync();
    }
    await DataSeeder.SeedDataAsync(app.Services);
    await OfficialGameSeeder.SeedOfficialGamesAsync(app.Services);
}

// Configure the HTTP request pipeline.
app.UseGlobalExceptionHandler();
app.UseSecurityHeaders();

// Serve static files from wwwroot (handles _framework, css, js, etc.)
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapControllers();
app.MapStaticAssets();
// AllowAnonymous at the HTTP endpoint level so the initial GET serves the Blazor
// HTML shell regardless of auth. The JWT lives in localStorage — it's NOT sent on
// plain HTTP requests. Auth is enforced at the Blazor component level by
// AuthorizeRouteView + [Authorize] attributes after the circuit connects and
// JS interop can read the token.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

// Map SignalR hubs
app.MapHub<InstallProgressHub>("/hubs/install-progress");
app.MapHub<UpdateHub>("/hubs/update");
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapHub<HytaleAuthHub>("/hubs/hytale-auth");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ServerRelocationHub>("/hubs/server-relocation");

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    // Return simple status for load balancers
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow
        };
        await context.Response.WriteAsJsonAsync(result);
    },
    // Include all checks for overall health
    Predicate = _ => true
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    // Liveness probe - always returns 200 if app is running
    Predicate = _ => false,
    ResponseWriter = async (context, _) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    // Readiness probe - checks only "ready" tagged health checks
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString()
            })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

// Map Hangfire dashboard (admin only)
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
});

// Synchronize scheduled tasks with Hangfire on startup
using (var scope = app.Services.CreateScope())
{
    var taskService = scope.ServiceProvider.GetRequiredService<IScheduledTaskService>();
    await taskService.SynchronizeJobsAsync();
}

// Handle post-update tasks (restart servers, etc.)
using (var scope = app.Services.CreateScope())
{
    var postUpdateService = scope.ServiceProvider.GetRequiredService<PostUpdateService>();
    await postUpdateService.HandlePostUpdateAsync();
}

// Sync official game mod providers from definitions to database
using (var scope = app.Services.CreateScope())
{
    var officialGameService = scope.ServiceProvider.GetRequiredService<IOfficialGameService>();
    await officialGameService.SyncAllModProvidersAsync();
}

app.Run();
