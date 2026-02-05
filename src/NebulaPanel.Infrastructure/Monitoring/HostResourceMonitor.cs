using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Infrastructure.Monitoring;

/// <summary>
/// Monitors host system resources (CPU, memory, disk).
/// </summary>
public class HostResourceMonitor : IHostMonitorService
{
    private readonly ILogger<HostResourceMonitor> _logger;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly object _cpuLock = new();

    // CPU tracking for delta calculation
    private ulong _lastIdleTime;
    private ulong _lastKernelTime;
    private ulong _lastUserTime;
    private double _lastCpuValue;
    private DateTime _lastCpuSampleTime;
    private bool _cpuInitialized;

    public HostResourceMonitor(ILogger<HostResourceMonitor> logger)
    {
        _logger = logger;
        _lastCpuSampleTime = DateTime.UtcNow;
        _lastCpuValue = 0;
        _cpuInitialized = false;

        // Try to initialize Windows performance counter for CPU
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                // Initial read to prime the counter (returns 0, but necessary)
                _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize CPU performance counter, will use fallback method");
                _cpuCounter = null;
            }

            // Initialize GetSystemTimes for fallback
            InitializeSystemTimes();
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void InitializeSystemTimes()
    {
        try
        {
            if (GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
            {
                _lastIdleTime = idleTime;
                _lastKernelTime = kernelTime;
                _lastUserTime = userTime;
                _cpuInitialized = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize system times");
        }
    }

    public Task<SystemHealthDto> GetCurrentHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cpuPercent = GetSystemCpuUsage();
            var (memoryUsed, memoryTotal, memoryPercent) = GetSystemMemoryUsage();
            var disks = GetDiskUsage();

            var health = new SystemHealthDto(
                CpuPercent: cpuPercent,
                MemoryPercent: memoryPercent,
                MemoryUsedBytes: memoryUsed,
                MemoryTotalBytes: memoryTotal,
                Disks: disks,
                Timestamp: DateTime.UtcNow
            );

            return Task.FromResult(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system health");
            return Task.FromResult(new SystemHealthDto(
                CpuPercent: 0,
                MemoryPercent: 0,
                MemoryUsedBytes: 0,
                MemoryTotalBytes: 0,
                Disks: [],
                Timestamp: DateTime.UtcNow
            ));
        }
    }

    private double GetSystemCpuUsage()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsCpuUsage();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxCpuUsage();
            }
            else
            {
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting CPU usage");
            return 0;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private double GetWindowsCpuUsage()
    {
        lock (_cpuLock)
        {
            // Use GetSystemTimes as primary method (more reliable than PerformanceCounter)
            try
            {
                if (GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
                {
                    if (_cpuInitialized)
                    {
                        // Calculate deltas (using unsigned arithmetic to handle wraparound)
                        var idleDelta = idleTime - _lastIdleTime;
                        var kernelDelta = kernelTime - _lastKernelTime;
                        var userDelta = userTime - _lastUserTime;

                        // Total time = kernel + user (kernel includes idle time in Windows)
                        var totalDelta = kernelDelta + userDelta;

                        if (totalDelta > 0)
                        {
                            // CPU usage = (total - idle) / total * 100
                            // Since kernel includes idle: busy = kernel - idle + user
                            var busyDelta = kernelDelta - idleDelta + userDelta;
                            var cpuPercent = ((double)busyDelta / totalDelta) * 100.0;

                            _lastCpuValue = Math.Round(Math.Max(0, Math.Min(100, cpuPercent)), 1);
                        }
                    }

                    // Update tracking values
                    _lastIdleTime = idleTime;
                    _lastKernelTime = kernelTime;
                    _lastUserTime = userTime;
                    _lastCpuSampleTime = DateTime.UtcNow;
                    _cpuInitialized = true;

                    return _lastCpuValue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading system times");
            }

            // Fallback to PerformanceCounter
            if (_cpuCounter is not null)
            {
                try
                {
                    var value = _cpuCounter.NextValue();
                    if (value > 0)
                    {
                        _lastCpuValue = Math.Round(value, 1);
                    }
                    return _lastCpuValue;
                }
                catch
                {
                    // Return last known value
                }
            }

            return _lastCpuValue;
        }
    }

    private double GetLinuxCpuUsage()
    {
        lock (_cpuLock)
        {
            try
            {
                var statLines = File.ReadAllLines("/proc/stat");
                var cpuLine = statLines.FirstOrDefault(l => l.StartsWith("cpu "));

                if (cpuLine is not null)
                {
                    var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        // Parse CPU times from /proc/stat
                        // Format: cpu user nice system idle iowait irq softirq steal guest guest_nice
                        var user = ulong.Parse(parts[1]);
                        var nice = ulong.Parse(parts[2]);
                        var system = ulong.Parse(parts[3]);
                        var idle = ulong.Parse(parts[4]);
                        var iowait = parts.Length > 5 ? ulong.Parse(parts[5]) : 0UL;

                        // Total idle = idle + iowait
                        var totalIdle = idle + iowait;
                        // Total active = user + nice + system
                        var totalActive = user + nice + system;
                        var total = totalIdle + totalActive;

                        if (_cpuInitialized)
                        {
                            var totalDelta = total - (_lastIdleTime + _lastKernelTime + _lastUserTime);
                            var idleDelta = totalIdle - _lastIdleTime;

                            if (totalDelta > 0)
                            {
                                var cpuPercent = ((double)(totalDelta - idleDelta) / totalDelta) * 100.0;
                                _lastCpuValue = Math.Round(Math.Max(0, Math.Min(100, cpuPercent)), 1);
                            }
                        }

                        // Store values for next calculation
                        // _lastIdleTime = totalIdle, _lastKernelTime = system, _lastUserTime = user + nice
                        _lastIdleTime = totalIdle;
                        _lastKernelTime = system;
                        _lastUserTime = user + nice;
                        _lastCpuSampleTime = DateTime.UtcNow;
                        _cpuInitialized = true;

                        return _lastCpuValue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading /proc/stat");
            }

            return _lastCpuValue;
        }
    }

    private (long Used, long Total, double Percent) GetSystemMemoryUsage()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsMemoryUsage();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxMemoryUsage();
            }
            else
            {
                return (0, 0, 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting memory usage");
            return (0, 0, 0);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private (long Used, long Total, double Percent) GetWindowsMemoryUsage()
    {
        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };

        if (GlobalMemoryStatusEx(ref memStatus))
        {
            var totalBytes = (long)memStatus.ullTotalPhys;
            var availableBytes = (long)memStatus.ullAvailPhys;
            var usedBytes = totalBytes - availableBytes;
            var percent = totalBytes > 0 ? ((double)usedBytes / totalBytes) * 100.0 : 0;

            return (usedBytes, totalBytes, Math.Round(percent, 1));
        }

        // Fallback using GC info
        var gcMemory = GC.GetGCMemoryInfo();
        var total = gcMemory.TotalAvailableMemoryBytes;
        if (total > 0)
        {
            var used = total - gcMemory.HighMemoryLoadThresholdBytes;
            return (used, total, Math.Round(((double)used / total) * 100, 1));
        }

        return (0, 0, 0);
    }

    private (long Used, long Total, double Percent) GetLinuxMemoryUsage()
    {
        try
        {
            var memInfo = File.ReadAllLines("/proc/meminfo");
            long totalKb = 0;
            long availableKb = 0;

            foreach (var line in memInfo)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    totalKb = ParseMemInfoValue(line);
                }
                else if (line.StartsWith("MemAvailable:"))
                {
                    availableKb = ParseMemInfoValue(line);
                }
            }

            if (totalKb > 0)
            {
                var totalBytes = totalKb * 1024;
                var usedBytes = (totalKb - availableKb) * 1024;
                var percent = ((double)usedBytes / totalBytes) * 100;
                return (usedBytes, totalBytes, Math.Round(percent, 1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading /proc/meminfo");
        }

        return (0, 0, 0);
    }

    private static long ParseMemInfoValue(string line)
    {
        var parts = line.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            var valuePart = parts[1].Replace("kB", "").Trim();
            if (long.TryParse(valuePart, out var value))
            {
                return value;
            }
        }
        return 0;
    }

    private List<DiskUsageDto> GetDiskUsage()
    {
        var disks = new List<DiskUsageDto>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady)
                    {
                        continue;
                    }

                    // Skip non-fixed drives (network, cdrom, etc.)
                    if (drive.DriveType != DriveType.Fixed)
                    {
                        continue;
                    }

                    var totalBytes = drive.TotalSize;
                    var freeBytes = drive.AvailableFreeSpace;
                    var usedBytes = totalBytes - freeBytes;
                    var usagePercent = totalBytes > 0 ? ((double)usedBytes / totalBytes) * 100 : 0;

                    disks.Add(new DiskUsageDto(
                        DriveName: drive.Name,
                        DriveLabel: string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel,
                        UsedBytes: usedBytes,
                        TotalBytes: totalBytes,
                        UsagePercent: Math.Round(usagePercent, 1)
                    ));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting disk info for {DriveName}", drive.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enumerating drives");
        }

        return disks;
    }

    #region Windows P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);

    #endregion
}
