using NebulaPanel.Agent.Auth;
using NebulaPanel.Agent.Configuration;
using NebulaPanel.Agent.Services;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.Executors;
using NebulaPanel.Infrastructure.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/agent-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting Nebula Panel Agent");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Bind agent configuration
    builder.Services.Configure<AgentOptions>(
        builder.Configuration.GetSection(AgentOptions.Section));

    // Infrastructure services — only what executors need
    builder.Services.AddSingleton<IServerPathResolver, ServerPathResolver>();
    builder.Services.AddSingleton<NativeProcessStateStore>();
    builder.Services.AddSingleton<DockerContainerStateStore>();
    builder.Services.AddSingleton<IServerExecutor, NativeProcessExecutor>();
    builder.Services.AddSingleton<IServerExecutor, DockerServerExecutor>();
    builder.Services.AddSingleton<IServerExecutorFactory, ServerExecutorFactory>();

    // Auth interceptor
    builder.Services.AddSingleton<TokenAuthInterceptor>();

    // gRPC
    builder.Services.AddGrpc(options =>
    {
        options.MaxReceiveMessageSize = 16 * 1024 * 1024; // 16 MB
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    });

    // Configure Kestrel
    var agentOptions = builder.Configuration
        .GetSection(AgentOptions.Section)
        .Get<AgentOptions>() ?? new AgentOptions();

    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.ListenAnyIP(agentOptions.Port, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;

            if (!string.IsNullOrEmpty(agentOptions.TlsCertPath))
            {
                listenOptions.UseHttps(agentOptions.TlsCertPath, agentOptions.TlsKeyPath);
            }
        });
    });

    var app = builder.Build();

    // Map gRPC service with auth interceptor
    app.MapGrpcService<AgentGrpcService>();

    Log.Information("Agent listening on port {Port}", agentOptions.Port);
    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}
