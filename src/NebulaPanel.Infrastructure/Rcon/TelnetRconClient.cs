using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace NebulaPanel.Infrastructure.Rcon;

/// <summary>
/// Telnet-based RCON client for TShock (Terraria).
/// TShock uses a simple telnet protocol with username/password authentication.
/// </summary>
public class TelnetRconClient : IRconClient
{
    private readonly ILogger<TelnetRconClient> _logger;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private const int ReadTimeoutMs = 5000;
    private const int AuthTimeoutMs = 10000;

    public bool IsConnected => _client?.Connected ?? false;

    public TelnetRconClient(ILogger<TelnetRconClient> logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(string host, int port, string password, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            _logger.LogDebug("Already connected to TShock RCON server");
            return;
        }

        _logger.LogInformation("Connecting to TShock RCON server at {Host}:{Port}", host, port);

        _client = new TcpClient
        {
            ReceiveTimeout = ReadTimeoutMs,
            SendTimeout = ReadTimeoutMs
        };

        try
        {
            await _client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            _stream = _client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };

            // TShock RCON authentication flow:
            // 1. Server sends "Username:" prompt
            // 2. Client sends username (we use "admin" by default)
            // 3. Server sends "Password:" prompt
            // 4. Client sends password
            // 5. Server responds with authentication result

            using var authTimeoutCts = new CancellationTokenSource(AuthTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, authTimeoutCts.Token);

            // Wait for username prompt
            var usernamePrompt = await ReadUntilAsync(":", linkedCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Received prompt: {Prompt}", usernamePrompt.Trim());

            if (!usernamePrompt.Contains("Username", StringComparison.OrdinalIgnoreCase) &&
                !usernamePrompt.Contains("login", StringComparison.OrdinalIgnoreCase))
            {
                // Some TShock versions might just ask for password directly if using REST API token
                // Try sending password as username
                await _writer.WriteLineAsync("admin").ConfigureAwait(false);
            }
            else
            {
                // Send username
                await _writer.WriteLineAsync("admin").ConfigureAwait(false);

                // Wait for password prompt
                var passwordPrompt = await ReadUntilAsync(":", linkedCts.Token).ConfigureAwait(false);
                _logger.LogDebug("Received prompt: {Prompt}", passwordPrompt.Trim());
            }

            // Send password
            await _writer.WriteLineAsync(password).ConfigureAwait(false);

            // Read authentication response
            var response = await ReadLineWithTimeoutAsync(linkedCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Auth response: {Response}", response?.Trim());

            if (response is null ||
                response.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                response.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                response.Contains("denied", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("TShock RCON authentication failed - invalid credentials");
            }

            _logger.LogInformation("Successfully connected and authenticated to TShock RCON server at {Host}:{Port}", host, port);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to TShock RCON server at {Host}:{Port}", host, port);
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _writer is null || _reader is null)
        {
            throw new InvalidOperationException("Not connected to TShock RCON server");
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogDebug("Sending TShock RCON command: {Command}", command);

            // Send command
            await _writer.WriteLineAsync(command).ConfigureAwait(false);

            // Read response (TShock typically sends response ending with newline)
            var response = await ReadResponseAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("TShock RCON response: {Response}", response);

            return response;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            // Try to send exit command gracefully
            if (_writer is not null && IsConnected)
            {
                try
                {
                    await _writer.WriteLineAsync("exit").ConfigureAwait(false);
                }
                catch
                {
                    // Ignore errors when disconnecting
                }
            }
        }
        finally
        {
            _writer?.Dispose();
            _writer = null;

            _reader?.Dispose();
            _reader = null;

            if (_stream is not null)
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
                _stream = null;
            }

            _client?.Dispose();
            _client = null;

            _logger.LogDebug("Disconnected from TShock RCON server");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _sendLock.Dispose();
    }

    /// <summary>
    /// Reads from the stream until the specified delimiter is found.
    /// </summary>
    private async Task<string> ReadUntilAsync(string delimiter, CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();
        var charBuffer = new char[1];

        while (!cancellationToken.IsCancellationRequested)
        {
            var charsRead = await _reader!.ReadAsync(charBuffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (charsRead == 0)
            {
                throw new InvalidOperationException("Connection closed by server");
            }

            buffer.Append(charBuffer[0]);

            if (buffer.ToString().EndsWith(delimiter))
            {
                break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return buffer.ToString();
    }

    /// <summary>
    /// Reads a line with timeout handling.
    /// </summary>
    private async Task<string?> ReadLineWithTimeoutAsync(CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();
        var charBuffer = new char[1];

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_stream?.DataAvailable != true)
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var charsRead = await _reader!.ReadAsync(charBuffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (charsRead == 0)
            {
                break;
            }

            if (charBuffer[0] == '\n')
            {
                break;
            }

            if (charBuffer[0] != '\r')
            {
                buffer.Append(charBuffer[0]);
            }
        }

        return buffer.ToString();
    }

    /// <summary>
    /// Reads the command response from the server.
    /// </summary>
    private async Task<string> ReadResponseAsync(CancellationToken cancellationToken)
    {
        var response = new StringBuilder();
        var emptyLineCount = 0;

        // TShock responses vary - wait for data or timeout
        using var timeoutCts = new CancellationTokenSource(ReadTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                // Check if data is available
                if (_stream?.DataAvailable != true)
                {
                    // Brief wait for more data
                    await Task.Delay(100, linkedCts.Token).ConfigureAwait(false);

                    // If still no data, check if we have any response
                    if (_stream?.DataAvailable != true)
                    {
                        if (response.Length > 0)
                        {
                            // We have some response, return it
                            break;
                        }

                        emptyLineCount++;
                        if (emptyLineCount > 10)
                        {
                            // No data after waiting, break
                            break;
                        }

                        continue;
                    }
                }

                var line = await ReadLineWithTimeoutAsync(linkedCts.Token).ConfigureAwait(false);

                if (string.IsNullOrEmpty(line))
                {
                    emptyLineCount++;
                    if (emptyLineCount > 2)
                    {
                        break;
                    }

                    continue;
                }

                emptyLineCount = 0;

                if (response.Length > 0)
                {
                    response.AppendLine();
                }

                response.Append(line);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Timeout is fine, we just return what we have
        }

        return response.ToString().Trim();
    }
}
