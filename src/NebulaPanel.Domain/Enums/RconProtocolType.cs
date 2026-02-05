namespace NebulaPanel.Domain.Enums;

public enum RconProtocolType
{
    Source,     // Valve Source RCON
    Minecraft,  // Minecraft RCON (similar to Source but different packet format)
    WebRcon,    // HTTP/WebSocket based RCON
    Battleye,   // Battleye RCON protocol
    Telnet      // Telnet-based RCON (TShock for Terraria)
}
