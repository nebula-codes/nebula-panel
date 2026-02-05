using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Rcon;

public class RconService : IRconService
{
    private readonly ILogger<RconService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public RconService(ILogger<RconService> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<string?> SendCommandAsync(RconConfiguration config, string command, CancellationToken cancellationToken = default)
    {
        if (!config.Enabled || string.IsNullOrEmpty(config.Password))
        {
            _logger.LogDebug("RCON is not enabled or password not set");
            return null;
        }

        IRconClient? client = null;
        try
        {
            client = CreateClient(config.Protocol);
            if (client is null)
            {
                _logger.LogWarning("No RCON client available for protocol {Protocol}", config.Protocol);
                return null;
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await client.ConnectAsync(config.Host, config.Port, config.Password, linkedCts.Token).ConfigureAwait(false);
            var response = await client.SendCommandAsync(command, linkedCts.Token).ConfigureAwait(false);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("RCON command timed out after {Timeout} seconds", config.TimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send RCON command: {Message}", ex.Message);
            return null;
        }
        finally
        {
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<bool> TestConnectionAsync(RconConfiguration config, CancellationToken cancellationToken = default)
    {
        if (!config.Enabled || string.IsNullOrEmpty(config.Password))
        {
            return false;
        }

        IRconClient? client = null;
        try
        {
            client = CreateClient(config.Protocol);
            if (client is null)
            {
                return false;
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await client.ConnectAsync(config.Host, config.Port, config.Password, linkedCts.Token).ConfigureAwait(false);

            return client.IsConnected;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RCON connection test failed");
            return false;
        }
        finally
        {
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private IRconClient? CreateClient(RconProtocolType protocol)
    {
        return protocol switch
        {
            RconProtocolType.Minecraft => new MinecraftRconClient(_loggerFactory.CreateLogger<MinecraftRconClient>()),
            RconProtocolType.Source => new MinecraftRconClient(_loggerFactory.CreateLogger<MinecraftRconClient>()), // Source and MC use same protocol
            RconProtocolType.Telnet => new TelnetRconClient(_loggerFactory.CreateLogger<TelnetRconClient>()), // TShock (Terraria) uses Telnet
            _ => null
        };
    }
}
