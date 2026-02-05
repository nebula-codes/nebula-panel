namespace NebulaPanel.Domain.Enums;

public enum CommandMethod
{
    Stdin,      // Send via process stdin
    Rcon,       // Send via RCON protocol
    WebApi      // Send via game's HTTP API (rare)
}
