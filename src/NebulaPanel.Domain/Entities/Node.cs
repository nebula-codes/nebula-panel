using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Entities;

public class Node
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? AgentToken { get; set; }
    public NodeStatus Status { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public ResourceLimits? Capacity { get; set; }

    // Relationships
    public ICollection<GameServer> Servers { get; set; } = [];
}
