namespace NebulaPanel.Domain.ValueObjects;

public class ResourceLimits
{
    public int? MaxMemoryMb { get; set; }
    public int? MaxCpuPercent { get; set; }
    public int? MaxDiskMb { get; set; }
    public int? MaxNetworkMbps { get; set; }
}
