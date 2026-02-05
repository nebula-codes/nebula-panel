using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Executors;

/// <summary>
/// Manages a single process instance, including I/O streams and resource monitoring.
/// </summary>
internal sealed class ManagedProcess : IDisposable
{
    private readonly Guid _serverId;
    private readonly Process _process;
    private readonly ILogger _logger;
    private readonly Channel<string> _outputChannel;
    private readonly SemaphoreSlim _inputLock = new(1, 1);

    private CancellationTokenSource? _outputCts;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    private DateTime? _lastCpuTime;
    private TimeSpan _lastTotalProcessorTime;
    private bool _disposed;

    public int ProcessId => _process.Id;
    public bool IsRunning => !_process.HasExited;
    public DateTime StartTime => _process.StartTime;

    public ManagedProcess(Guid serverId, Process process, ILogger logger)
    {
        _serverId = serverId;
        _process = process;
        _logger = logger;
        _outputChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Starts capturing stdout and stderr from the process.
    /// </summary>
    public void StartOutputCapture()
    {
        _outputCts = new CancellationTokenSource();
        var ct = _outputCts.Token;

        _stdoutTask = CaptureStreamAsync(_process.StandardOutput, "stdout", ct);
        _stderrTask = CaptureStreamAsync(_process.StandardError, "stderr", ct);
    }

    private async Task CaptureStreamAsync(StreamReader reader, string streamName, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null)
                {
                    break; // End of stream
                }

                var timestampedLine = $"[{DateTime.UtcNow:HH:mm:ss}] {line}";

                // Try to write to channel, drop if full (shouldn't happen with DropOldest)
                _outputChannel.Writer.TryWrite(timestampedLine);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing {StreamName} for server {ServerId}", streamName, _serverId);
        }
    }

    /// <summary>
    /// Reads output lines asynchronously.
    /// </summary>
    public async IAsyncEnumerable<string> ReadOutputAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var line in _outputChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return line;
        }
    }

    /// <summary>
    /// Sends input to the process stdin.
    /// </summary>
    public async Task SendInputAsync(string command, CancellationToken ct = default)
    {
        if (_process.HasExited)
        {
            throw new InvalidOperationException("Process has exited");
        }

        await _inputLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _process.StandardInput.WriteLineAsync(command.AsMemory(), ct).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _inputLock.Release();
        }
    }

    /// <summary>
    /// Stops the process, optionally forcing termination.
    /// </summary>
    public async Task<bool> StopAsync(bool force, CancellationToken ct = default)
    {
        if (_process.HasExited)
        {
            return true;
        }

        if (force)
        {
            _logger.LogInformation("Force killing server {ServerId} process {ProcessId}",
                _serverId, _process.Id);
            _process.Kill(entireProcessTree: true);
            return true;
        }

        // Try graceful shutdown
        _logger.LogInformation("Attempting graceful shutdown of server {ServerId}", _serverId);

        // Send common stop commands
        try
        {
            await SendInputAsync("stop", ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send stop command to server {ServerId}", _serverId);
        }

        // Wait for graceful exit with timeout
        using var gracefulCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        gracefulCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await _process.WaitForExitAsync(gracefulCts.Token).ConfigureAwait(false);
            _logger.LogInformation("Server {ServerId} stopped gracefully", _serverId);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Graceful shutdown timed out
            _logger.LogWarning("Graceful shutdown timed out for server {ServerId}, forcing...", _serverId);
            _process.Kill(entireProcessTree: true);
            return true;
        }
    }

    /// <summary>
    /// Gets current resource usage for the process.
    /// </summary>
    public ResourceUsage GetResourceUsage()
    {
        if (_process.HasExited)
        {
            _logger.LogDebug("Process {ProcessId} for server {ServerId} has exited", _process.Id, _serverId);
            return new ResourceUsage();
        }

        try
        {
            _process.Refresh();

            var cpuPercent = CalculateCpuPercent();
            var memoryBytes = _process.WorkingSet64;
            var privateBytes = _process.PrivateMemorySize64;

            _logger.LogDebug(
                "Server {ServerId} PID {ProcessId} ({ProcessName}): WorkingSet={WorkingSet}MB, PrivateBytes={PrivateBytes}MB",
                _serverId, _process.Id, _process.ProcessName,
                memoryBytes / 1024 / 1024, privateBytes / 1024 / 1024);

            // Use the larger of WorkingSet64 or PrivateMemorySize64
            // WorkingSet64 is physical RAM, PrivateMemorySize64 is committed memory
            var reportedMemory = Math.Max(memoryBytes, privateBytes);

            return new ResourceUsage
            {
                CpuPercent = cpuPercent,
                MemoryBytes = reportedMemory,
                VirtualMemoryBytes = _process.VirtualMemorySize64,
                ThreadCount = _process.Threads.Count,
                HandleCount = _process.HandleCount,
                Uptime = DateTime.Now - _process.StartTime,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting resource usage for server {ServerId}", _serverId);
            return new ResourceUsage();
        }
    }

    private double CalculateCpuPercent()
    {
        var currentTime = DateTime.UtcNow;
        var currentCpuTime = _process.TotalProcessorTime;

        if (_lastCpuTime is null)
        {
            _lastCpuTime = currentTime;
            _lastTotalProcessorTime = currentCpuTime;
            return 0;
        }

        var timeDelta = currentTime - _lastCpuTime.Value;
        var cpuDelta = currentCpuTime - _lastTotalProcessorTime;

        _lastCpuTime = currentTime;
        _lastTotalProcessorTime = currentCpuTime;

        if (timeDelta.TotalMilliseconds <= 0)
        {
            return 0;
        }

        // Calculate CPU percentage based on elapsed time and processor count
        var cpuPercent = (cpuDelta.TotalMilliseconds / timeDelta.TotalMilliseconds) * 100.0;
        return Math.Round(cpuPercent, 2);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _outputCts?.Cancel();
        _outputChannel.Writer.TryComplete();

        try
        {
            _stdoutTask?.Wait(TimeSpan.FromSeconds(1));
            _stderrTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore task wait exceptions on dispose
        }

        _outputCts?.Dispose();
        _inputLock.Dispose();
        _process.Dispose();
    }
}
