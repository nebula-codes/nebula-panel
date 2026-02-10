using System.ComponentModel.DataAnnotations;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.DTOs;

public record NodeDto(
    Guid Id,
    string Name,
    string Address,
    string? AgentToken,
    NodeStatus Status,
    DateTime LastHeartbeat,
    ResourceLimits? Capacity,
    int ServerCount
);

public record NodeListItemDto(
    Guid Id,
    string Name,
    string Address,
    NodeStatus Status,
    DateTime LastHeartbeat,
    int ServerCount
);

public record CreateNodeRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, StringLength(500)] string Address,
    ResourceLimitsRequest? Capacity
);

public record UpdateNodeRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, StringLength(500)] string Address,
    ResourceLimitsRequest? Capacity
);
