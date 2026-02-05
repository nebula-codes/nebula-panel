namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents current resource usage of a server process.
/// </summary>
public record ResourceUsage
{
    public double CpuPercent { get; init; }
    public long MemoryBytes { get; init; }
    public long VirtualMemoryBytes { get; init; }
    public long DiskReadBytes { get; init; }
    public long DiskWriteBytes { get; init; }
    public long NetworkSentBytes { get; init; }
    public long NetworkReceivedBytes { get; init; }
    public int ThreadCount { get; init; }
    public int HandleCount { get; init; }
    public TimeSpan Uptime { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public double MemoryMb => MemoryBytes / (1024.0 * 1024.0);
    public double VirtualMemoryMb => VirtualMemoryBytes / (1024.0 * 1024.0);
}
