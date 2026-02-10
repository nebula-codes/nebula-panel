using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Executors;

/// <summary>
/// Executes game servers as native processes (non-Docker).
/// Handles different executable types: Exe, Jar, Shell scripts.
/// </summary>
public class NativeProcessExecutor : IServerExecutor
{
    private readonly ILogger<NativeProcessExecutor> _logger;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly NativeProcessStateStore _processStore;

    public ServerDeploymentType DeploymentType => ServerDeploymentType.Native;

    public NativeProcessExecutor(
        ILogger<NativeProcessExecutor> logger,
        NativeProcessStateStore processStore,
        IServiceScopeFactory? serviceScopeFactory = null)
    {
        _logger = logger;
        _processStore = processStore;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Test constructor — creates its own in-memory state store.
    /// </summary>
    internal NativeProcessExecutor(ILogger<NativeProcessExecutor> logger)
        : this(logger, new NativeProcessStateStore(), null)
    {
    }

    public Task<bool> InstallAsync(
        GameServer server,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        // Native installs are typically done via SteamCMD or manual file copying
        // This is a placeholder - actual implementation would depend on the game
        progress?.Report(InstallProgress.Starting());
        _logger.LogInformation("Native installation for server {ServerId} - manual setup required", server.Id);
        progress?.Report(InstallProgress.Completed());
        return Task.FromResult(true);
    }

    public Task<bool> UpdateAsync(
        GameServer server,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default)
    {
        // Native updates are typically done via SteamCMD or manual file replacement
        // This is a placeholder - actual implementation would depend on the game
        progress?.Report(UpdateProgress.CheckingForUpdates());
        _logger.LogInformation("Native update for server {ServerId} - manual update required", server.Id);
        progress?.Report(UpdateProgress.Completed());
        return Task.FromResult(true);
    }

    public async Task<bool> StartAsync(GameServer server, CancellationToken ct = default)
    {
        if (server.NativeConfig is null)
        {
            _logger.LogError("Server {ServerId} has no native configuration", server.Id);
            return false;
        }

        var config = server.NativeConfig;

        // Check if already running
        if (_processStore.TryGet(server.Id, out var existingProcess) && existingProcess is not null && existingProcess.IsRunning)
        {
            _logger.LogWarning("Server {ServerId} is already running with PID {ProcessId}",
                server.Id, existingProcess.ProcessId);
            return true;
        }

        try
        {
            var arguments = BuildArguments(server);

            // Runtime AOT cache detection for Hytale servers
            // The AOT cache may be downloaded with updates after initial installation
            if (IsHytaleServer(server) && !arguments.Contains("-XX:AOTCache"))
            {
                var aotCachePath = Path.Combine(config.WorkingDirectory, "HytaleServer.aot");
                if (File.Exists(aotCachePath))
                {
                    // Insert AOT arg after memory args, before -jar
                    var jarIndex = arguments.IndexOf("-jar", StringComparison.Ordinal);
                    if (jarIndex > 0)
                    {
                        arguments = arguments.Insert(jarIndex, "-XX:AOTCache=HytaleServer.aot ");
                        _logger.LogInformation(
                            "Detected AOT cache for Hytale server {ServerId}, injecting -XX:AOTCache argument",
                            server.Id);
                    }
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = DetermineExecutable(server),
                Arguments = arguments,
                WorkingDirectory = config.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            // Add environment variables
            foreach (var env in config.EnvironmentVariables)
            {
                startInfo.EnvironmentVariables[env.Key] = env.Value;
            }

            // Inject Hytale session tokens if this is a Hytale server
            if (IsHytaleServer(server))
            {
                await InjectHytaleSessionTokensAsync(server, startInfo, ct).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Starting server {ServerId}: {FileName} {Arguments} in {WorkingDirectory}",
                server.Id, startInfo.FileName, startInfo.Arguments, startInfo.WorkingDirectory);

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            var managedProcess = new ManagedProcess(server.Id, process, _logger);

            if (!process.Start())
            {
                _logger.LogError("Failed to start process for server {ServerId}", server.Id);
                return false;
            }

            server.ProcessId = process.Id;
            server.Status = ServerStatus.Starting;
            server.LastStarted = DateTime.UtcNow;

            _processStore.Set(server.Id, managedProcess);

            // Start capturing output asynchronously
            managedProcess.StartOutputCapture();

            // Monitor for process exit
            process.Exited += async (_, _) =>
            {
                try
                {
                    _logger.LogInformation(
                        "Server {ServerId} process exited with code {ExitCode}",
                        server.Id, process.ExitCode);

                    // Exit code 8 = Hytale update restart request (from official start scripts)
                    if (process.ExitCode == 8 && IsHytaleServer(server))
                    {
                        _logger.LogInformation(
                            "Server {ServerId} requested update restart (exit code 8), restarting automatically",
                            server.Id);

                        server.Status = ServerStatus.Restarting;
                        server.ProcessId = null;
                        _processStore.TryRemove(server.Id, out _);

                        // Brief delay before restart to allow resources to be released
                        await Task.Delay(2000).ConfigureAwait(false);

                        // Restart the server
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await StartAsync(server, CancellationToken.None).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to restart server {ServerId} after exit code 8", server.Id);
                                server.Status = ServerStatus.Crashed;
                            }
                        });
                        return;
                    }

                    server.Status = process.ExitCode == 0 ? ServerStatus.Stopped : ServerStatus.Crashed;
                    server.LastStopped = DateTime.UtcNow;
                    server.ProcessId = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in process Exited handler for server {ServerId}", server.Id);
                }
            };

            _logger.LogInformation(
                "Server {ServerId} started successfully with PID {ProcessId}",
                server.Id, process.Id);

            server.Status = ServerStatus.Running;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start server {ServerId}", server.Id);
            server.Status = ServerStatus.Crashed;
            return false;
        }
    }

    public async Task<bool> StopAsync(GameServer server, bool force = false, CancellationToken ct = default)
    {
        if (!_processStore.TryGet(server.Id, out var managedProcess) || managedProcess is null)
        {
            // Try to find by PID if we have one
            if (server.ProcessId.HasValue)
            {
                try
                {
                    using var process = Process.GetProcessById(server.ProcessId.Value);
                    return await KillProcessAsync(process, force, ct).ConfigureAwait(false);
                }
                catch (ArgumentException)
                {
                    // Process doesn't exist
                    _logger.LogInformation("Server {ServerId} process {ProcessId} not found - already stopped",
                        server.Id, server.ProcessId);
                    server.ProcessId = null;
                    server.Status = ServerStatus.Stopped;
                    return true;
                }
            }

            _logger.LogWarning("No managed process found for server {ServerId}", server.Id);
            return false;
        }

        server.Status = ServerStatus.Stopping;

        try
        {
            var success = await managedProcess.StopAsync(force, ct).ConfigureAwait(false);

            if (success)
            {
                server.Status = ServerStatus.Stopped;
                server.LastStopped = DateTime.UtcNow;
                server.ProcessId = null;
                _processStore.TryRemove(server.Id, out _);

                // End Hytale session if this is a Hytale server
                await EndHytaleSessionAsync(server, ct).ConfigureAwait(false);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping server {ServerId}", server.Id);
            return false;
        }
    }

    public async Task<bool> RestartAsync(GameServer server, CancellationToken ct = default)
    {
        _logger.LogInformation("Restarting server {ServerId}", server.Id);

        var stopped = await StopAsync(server, force: false, ct).ConfigureAwait(false);
        if (!stopped)
        {
            _logger.LogWarning("Failed to stop server {ServerId} gracefully, forcing...", server.Id);
            stopped = await StopAsync(server, force: true, ct).ConfigureAwait(false);
        }

        if (!stopped)
        {
            _logger.LogError("Failed to stop server {ServerId} for restart", server.Id);
            return false;
        }

        // Brief delay to ensure resources are released
        await Task.Delay(1000, ct).ConfigureAwait(false);

        return await StartAsync(server, ct).ConfigureAwait(false);
    }

    public Task<ServerStatus> GetStatusAsync(GameServer server, CancellationToken ct = default)
    {
        // Check managed processes first
        if (_processStore.TryGet(server.Id, out var managedProcess) && managedProcess is not null)
        {
            return Task.FromResult(managedProcess.IsRunning ? ServerStatus.Running : ServerStatus.Stopped);
        }

        // Check by PID
        if (server.ProcessId.HasValue)
        {
            try
            {
                using var process = Process.GetProcessById(server.ProcessId.Value);
                return Task.FromResult(process.HasExited ? ServerStatus.Stopped : ServerStatus.Running);
            }
            catch (ArgumentException)
            {
                // Process doesn't exist
                return Task.FromResult(ServerStatus.Stopped);
            }
        }

        return Task.FromResult(ServerStatus.Unknown);
    }

    public async IAsyncEnumerable<string> StreamConsoleAsync(
        GameServer server,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_processStore.TryGet(server.Id, out var managedProcess) || managedProcess is null)
        {
            _logger.LogWarning("No managed process found for server {ServerId}", server.Id);
            yield break;
        }

        await foreach (var line in managedProcess.ReadOutputAsync(ct).ConfigureAwait(false))
        {
            yield return line;
        }
    }

    public async Task SendCommandAsync(GameServer server, string command, CancellationToken ct = default)
    {
        if (!_processStore.TryGet(server.Id, out var managedProcess) || managedProcess is null)
        {
            _logger.LogWarning("No managed process found for server {ServerId}", server.Id);
            throw new InvalidOperationException($"Server {server.Id} is not running or not managed");
        }

        await managedProcess.SendInputAsync(command, ct).ConfigureAwait(false);
        _logger.LogDebug("Sent command to server {ServerId}: {Command}", server.Id, command);
    }

    public Task<ResourceUsage> GetResourceUsageAsync(GameServer server, CancellationToken ct = default)
    {
        if (!_processStore.TryGet(server.Id, out var managedProcess) || managedProcess is null)
        {
            _logger.LogDebug(
                "No managed process found for server {ServerId}, trying PID lookup (ProcessId={ProcessId})",
                server.Id, server.ProcessId);

            // Try by PID
            if (server.ProcessId.HasValue)
            {
                try
                {
                    using var process = Process.GetProcessById(server.ProcessId.Value);
                    return Task.FromResult(GetProcessResourceUsage(process));
                }
                catch (ArgumentException)
                {
                    _logger.LogDebug("Process {ProcessId} not found for server {ServerId}", server.ProcessId, server.Id);
                }
            }

            return Task.FromResult(new ResourceUsage());
        }

        _logger.LogDebug("Using managed process for server {ServerId}", server.Id);
        return Task.FromResult(managedProcess.GetResourceUsage());
    }

    public Task CleanupAsync(GameServer server, CancellationToken ct = default)
    {
        // No-op for native processes - nothing to clean up
        _logger.LogDebug("CleanupAsync called for native server {ServerId} - no action needed", server.Id);
        return Task.CompletedTask;
    }

    private string DetermineExecutable(GameServer server)
    {
        var game = server.Game;
        var config = server.NativeConfig!;

        // Check for startup script mode first (e.g., modpack-provided start.bat/start.sh)
        if (config.UseStartupScript && !string.IsNullOrEmpty(config.StartupScriptPath))
        {
            var scriptPath = Path.IsPathRooted(config.StartupScriptPath)
                ? config.StartupScriptPath
                : Path.Combine(config.WorkingDirectory, config.StartupScriptPath);

            if (File.Exists(scriptPath))
            {
                _logger.LogDebug("Using startup script: {ScriptPath}", scriptPath);
                if (OperatingSystem.IsWindows())
                {
                    // Run .bat files via cmd.exe
                    return "cmd.exe";
                }
                else
                {
                    // Run .sh scripts directly (must be executable)
                    return scriptPath;
                }
            }
            else
            {
                _logger.LogWarning("Startup script not found: {ScriptPath}, falling back to default", scriptPath);
            }
        }

        return game.ExecutableType switch
        {
            ExecutableType.Jar => config.JavaPath ?? FindJavaExecutable(),
            ExecutableType.Exe => ResolveExePath(config),
            ExecutableType.Shell => GetShellExecutable(),
            _ => config.ExecutablePath
        };
    }

    private static string ResolveExePath(NativeConfiguration config)
    {
        // If path is rooted, use it directly
        if (Path.IsPathRooted(config.ExecutablePath))
        {
            return config.ExecutablePath;
        }

        // Try combining with working directory first
        var combinedPath = Path.Combine(config.WorkingDirectory, config.ExecutablePath);
        if (File.Exists(combinedPath))
        {
            return combinedPath;
        }

        // Otherwise, return as-is and let the system find it in PATH
        return config.ExecutablePath;
    }

    private string BuildArguments(GameServer server)
    {
        var game = server.Game;
        var config = server.NativeConfig!;

        // Check for startup script mode first
        if (config.UseStartupScript && !string.IsNullOrEmpty(config.StartupScriptPath))
        {
            var scriptPath = Path.IsPathRooted(config.StartupScriptPath)
                ? config.StartupScriptPath
                : Path.Combine(config.WorkingDirectory, config.StartupScriptPath);

            if (File.Exists(scriptPath))
            {
                if (OperatingSystem.IsWindows())
                {
                    // cmd /c "path\to\start.bat"
                    return $"/c \"{scriptPath}\"";
                }
                else
                {
                    // Shell script runs directly, no arguments needed
                    return string.Empty;
                }
            }
        }

        return game.ExecutableType switch
        {
            ExecutableType.Jar => BuildJarArguments(config),
            ExecutableType.Shell => BuildShellArguments(config),
            _ => config.Arguments
        };
    }

    private static string BuildJarArguments(NativeConfiguration config)
    {
        var javaArgs = config.JavaArguments ?? "-Xmx2G -Xms1G";

        // Modern Forge (1.17+) uses @argsfile syntax instead of -jar
        // ExecutablePath will be empty and Arguments will contain @file references
        if (string.IsNullOrEmpty(config.ExecutablePath) &&
            config.Arguments.Contains('@'))
        {
            // For modern Forge, arguments already contain everything needed
            // Format: @user_jvm_args.txt @libraries/.../win_args.txt nogui
            // But we need to resolve the actual path if it contains "recommended" or "latest"
            return ResolveForgeArgsPath(config);
        }

        var jarPath = ResolveJarPath(config);
        return $"{javaArgs} -jar \"{jarPath}\" {config.Arguments}".Trim();
    }

    /// <summary>
    /// Resolves the actual Forge args file path, handling cases where
    /// "recommended" or "latest" was stored instead of actual version.
    /// </summary>
    private static string ResolveForgeArgsPath(NativeConfiguration config)
    {
        var arguments = config.Arguments;

        // Check if the path contains "recommended" or "latest" which need resolution
        if (!arguments.Contains("-recommended") && !arguments.Contains("-latest"))
            return arguments;

        // Extract the args file path from arguments
        // Format: @user_jvm_args.txt @libraries/net/minecraftforge/forge/1.20.1-recommended/win_args.txt nogui
        var argsFileName = OperatingSystem.IsWindows() ? "win_args.txt" : "unix_args.txt";
        var forgeLibDir = Path.Combine(config.WorkingDirectory, "libraries", "net", "minecraftforge", "forge");

        if (!Directory.Exists(forgeLibDir))
            return arguments;

        // Find directories that might contain the args file
        var forgeDirs = Directory.GetDirectories(forgeLibDir);
        foreach (var dir in forgeDirs.OrderByDescending(d => new DirectoryInfo(d).CreationTime))
        {
            var argsFile = Path.Combine(dir, argsFileName);
            if (File.Exists(argsFile))
            {
                var dirName = Path.GetFileName(dir);
                var actualPath = $"libraries/net/minecraftforge/forge/{dirName}/{argsFileName}";
                // Replace the placeholder path with actual path
                return $"@user_jvm_args.txt @{actualPath} nogui";
            }
        }

        return arguments;
    }

    private static string ResolveJarPath(NativeConfiguration config)
    {
        // First, try the configured path
        var configuredPath = config.ExecutablePath;
        var fullPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(config.WorkingDirectory, configuredPath);

        if (File.Exists(fullPath))
            return configuredPath;

        // If the configured jar doesn't exist and it looks like a Forge jar,
        // scan for the actual Forge jar file (Forge naming conventions vary)
        if (configuredPath.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
        {
            var foundJar = FindForgeJar(config.WorkingDirectory);
            if (foundJar is not null)
                return foundJar;
        }

        // Return original path (will fail with a clear error message)
        return configuredPath;
    }

    /// <summary>
    /// Finds the actual Forge server jar file in the install directory.
    /// Legacy Forge creates jars with various naming conventions.
    /// </summary>
    private static string? FindForgeJar(string workingDirectory)
    {
        if (!Directory.Exists(workingDirectory))
            return null;

        // Look for forge jar files - they can have various naming patterns:
        // - forge-1.7.10-10.13.4.1614-1.7.10-universal.jar (legacy with MC version suffix)
        // - forge-1.12.2-14.23.5.2859.jar (legacy without suffix)
        // - forge-1.16.5-36.2.39.jar (intermediate)
        var forgeJars = Directory.GetFiles(workingDirectory, "forge-*.jar")
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                // Exclude installer jars
                return !name.Contains("-installer", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(f => new FileInfo(f).Length) // Prefer larger files (universal jars are bigger)
            .ToList();

        return forgeJars.Count > 0 ? Path.GetFileName(forgeJars[0]) : null;
    }

    private static string BuildShellArguments(NativeConfiguration config)
    {
        // For shell scripts, wrap the command properly
        var scriptPath = config.ExecutablePath;
        var args = config.Arguments;

        if (OperatingSystem.IsWindows())
        {
            return $"/c \"{scriptPath}\" {args}".Trim();
        }
        else
        {
            return $"-c \"{scriptPath} {args}\"".Trim();
        }
    }

    private static string GetShellExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            return "cmd.exe";
        }
        return "/bin/bash";
    }

    private static string FindJavaExecutable()
    {
        // Check JAVA_HOME first
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var javaPath = Path.Combine(javaHome, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");
            if (File.Exists(javaPath))
            {
                return javaPath;
            }
        }

        // Fall back to PATH
        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }

    private async Task<bool> KillProcessAsync(Process process, bool force, CancellationToken ct)
    {
        if (process.HasExited)
        {
            return true;
        }

        if (force)
        {
            process.Kill(entireProcessTree: true);
            return true;
        }

        // Try graceful shutdown first
        try
        {
            // On Windows, try CloseMainWindow for GUI apps
            if (OperatingSystem.IsWindows() && process.MainWindowHandle != IntPtr.Zero)
            {
                process.CloseMainWindow();
            }

            // Wait for graceful exit
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown timed out, force kill
            _logger.LogWarning("Graceful shutdown timed out, forcing kill");
            process.Kill(entireProcessTree: true);
            return true;
        }
    }

    private ResourceUsage GetProcessResourceUsage(Process process)
    {
        try
        {
            process.Refresh();

            var memoryBytes = process.WorkingSet64;
            var privateBytes = process.PrivateMemorySize64;
            var reportedMemory = Math.Max(memoryBytes, privateBytes);

            _logger.LogDebug(
                "Fallback resource check for PID {ProcessId} ({ProcessName}): WorkingSet={WorkingSet}MB, PrivateBytes={PrivateBytes}MB",
                process.Id, process.ProcessName,
                memoryBytes / 1024 / 1024, privateBytes / 1024 / 1024);

            return new ResourceUsage
            {
                CpuPercent = 0, // CPU requires time-based calculation
                MemoryBytes = reportedMemory,
                VirtualMemoryBytes = process.VirtualMemorySize64,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                Uptime = DateTime.Now - process.StartTime,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception)
        {
            return new ResourceUsage();
        }
    }

    /// <summary>
    /// Checks if the server is a Hytale server.
    /// </summary>
    private static bool IsHytaleServer(GameServer server)
    {
        return server.Game?.Name?.Equals("Hytale", StringComparison.OrdinalIgnoreCase) == true ||
               server.HytaleInfo is not null;
    }

    /// <summary>
    /// Injects Hytale session and identity tokens into the process environment.
    /// </summary>
    private async Task InjectHytaleSessionTokensAsync(
        GameServer server,
        ProcessStartInfo startInfo,
        CancellationToken ct)
    {
        if (_serviceScopeFactory is null)
        {
            _logger.LogWarning(
                "ServiceScopeFactory not available for server {ServerId}, skipping session token injection",
                server.Id);
            return;
        }

        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var sessionManager = scope.ServiceProvider.GetService<IHytaleSessionManager>();

            if (sessionManager is null)
            {
                _logger.LogWarning(
                    "HytaleSessionManager not available for server {ServerId}, skipping session token injection",
                    server.Id);
                return;
            }

            // Ensure we have a valid session (creates one if needed)
            var credentials = await sessionManager.EnsureSessionAsync(server.OwnerId, ct).ConfigureAwait(false);

            if (credentials?.SessionToken is null || credentials.IdentityToken is null)
            {
                _logger.LogWarning(
                    "No valid Hytale session for server {ServerId} owner {OwnerId}. Server may fail to authenticate.",
                    server.Id, server.OwnerId);
                return;
            }

            // Append tokens as CLI arguments (Hytale server expects --session-token and --identity-token args)
            startInfo.Arguments = $"{startInfo.Arguments} --session-token {credentials.SessionToken} --identity-token {credentials.IdentityToken}";

            _logger.LogInformation(
                "Appended Hytale session tokens to arguments for server {ServerId} (session expires at {ExpiresAt})",
                server.Id, credentials.SessionExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject Hytale session tokens for server {ServerId}", server.Id);
            // Continue without tokens - the server will fail with auth error, but that's better than not starting at all
        }
    }

    /// <summary>
    /// Ends the Hytale game session when a server stops.
    /// </summary>
    private async Task EndHytaleSessionAsync(GameServer server, CancellationToken ct)
    {
        if (_serviceScopeFactory is null || !IsHytaleServer(server))
        {
            return;
        }

        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var sessionManager = scope.ServiceProvider.GetService<IHytaleSessionManager>();

            if (sessionManager is null)
            {
                return;
            }

            await sessionManager.EndSessionAsync(server.OwnerId, ct).ConfigureAwait(false);
            _logger.LogInformation("Ended Hytale session for server {ServerId}", server.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to end Hytale session for server {ServerId}", server.Id);
            // Non-fatal - session will eventually expire on its own
        }
    }
}
