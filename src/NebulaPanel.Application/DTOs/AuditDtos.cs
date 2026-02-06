using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

public record AuditEventDto(
    Guid Id,
    Guid? UserId,
    string? Username,
    SecurityEventType EventType,
    string? IpAddress,
    string? Details,
    bool Success,
    DateTime OccurredAt);

public record AuditLogQueryRequest(
    int Page = 1,
    int PageSize = 50,
    Guid? UserId = null,
    SecurityEventType? EventType = null,
    DateTime? From = null,
    DateTime? To = null,
    string? SearchTerm = null);
