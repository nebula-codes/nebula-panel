using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Infrastructure.OfficialGames.FikaHeadless;

/// <summary>
/// Service for Fika Headless Client operations — discovering parent servers and headless profiles.
/// </summary>
public class FikaHeadlessService(
    IGameServerRepository serverRepository,
    IServerFileManager fileManager,
    ILogger<FikaHeadlessService> logger) : IFikaHeadlessService
{
    private readonly IGameServerRepository _serverRepository = serverRepository;
    private readonly IServerFileManager _fileManager = fileManager;
    private readonly ILogger<FikaHeadlessService> _logger = logger;

    public async Task<IReadOnlyList<FikaParentServerDto>> GetAvailableParentServersAsync(
        CancellationToken cancellationToken = default)
    {
        var allServers = await _serverRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return allServers
            .Where(s => s.Game.Slug == "fika-spt")
            .Select(s => new FikaParentServerDto(
                s.Id,
                s.Name,
                s.InstallPath,
                s.PrimaryPort,
                s.Status,
                s.InstalledVersion))
            .ToList();
    }

    public async Task<Result<IReadOnlyList<FikaHeadlessProfileDto>>> GetAvailableProfilesAsync(
        Guid parentServerId,
        CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(parentServerId, cancellationToken)
            .ConfigureAwait(false);

        if (server is null)
            return Result.Failure<IReadOnlyList<FikaHeadlessProfileDto>>(
                Error.NotFound("Server", parentServerId.ToString()));

        if (server.Game.Slug != "fika-spt")
            return Result.Failure<IReadOnlyList<FikaHeadlessProfileDto>>(
                Error.Validation("Selected server is not a Fika SPT server."));

        try
        {
            var profilesDir = await _fileManager.GetDirectoryContentsAsync(
                server, "SPT/user/profiles/", cancellationToken).ConfigureAwait(false);

            if (profilesDir.Children is null || profilesDir.Children.Count == 0)
                return Result.Success<IReadOnlyList<FikaHeadlessProfileDto>>([]);

            var profiles = new List<FikaHeadlessProfileDto>();

            foreach (var child in profilesDir.Children.Where(c =>
                c.Type == FileEntryType.File &&
                c.Extension?.Equals(".json", StringComparison.OrdinalIgnoreCase) == true))
            {
                try
                {
                    var content = await _fileManager.ReadFileAsync(server, child.Path, cancellationToken)
                        .ConfigureAwait(false);

                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    string? nickname = null;
                    string? edition = null;
                    string? side = null;

                    if (root.TryGetProperty("info", out var info))
                    {
                        if (info.TryGetProperty("nickname", out var nickProp))
                            nickname = nickProp.GetString();
                        if (info.TryGetProperty("edition", out var editionProp))
                            edition = editionProp.GetString();
                    }

                    if (root.TryGetProperty("characters", out var characters) &&
                        characters.TryGetProperty("pmc", out var pmc) &&
                        pmc.TryGetProperty("Info", out var pmcInfo) &&
                        pmcInfo.TryGetProperty("Side", out var sideProp))
                    {
                        side = sideProp.GetString();
                    }

                    var profileId = Path.GetFileNameWithoutExtension(child.Name);
                    profiles.Add(new FikaHeadlessProfileDto(profileId, nickname, edition, side));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse profile file {Path}", child.Path);
                }
            }

            return Result.Success<IReadOnlyList<FikaHeadlessProfileDto>>(
                profiles.OrderBy(p => p.Nickname).ToList());
        }
        catch (DirectoryNotFoundException)
        {
            return Result.Failure<IReadOnlyList<FikaHeadlessProfileDto>>(
                Error.Validation("Profiles directory not found. Make sure the parent server has been started at least once and has NUM_HEADLESS_PROFILES > 0."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read profiles from parent server {ServerId}", parentServerId);
            return Result.Failure<IReadOnlyList<FikaHeadlessProfileDto>>(
                Error.Validation($"Failed to read profiles: {ex.Message}"));
        }
    }
}
