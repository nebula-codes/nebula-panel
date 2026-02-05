using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

public class RconConfiguration
{
    public bool Enabled { get; set; }
    public RconProtocolType Protocol { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; }
    public string? Password { get; set; }
    public bool UseWebSocket { get; set; }
    public string? WebRconPath { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}
