using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;
using NebulaPanel.Agent.Configuration;

namespace NebulaPanel.Agent.Auth;

/// <summary>
/// gRPC server interceptor that validates Bearer token from metadata.
/// </summary>
public class TokenAuthInterceptor(IOptions<AgentOptions> options, ILogger<TokenAuthInterceptor> logger)
    : Interceptor
{
    private readonly string _expectedToken = options.Value.Token;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ValidateToken(context);
        return await continuation(request, context).ConfigureAwait(false);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ValidateToken(context);
        await continuation(request, responseStream, context).ConfigureAwait(false);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ValidateToken(context);
        return await continuation(requestStream, context).ConfigureAwait(false);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ValidateToken(context);
        await continuation(requestStream, responseStream, context).ConfigureAwait(false);
    }

    private void ValidateToken(ServerCallContext context)
    {
        if (string.IsNullOrEmpty(_expectedToken))
        {
            logger.LogWarning("Agent token is not configured — rejecting all requests");
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Agent token not configured"));
        }

        var authHeader = context.RequestHeaders.GetValue("authorization");
        if (string.IsNullOrEmpty(authHeader))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing authorization header"));
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid authorization scheme"));
        }

        var token = authHeader["Bearer ".Length..];
        if (!string.Equals(token, _expectedToken, StringComparison.Ordinal))
        {
            logger.LogWarning("Invalid agent token from {Peer}", context.Peer);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));
        }
    }
}
