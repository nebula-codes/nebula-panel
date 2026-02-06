using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

public record AlertRuleDto(
    Guid Id,
    Guid? ServerId,
    string? ServerName,
    Guid OwnerId,
    string Name,
    AlertResourceType ResourceType,
    AlertComparison Comparison,
    double Threshold,
    int DurationSeconds,
    bool IsEnabled,
    int CooldownMinutes,
    DateTime CreatedAt,
    DateTime? LastTriggeredAt);

public record CreateAlertRuleRequest(
    Guid? ServerId,
    string Name,
    AlertResourceType ResourceType,
    AlertComparison Comparison,
    double Threshold,
    int DurationSeconds = 60,
    int CooldownMinutes = 15);

public record UpdateAlertRuleRequest(
    string Name,
    AlertResourceType ResourceType,
    AlertComparison Comparison,
    double Threshold,
    int DurationSeconds,
    bool IsEnabled,
    int CooldownMinutes);
