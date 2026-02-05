using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Default RCON settings for a game. Individual servers can override these.
/// </summary>
public class RconDefaults
{
    public bool DefaultEnabled { get; set; }
    public RconProtocolType Protocol { get; set; }
    public int DefaultPort { get; set; }
    public bool UseWebSocket { get; set; }
    public string? WebRconPath { get; set; }
}
