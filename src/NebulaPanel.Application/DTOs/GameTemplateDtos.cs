using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

public record GameTemplateDto(
    string Slug,
    string Name,
    ExecutableType ExecutableType,
    bool SupportsDocker,
    bool SupportsMods,
    string? Author,
    string? Description,
    List<string> Tags,
    string TemplateVersion,
    DateTime CreatedAt);

public record GameTemplateExportRequest(
    string? Author = null,
    string? Description = null,
    List<string>? Tags = null);

public record GameTemplateValidationResult(
    bool IsValid,
    List<string> Errors);

public record CommunityTemplateListItemDto(
    string Slug,
    string Name,
    string? Author,
    string? Description,
    List<string> Tags,
    DateTime? UpdatedAt);
