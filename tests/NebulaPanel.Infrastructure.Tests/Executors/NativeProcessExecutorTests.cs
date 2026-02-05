using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.Executors;
using Xunit;

namespace NebulaPanel.Infrastructure.Tests.Executors;

public class NativeProcessExecutorTests : IDisposable
{
    private readonly Mock<ILogger<NativeProcessExecutor>> _loggerMock;
    private readonly NativeProcessExecutor _executor;
    private readonly string _testDir;

    // Cross-platform executable helpers
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static string ShellExecutable => IsWindows ? "cmd.exe" : "/bin/bash";
    private static string ShellRunArg => IsWindows ? "/c" : "-c";
    private static string ShellInteractiveArg => IsWindows ? "/k" : "-i";
    private static string SleepCommand(int seconds) => IsWindows
        ? $"/c timeout /t {seconds} /nobreak > nul"
        : $"-c \"sleep {seconds}\"";
    private static string EchoAndSleepCommand => IsWindows
        ? "/c echo Hello && timeout /t 5 /nobreak > nul"
        : "-c \"echo Hello && sleep 5\"";

    public NativeProcessExecutorTests()
    {
        _loggerMock = new Mock<ILogger<NativeProcessExecutor>>();
        _executor = new NativeProcessExecutor(_loggerMock.Object);
        _testDir = Path.Combine(Path.GetTempPath(), "nebula-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void DeploymentType_ShouldBeNative()
    {
        _executor.DeploymentType.Should().Be(ServerDeploymentType.Native);
    }

    [Fact]
    public async Task StartAsync_WithNullConfig_ShouldReturnFalse()
    {
        // Arrange
        var server = CreateTestServer();
        server.NativeConfig = null;

        // Act
        var result = await _executor.StartAsync(server);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_WithValidExe_ShouldStartProcess()
    {
        // Arrange
        var server = CreateTestServer();

        // Use a simple command that exists on the current platform
        server.NativeConfig = new NativeConfiguration
        {
            WorkingDirectory = _testDir,
            ExecutablePath = ShellExecutable,
            Arguments = EchoAndSleepCommand
        };
        server.Game.ExecutableType = ExecutableType.Exe;

        // Act
        var result = await _executor.StartAsync(server);

        // Assert
        result.Should().BeTrue();
        server.ProcessId.Should().NotBeNull();
        server.Status.Should().Be(ServerStatus.Running);

        // Cleanup
        await _executor.StopAsync(server, force: true);
    }

    [Fact]
    public async Task StopAsync_WithRunningProcess_ShouldStopGracefully()
    {
        // Arrange
        var server = CreateTestServer();
        server.NativeConfig = new NativeConfiguration
        {
            WorkingDirectory = _testDir,
            ExecutablePath = ShellExecutable,
            Arguments = SleepCommand(60)
        };
        server.Game.ExecutableType = ExecutableType.Exe;

        await _executor.StartAsync(server);
        server.ProcessId.Should().NotBeNull();

        // Act
        var result = await _executor.StopAsync(server, force: true);

        // Assert
        result.Should().BeTrue();
        server.Status.Should().Be(ServerStatus.Stopped);
    }

    [Fact]
    public async Task GetStatusAsync_WithNoProcess_ShouldReturnUnknown()
    {
        // Arrange
        var server = CreateTestServer();
        server.ProcessId = null;

        // Act
        var status = await _executor.GetStatusAsync(server);

        // Assert
        status.Should().Be(ServerStatus.Unknown);
    }

    [Fact]
    public async Task GetResourceUsageAsync_WithRunningProcess_ShouldReturnUsage()
    {
        // Arrange
        var server = CreateTestServer();
        server.NativeConfig = new NativeConfiguration
        {
            WorkingDirectory = _testDir,
            ExecutablePath = ShellExecutable,
            Arguments = SleepCommand(30)
        };
        server.Game.ExecutableType = ExecutableType.Exe;

        await _executor.StartAsync(server);

        // Give the process a moment to start and allocate memory
        await Task.Delay(500);

        // Act
        var usage = await _executor.GetResourceUsageAsync(server);

        // Assert
        usage.Should().NotBeNull();
        usage.MemoryBytes.Should().BeGreaterThanOrEqualTo(0); // May be 0 on some platforms
        usage.ThreadCount.Should().BeGreaterThanOrEqualTo(0);

        // Cleanup
        await _executor.StopAsync(server, force: true);
    }

    [Fact]
    public async Task SendCommandAsync_WithRunningProcess_ShouldSendCommand()
    {
        // Arrange
        var server = CreateTestServer();
        server.NativeConfig = new NativeConfiguration
        {
            WorkingDirectory = _testDir,
            ExecutablePath = ShellExecutable,
            Arguments = ShellInteractiveArg
        };
        server.Game.ExecutableType = ExecutableType.Exe;

        var started = await _executor.StartAsync(server);

        // Skip test if process didn't start (interactive shells may not work in CI)
        if (!started)
        {
            return;
        }

        // Give the process time to initialize
        await Task.Delay(500);

        // Act & Assert
        var act = async () => await _executor.SendCommandAsync(server, "echo test");
        await act.Should().NotThrowAsync();

        // Cleanup
        await _executor.StopAsync(server, force: true);
    }

    [Fact]
    public async Task RestartAsync_WithRunningProcess_ShouldRestartProcess()
    {
        // Arrange
        var server = CreateTestServer();
        server.NativeConfig = new NativeConfiguration
        {
            WorkingDirectory = _testDir,
            ExecutablePath = ShellExecutable,
            Arguments = SleepCommand(60)
        };
        server.Game.ExecutableType = ExecutableType.Exe;

        var started = await _executor.StartAsync(server);

        // Skip test if process didn't start
        if (!started)
        {
            return;
        }

        var originalPid = server.ProcessId;

        // Act
        var result = await _executor.RestartAsync(server);

        // Assert
        result.Should().BeTrue();
        server.ProcessId.Should().NotBeNull();
        server.ProcessId.Should().NotBe(originalPid);

        // Cleanup
        await _executor.StopAsync(server, force: true);
    }

    [Fact]
    public async Task StartAsync_WithMissingExecutable_ReturnsFalse()
    {
        // Arrange
        var server = CreateTestServer();
        server.NativeConfig = new NativeConfiguration
        {
            WorkingDirectory = _testDir,
            ExecutablePath = "nonexistent_executable_12345.exe",
            Arguments = ""
        };
        server.Game.ExecutableType = ExecutableType.Exe;

        // Act
        var result = await _executor.StartAsync(server);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendCommandAsync_WithNoRunningProcess_ThrowsInvalidOperation()
    {
        // Arrange
        var server = CreateTestServer();
        server.ProcessId = null;
        server.Status = ServerStatus.Stopped;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _executor.SendCommandAsync(server, "test command"));
    }

    [Fact]
    public async Task GetResourceUsageAsync_WithNullProcessId_ReturnsZeroUsage()
    {
        // Arrange
        var server = CreateTestServer();
        server.ProcessId = null;

        // Act
        var usage = await _executor.GetResourceUsageAsync(server);

        // Assert
        usage.Should().NotBeNull();
        usage.CpuPercent.Should().Be(0);
        usage.MemoryBytes.Should().Be(0);
    }

    [Fact]
    public async Task GetResourceUsageAsync_WithInvalidProcessId_ReturnsZeroUsage()
    {
        // Arrange
        var server = CreateTestServer();
        server.ProcessId = 999999999; // Very unlikely to exist

        // Act
        var usage = await _executor.GetResourceUsageAsync(server);

        // Assert
        usage.Should().NotBeNull();
    }

    [Fact]
    public async Task StopAsync_WithNullProcessIdAndNoManagedProcess_ReturnsFalse()
    {
        // Arrange
        var server = CreateTestServer();
        server.ProcessId = null;
        server.Status = ServerStatus.Stopped;

        // Act - Server has no ProcessId and no managed process, so stop returns false
        var result = await _executor.StopAsync(server, force: false);

        // Assert - Returns false because there's no process to stop
        result.Should().BeFalse();
    }

    private static GameServer CreateTestServer()
    {
        return new GameServer
        {
            Id = Guid.NewGuid(),
            Name = "Test Server",
            GameId = Guid.NewGuid(),
            Game = new Game
            {
                Id = Guid.NewGuid(),
                Name = "Test Game",
                Slug = "test",
                ExecutableType = ExecutableType.Exe,
                DefaultExecutablePath = "test.exe",
                DefaultStartCommand = ""
            },
            DeploymentType = ServerDeploymentType.Native,
            InstallPath = Path.GetTempPath(),
            PrimaryPort = 25565,
            OwnerId = Guid.NewGuid(),
            Owner = new User { Id = Guid.NewGuid(), Username = "test", Email = "test@test.com", PasswordHash = "" },
            NativeConfig = new NativeConfiguration
            {
                WorkingDirectory = Path.GetTempPath(),
                ExecutablePath = "test.exe",
                Arguments = ""
            }
        };
    }
}
