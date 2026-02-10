using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Interface for RCON service to send commands to game servers.
/// </summary>
public interface IRconService
{
    Task<string?> SendCommandAsync(RconConfiguration config, string command, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(RconConfiguration config, CancellationToken cancellationToken = default);
}
