using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

public class AlertRuleService(
    IAlertRuleRepository alertRuleRepository,
    IGameServerRepository serverRepository,
    ILogger<AlertRuleService> logger) : IAlertRuleService
{
    private readonly IAlertRuleRepository _alertRuleRepository = alertRuleRepository;
    private readonly IGameServerRepository _serverRepository = serverRepository;
    private readonly ILogger<AlertRuleService> _logger = logger;

    public async Task<IReadOnlyList<AlertRuleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _alertRuleRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return rules.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AlertRuleDto>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var rules = await _alertRuleRepository.GetByOwnerIdAsync(ownerId, cancellationToken).ConfigureAwait(false);
        return rules.Select(MapToDto).ToList();
    }

    public async Task<Result<AlertRuleDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _alertRuleRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (rule is null)
            return Result.Failure<AlertRuleDto>(Error.NotFound("Alert rule", id.ToString()));

        return MapToDto(rule);
    }

    public async Task<Result<AlertRuleDto>> CreateAsync(CreateAlertRuleRequest request, Guid ownerId, CancellationToken cancellationToken = default)
    {
        if (request.ServerId.HasValue)
        {
            var server = await _serverRepository.GetByIdAsync(request.ServerId.Value, cancellationToken).ConfigureAwait(false);
            if (server is null)
                return Result.Failure<AlertRuleDto>(Error.NotFound("Server", request.ServerId.Value.ToString()));
        }

        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            ServerId = request.ServerId,
            OwnerId = ownerId,
            Name = request.Name,
            ResourceType = request.ResourceType,
            Comparison = request.Comparison,
            Threshold = request.Threshold,
            DurationSeconds = request.DurationSeconds,
            CooldownMinutes = request.CooldownMinutes,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        await _alertRuleRepository.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created alert rule {RuleName} ({RuleId}) for owner {OwnerId}", rule.Name, rule.Id, ownerId);

        return MapToDto(rule);
    }

    public async Task<Result<AlertRuleDto>> UpdateAsync(Guid id, UpdateAlertRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await _alertRuleRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (rule is null)
            return Result.Failure<AlertRuleDto>(Error.NotFound("Alert rule", id.ToString()));

        rule.Name = request.Name;
        rule.ResourceType = request.ResourceType;
        rule.Comparison = request.Comparison;
        rule.Threshold = request.Threshold;
        rule.DurationSeconds = request.DurationSeconds;
        rule.IsEnabled = request.IsEnabled;
        rule.CooldownMinutes = request.CooldownMinutes;

        await _alertRuleRepository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Updated alert rule {RuleName} ({RuleId})", rule.Name, rule.Id);

        return MapToDto(rule);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _alertRuleRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (rule is null)
            return Result.Failure(Error.NotFound("Alert rule", id.ToString()));

        await _alertRuleRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Deleted alert rule {RuleName} ({RuleId})", rule.Name, rule.Id);

        return Result.Success();
    }

    private static AlertRuleDto MapToDto(AlertRule rule) => new(
        rule.Id,
        rule.ServerId,
        rule.Server?.Name,
        rule.OwnerId,
        rule.Name,
        rule.ResourceType,
        rule.Comparison,
        rule.Threshold,
        rule.DurationSeconds,
        rule.IsEnabled,
        rule.CooldownMinutes,
        rule.CreatedAt,
        rule.LastTriggeredAt);
}
