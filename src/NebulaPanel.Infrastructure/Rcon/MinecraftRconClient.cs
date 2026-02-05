using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace NebulaPanel.Infrastructure.Rcon;

/// <summary>
/// RCON client for Minecraft servers.
/// Implements the Source RCON protocol used by Minecraft.
/// </summary>
public class MinecraftRconClient : IRconClient
{
    private readonly ILogger<MinecraftRconClient> _logger;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _requestId;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private const int PacketTypeLogin = 3;
    private const int PacketTypeCommand = 2;
    private const int PacketTypeResponse = 0;
    private const int MaxPacketSize = 4096;

    public bool IsConnected => _client?.Connected ?? false;

    public MinecraftRconClient(ILogger<MinecraftRconClient> logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(string host, int port, string password, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            _logger.LogDebug("Already connected to RCON server");
            return;
        }

        _logger.LogInformation("Connecting to RCON server at {Host}:{Port} with password length {PasswordLength}, first 4 chars: {PasswordStart}",
            host, port, password?.Length ?? 0, password?.Length >= 4 ? password[..4] + "..." : password);

        _client = new TcpClient();

        try
        {
            await _client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            _stream = _client.GetStream();

            _logger.LogInformation("TCP connection established, sending authentication packet");

            // Authenticate
            var response = await SendPacketAsync(PacketTypeLogin, password, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Auth response: RequestId={RequestId}, Type={Type}", response.RequestId, response.Type);

            if (response.RequestId == -1)
            {
                throw new InvalidOperationException("RCON authentication failed - invalid password");
            }

            _logger.LogInformation("Successfully connected and authenticated to RCON server at {Host}:{Port}", host, port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RCON server at {Host}:{Port}", host, port);
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream is null)
        {
            throw new InvalidOperationException("Not connected to RCON server");
        }

        _logger.LogDebug("Sending RCON command: {Command}", command);

        var response = await SendPacketAsync(PacketTypeCommand, command, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("RCON response: {Response}", response.Payload);

        return response.Payload;
    }

    public async Task DisconnectAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }

        _client?.Dispose();
        _client = null;

        _logger.LogDebug("Disconnected from RCON server");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _sendLock.Dispose();
    }

    private async Task<RconPacket> SendPacketAsync(int type, string payload, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var requestId = Interlocked.Increment(ref _requestId);
            var packet = CreatePacket(requestId, type, payload);

            await _stream!.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            return await ReadPacketAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static byte[] CreatePacket(int requestId, int type, string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var packetLength = 4 + 4 + payloadBytes.Length + 2; // requestId + type + payload + 2 null terminators

        var packet = new byte[4 + packetLength]; // length prefix + packet
        var offset = 0;

        // Length (not including the length field itself)
        WriteInt32(packet, ref offset, packetLength);

        // Request ID
        WriteInt32(packet, ref offset, requestId);

        // Type
        WriteInt32(packet, ref offset, type);

        // Payload
        Array.Copy(payloadBytes, 0, packet, offset, payloadBytes.Length);
        offset += payloadBytes.Length;

        // Two null terminators
        packet[offset++] = 0;
        packet[offset] = 0;

        return packet;
    }

    private async Task<RconPacket> ReadPacketAsync(CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        await ReadExactAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
        var length = ReadInt32(lengthBuffer, 0);

        if (length > MaxPacketSize)
        {
            throw new InvalidOperationException($"RCON packet too large: {length} bytes");
        }

        var packetBuffer = new byte[length];
        await ReadExactAsync(packetBuffer, cancellationToken).ConfigureAwait(false);

        var requestId = ReadInt32(packetBuffer, 0);
        var type = ReadInt32(packetBuffer, 4);

        // Payload is everything after the header, minus the two null terminators
        var payloadLength = length - 4 - 4 - 2;
        var payload = payloadLength > 0
            ? Encoding.UTF8.GetString(packetBuffer, 8, payloadLength)
            : string.Empty;

        return new RconPacket(requestId, type, payload);
    }

    private async Task ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await _stream!.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
            {
                throw new InvalidOperationException("Connection closed by server");
            }

            offset += read;
        }
    }

    private static void WriteInt32(byte[] buffer, ref int offset, int value)
    {
        buffer[offset++] = (byte)(value & 0xFF);
        buffer[offset++] = (byte)((value >> 8) & 0xFF);
        buffer[offset++] = (byte)((value >> 16) & 0xFF);
        buffer[offset++] = (byte)((value >> 24) & 0xFF);
    }

    private static int ReadInt32(byte[] buffer, int offset)
    {
        return buffer[offset] |
               (buffer[offset + 1] << 8) |
               (buffer[offset + 2] << 16) |
               (buffer[offset + 3] << 24);
    }

    private record RconPacket(int RequestId, int Type, string Payload);
}
