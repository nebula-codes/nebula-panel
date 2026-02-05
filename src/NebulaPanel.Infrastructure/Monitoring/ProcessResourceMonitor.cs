using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Monitoring;

/// <summary>
/// Monitors resource usage of native processes using System.Diagnostics.
/// Provides periodic updates and historical data.
/// </summary>
public class ProcessResourceMonitor : IDisposable
{
    private readonly ILogger<ProcessResourceMonitor> _logger;
    private readonly ConcurrentDictionary<int, ProcessMetrics> _processMetrics = new();
    private readonly ConcurrentDictionary<int, List<ResourceUsage>> _history = new();
    private readonly Timer _sampleTimer;
    private readonly int _maxHistorySize;
    private readonly TimeSpan _sampleInterval;
    private bool _disposed;

    public ProcessResourceMonitor(
        ILogger<ProcessResourceMonitor> logger,
        TimeSpan? sampleInterval = null,
        int maxHistorySize = 100)
    {
        _logger = logger;
        _sampleInterval = sampleInterval ?? TimeSpan.FromSeconds(5);
        _maxHistorySize = maxHistorySize;
        _sampleTimer = new Timer(SampleAllProcesses, null, _sampleInterval, _sampleInterval);
    }

    /// <summary>
    /// Starts monitoring a process.
    /// </summary>
    public void StartMonitoring(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var metrics = new ProcessMetrics(process);
            _processMetrics[processId] = metrics;
            _history[processId] = new List<ResourceUsage>();

            _logger.LogInformation("Started monitoring process {ProcessId}", processId);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Failed to start monitoring process {ProcessId} - not found", processId);
        }
    }

    /// <summary>
    /// Stops monitoring a process.
    /// </summary>
    public void StopMonitoring(int processId)
    {
        _processMetrics.TryRemove(processId, out _);
        _history.TryRemove(processId, out _);
        _logger.LogInformation("Stopped monitoring process {ProcessId}", processId);
    }

    /// <summary>
    /// Gets the current resource usage for a process.
    /// </summary>
    public ResourceUsage? GetCurrentUsage(int processId)
    {
        if (!_processMetrics.TryGetValue(processId, out var metrics))
        {
            return null;
        }

        return metrics.Sample();
    }

    /// <summary>
    /// Gets historical resource usage for a process.
    /// </summary>
    public IReadOnlyList<ResourceUsage> GetHistory(int processId)
    {
        if (!_history.TryGetValue(processId, out var history))
        {
            return Array.Empty<ResourceUsage>();
        }

        lock (history)
        {
            return history.ToList();
        }
    }

    /// <summary>
    /// Gets resource usage by process ID without prior registration.
    /// </summary>
    public static ResourceUsage? GetUsageByPid(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return GetProcessUsage(process);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void SampleAllProcesses(object? state)
    {
        foreach (var kvp in _processMetrics)
        {
            var processId = kvp.Key;
            var metrics = kvp.Value;

            try
            {
                var usage = metrics.Sample();
                if (usage is null)
                {
                    // Process has exited
                    StopMonitoring(processId);
                    continue;
                }

                if (_history.TryGetValue(processId, out var history))
                {
                    lock (history)
                    {
                        history.Add(usage);
                        while (history.Count > _maxHistorySize)
                        {
                            history.RemoveAt(0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sampling process {ProcessId}", processId);
            }
        }
    }

    private static ResourceUsage GetProcessUsage(Process process)
    {
        process.Refresh();

        return new ResourceUsage
        {
            MemoryBytes = process.PrivateMemorySize64,
            VirtualMemoryBytes = process.VirtualMemorySize64,
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            Uptime = DateTime.Now - process.StartTime,
            Timestamp = DateTime.UtcNow
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sampleTimer.Dispose();
        _processMetrics.Clear();
        _history.Clear();
    }

    /// <summary>
    /// Tracks metrics for a single process over time.
    /// </summary>
    private sealed class ProcessMetrics
    {
        private readonly Process _process;
        private DateTime _lastSampleTime;
        private TimeSpan _lastTotalProcessorTime;

        public ProcessMetrics(Process process)
        {
            _process = process;
            _lastSampleTime = DateTime.UtcNow;
            _lastTotalProcessorTime = process.TotalProcessorTime;
        }

        public ResourceUsage? Sample()
        {
            try
            {
                _process.Refresh();

                if (_process.HasExited)
                {
                    return null;
                }

                var currentTime = DateTime.UtcNow;
                var currentCpuTime = _process.TotalProcessorTime;

                var timeDelta = currentTime - _lastSampleTime;
                var cpuDelta = currentCpuTime - _lastTotalProcessorTime;

                _lastSampleTime = currentTime;
                _lastTotalProcessorTime = currentCpuTime;

                double cpuPercent = 0;
                if (timeDelta.TotalMilliseconds > 0)
                {
                    cpuPercent = (cpuDelta.TotalMilliseconds / timeDelta.TotalMilliseconds) * 100.0;
                    cpuPercent = Math.Round(cpuPercent, 2);
                }

                return new ResourceUsage
                {
                    CpuPercent = cpuPercent,
                    MemoryBytes = _process.PrivateMemorySize64,
                    VirtualMemoryBytes = _process.VirtualMemorySize64,
                    ThreadCount = _process.Threads.Count,
                    HandleCount = _process.HandleCount,
                    Uptime = DateTime.Now - _process.StartTime,
                    Timestamp = currentTime
                };
            }
            catch (InvalidOperationException)
            {
                // Process has exited
                return null;
            }
        }
    }
}
