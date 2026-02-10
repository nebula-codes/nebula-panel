using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

public interface INodeService
{
    Task<IReadOnlyList<NodeListItemDto>> GetAllNodesAsync(CancellationToken cancellationToken = default);
    Task<Result<NodeDto>> GetNodeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<NodeDto>> CreateNodeAsync(CreateNodeRequest request, CancellationToken cancellationToken = default);
    Task<Result<NodeDto>> UpdateNodeAsync(Guid id, UpdateNodeRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteNodeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<string>> RegenerateTokenAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> DrainNodeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> UndrainNodeAsync(Guid id, CancellationToken cancellationToken = default);
}

public class NodeService(
    INodeRepository nodeRepository,
    ILogger<NodeService> logger) : INodeService
{
    private readonly INodeRepository _nodeRepository = nodeRepository;
    private readonly ILogger<NodeService> _logger = logger;

    public async Task<IReadOnlyList<NodeListItemDto>> GetAllNodesAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _nodeRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return nodes.Select(MapToListItemDto).ToList();
    }

    public async Task<Result<NodeDto>> GetNodeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await _nodeRepository.GetByIdWithServersAsync(id, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return Result.Failure<NodeDto>(Error.NotFound("Node", id.ToString()));
        }

        return MapToDto(node);
    }

    public async Task<Result<NodeDto>> CreateNodeAsync(CreateNodeRequest request, CancellationToken cancellationToken = default)
    {
        if (await _nodeRepository.NameExistsAsync(request.Name, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<NodeDto>(Error.AlreadyExists("Node", request.Name));
        }

        var node = new Node
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Address = request.Address,
            AgentToken = GenerateAgentToken(),
            Status = NodeStatus.Offline,
            LastHeartbeat = DateTime.MinValue,
            Capacity = request.Capacity is not null ? new ResourceLimits
            {
                MaxMemoryMb = request.Capacity.MaxMemoryMb,
                MaxCpuPercent = request.Capacity.MaxCpuPercent,
                MaxDiskMb = request.Capacity.MaxDiskMb,
                MaxNetworkMbps = request.Capacity.MaxNetworkMbps
            } : null
        };

        await _nodeRepository.AddAsync(node, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created node {NodeName} with ID {NodeId}", node.Name, node.Id);

        return MapToDto(node);
    }

    public async Task<Result<NodeDto>> UpdateNodeAsync(Guid id, UpdateNodeRequest request, CancellationToken cancellationToken = default)
    {
        var node = await _nodeRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return Result.Failure<NodeDto>(Error.NotFound("Node", id.ToString()));
        }

        if (await _nodeRepository.NameExistsAsync(request.Name, id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<NodeDto>(Error.AlreadyExists("Node", request.Name));
        }

        node.Name = request.Name;
        node.Address = request.Address;
        node.Capacity = request.Capacity is not null ? new ResourceLimits
        {
            MaxMemoryMb = request.Capacity.MaxMemoryMb,
            MaxCpuPercent = request.Capacity.MaxCpuPercent,
            MaxDiskMb = request.Capacity.MaxDiskMb,
            MaxNetworkMbps = request.Capacity.MaxNetworkMbps
        } : node.Capacity;

        await _nodeRepository.UpdateAsync(node, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Updated node {NodeName} with ID {NodeId}", node.Name, node.Id);

        return MapToDto(node);
    }

    public async Task<Result> DeleteNodeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await _nodeRepository.GetByIdWithServersAsync(id, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return Result.Failure(Error.NotFound("Node", id.ToString()));
        }

        if (node.Servers.Count > 0)
        {
            return Result.Failure(Error.InvalidOperation(
                $"Cannot delete node '{node.Name}': {node.Servers.Count} server(s) are still assigned. Reassign or remove them first."));
        }

        await _nodeRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Deleted node {NodeName} with ID {NodeId}", node.Name, node.Id);

        return Result.Success();
    }

    public async Task<Result<string>> RegenerateTokenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await _nodeRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return Result.Failure<string>(Error.NotFound("Node", id.ToString()));
        }

        node.AgentToken = GenerateAgentToken();
        await _nodeRepository.UpdateAsync(node, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Regenerated agent token for node {NodeName} ({NodeId})", node.Name, node.Id);
        return Result.Success(node.AgentToken);
    }

    public async Task<Result> DrainNodeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await _nodeRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return Result.Failure(Error.NotFound("Node", id.ToString()));
        }

        if (node.Status == NodeStatus.Draining)
        {
            return Result.Failure(Error.InvalidOperation("Node is already draining."));
        }

        if (node.Status != NodeStatus.Online)
        {
            return Result.Failure(Error.InvalidOperation("Only online nodes can be drained."));
        }

        node.Status = NodeStatus.Draining;
        await _nodeRepository.UpdateAsync(node, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Node {NodeName} ({NodeId}) set to draining", node.Name, node.Id);

        return Result.Success();
    }

    public async Task<Result> UndrainNodeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await _nodeRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return Result.Failure(Error.NotFound("Node", id.ToString()));
        }

        if (node.Status != NodeStatus.Draining)
        {
            return Result.Failure(Error.InvalidOperation("Only draining nodes can be undrained."));
        }

        node.Status = NodeStatus.Online;
        await _nodeRepository.UpdateAsync(node, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Node {NodeName} ({NodeId}) undrained, set back to online", node.Name, node.Id);

        return Result.Success();
    }

    private static string GenerateAgentToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static NodeDto MapToDto(Node node) => new(
        node.Id,
        node.Name,
        node.Address,
        node.AgentToken,
        node.Status,
        node.LastHeartbeat,
        node.Capacity,
        node.Servers.Count
    );

    private static NodeListItemDto MapToListItemDto(Node node) => new(
        node.Id,
        node.Name,
        node.Address,
        node.Status,
        node.LastHeartbeat,
        node.Servers.Count
    );
}
