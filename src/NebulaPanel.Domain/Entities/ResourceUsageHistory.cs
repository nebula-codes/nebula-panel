namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Stores historical resource usage data for game servers.
/// Used to display resource charts on the dashboard.
/// </summary>
public class ResourceUsageHistory
{
    public Guid Id { get; set; }

    /// <summary>
    /// The server this usage data is for.
    /// </summary>
    public Guid ServerId { get; set; }
    private GameServer? _server;
    public GameServer Server
    {
        get => _server ?? throw new InvalidOperationException("Server navigation property was not loaded. Use .Include(x => x.Server) in your query.");
        set => _server = value;
    }

    /// <summary>
    /// When this sample was taken.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// CPU usage percentage at this time.
    /// </summary>
    public double CpuPercent { get; set; }

    /// <summary>
    /// Memory usage in bytes at this time.
    /// </summary>
    public long MemoryBytes { get; set; }

    /// <summary>
    /// Player count at this time, if available from server query.
    /// </summary>
    public int? PlayerCount { get; set; }
}
