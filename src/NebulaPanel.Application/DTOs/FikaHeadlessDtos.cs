using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

public record FikaParentServerDto(
    Guid Id,
    string Name,
    string InstallPath,
    int PrimaryPort,
    ServerStatus Status,
    string? InstalledVersion);

public record FikaHeadlessProfileDto(
    string ProfileId,
    string? Nickname,
    string? Edition,
    string? Side);
