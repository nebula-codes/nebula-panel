using System.Collections.Concurrent;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using NebulaPanel.Agent.Shared.Protos;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Executors;

/// <summary>
/// Manages pooled gRPC channels to Agent nodes.
/// One channel per node address, reused across requests.
/// Injects bearer tokens via CallCredentials for agent authentication.
/// </summary>
public sealed class AgentChannelManager : IDisposable
{
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
    private readonly ILogger<AgentChannelManager> _logger;
    private bool _disposed;

    public AgentChannelManager(ILogger<AgentChannelManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets a gRPC client for the specified node, creating/reusing a channel as needed.
    /// </summary>
    public AgentService.AgentServiceClient GetClient(Node node)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = _channels.GetOrAdd(node.Address, address =>
        {
            _logger.LogInformation("Creating gRPC channel to agent at {Address}", address);
            return CreateChannel(address, node.AgentToken);
        });

        return new AgentService.AgentServiceClient(channel);
    }

    /// <summary>
    /// Invalidates and disposes the channel for a given address.
    /// Used when connection failures are detected.
    /// </summary>
    public void InvalidateChannel(string address)
    {
        if (_channels.TryRemove(address, out var channel))
        {
            _logger.LogInformation("Invalidating gRPC channel to {Address}", address);
            channel.Dispose();
        }
    }

    private static GrpcChannel CreateChannel(string address, string? agentToken)
    {
        var uri = new Uri(address);
        var isHttps = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

        if (isHttps && !string.IsNullOrEmpty(agentToken))
        {
            var credentials = CallCredentials.FromInterceptor((_, metadata) =>
            {
                metadata.Add("authorization", $"Bearer {agentToken}");
                return Task.CompletedTask;
            });

            return GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.Create(new SslCredentials(), credentials)
            });
        }

        // For insecure channels (development), use a handler that allows HTTP/2 without TLS
        var handler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true
        };

        var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = handler,
            DisposeHttpClient = true,
            Credentials = ChannelCredentials.Insecure
        });

        return channel;
    }

    /// <summary>
    /// Creates call options with the agent token as metadata.
    /// Used for insecure channels where CallCredentials can't inject the token.
    /// </summary>
    public static CallOptions CreateCallOptions(Node node, CancellationToken ct = default)
    {
        var headers = new Metadata();
        if (!string.IsNullOrEmpty(node.AgentToken))
        {
            headers.Add("authorization", $"Bearer {node.AgentToken}");
        }
        return new CallOptions(headers: headers, cancellationToken: ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _channels)
        {
            kvp.Value.Dispose();
        }
        _channels.Clear();
    }
}
