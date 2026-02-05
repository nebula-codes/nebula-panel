namespace NebulaPanel.Infrastructure.Rcon;

/// <summary>
/// Interface for RCON client implementations.
/// </summary>
public interface IRconClient : IAsyncDisposable
{
    /// <summary>
    /// Whether the client is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the RCON server and authenticates.
    /// </summary>
    Task ConnectAsync(string host, int port, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command and returns the response.
    /// </summary>
    Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the RCON server.
    /// </summary>
    Task DisconnectAsync();
}
