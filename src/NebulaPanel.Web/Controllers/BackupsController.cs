using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Web.Extensions;

namespace NebulaPanel.Web.Controllers;

[ApiController]
[Route("api/servers/{serverId:guid}/backups")]
[IgnoreAntiforgeryToken]
[Authorize]
public class BackupsController(IBackupService backupService, IGameServerService serverService) : ControllerBase
{
    private readonly IBackupService _backupService = backupService;
    private readonly IGameServerService _serverService = serverService;

    [HttpGet]
    public async Task<IActionResult> GetBackups(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var serverResult = await _serverService.GetServerByIdAsync(serverId, cancellationToken);
        if (serverResult.IsFailure)
            return serverResult.ToActionResult();

        var backups = await _backupService.GetBackupsByServerIdAsync(serverId, cancellationToken);
        return Ok(backups);
    }

    [HttpGet("{backupId:guid}")]
    public async Task<IActionResult> GetBackup(
        Guid serverId,
        Guid backupId,
        CancellationToken cancellationToken)
    {
        var result = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);

        if (result.IsFailure)
            return result.ToActionResult();

        if (result.Value!.ServerId != serverId)
            return NotFound(new { error = "Backup not found for this server." });

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBackup(
        Guid serverId,
        [FromBody] CreateBackupRequest request,
        CancellationToken cancellationToken)
    {
        // Ensure the request is for this server
        if (request.ServerId != serverId)
        {
            return BadRequest(new { error = "Server ID in request does not match route." });
        }

        var result = await _backupService.CreateBackupAsync(request, BackupType.Manual, cancellationToken: cancellationToken);
        return result.ToCreatedResult();
    }

    [HttpPost("{backupId:guid}/restore")]
    public async Task<IActionResult> RestoreBackup(
        Guid serverId,
        Guid backupId,
        [FromQuery] bool stopServer = true,
        [FromQuery] bool createPreRestoreBackup = true,
        CancellationToken cancellationToken = default)
    {
        var backupResult = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);
        if (backupResult.IsFailure)
            return backupResult.ToActionResult();

        if (backupResult.Value!.ServerId != serverId)
            return NotFound(new { error = "Backup not found for this server." });

        var result = await _backupService.RestoreBackupAsync(
            backupId,
            stopServer,
            createPreRestoreBackup,
            cancellationToken);

        if (result.IsFailure)
            return result.ToActionResult();

        return Ok(new { message = "Backup restored successfully." });
    }

    [HttpGet("{backupId:guid}/download")]
    public async Task<IActionResult> DownloadBackup(
        Guid serverId,
        Guid backupId,
        CancellationToken cancellationToken)
    {
        var backupResult = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);
        if (backupResult.IsFailure)
            return backupResult.ToActionResult();

        if (backupResult.Value!.ServerId != serverId)
            return NotFound(new { error = "Backup not found for this server." });

        var streamResult = await _backupService.DownloadBackupAsync(backupId, cancellationToken);
        if (streamResult.IsFailure)
            return streamResult.ToActionResult();

        var fileName = Path.GetFileName(backupResult.Value.FilePath);
        return File(streamResult.Value!, "application/zip", fileName);
    }

    [HttpGet("{backupId:guid}/contents")]
    public async Task<IActionResult> GetBackupContents(
        Guid serverId,
        Guid backupId,
        CancellationToken cancellationToken)
    {
        var backupResult = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);
        if (backupResult.IsFailure)
            return backupResult.ToActionResult();

        if (backupResult.Value!.ServerId != serverId)
            return NotFound(new { error = "Backup not found for this server." });

        var result = await _backupService.GetBackupContentsAsync(backupId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{backupId:guid}")]
    public async Task<IActionResult> DeleteBackup(
        Guid serverId,
        Guid backupId,
        CancellationToken cancellationToken)
    {
        var backupResult = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);
        if (backupResult.IsFailure)
            return backupResult.ToActionResult();

        if (backupResult.Value!.ServerId != serverId)
            return NotFound(new { error = "Backup not found for this server." });

        var result = await _backupService.DeleteBackupAsync(backupId, cancellationToken);
        return result.ToNoContentResult();
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var serverResult = await _serverService.GetServerByIdAsync(serverId, cancellationToken);
        if (serverResult.IsFailure)
            return serverResult.ToActionResult();

        var summary = await _backupService.GetServerBackupSummaryAsync(serverId, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("retention")]
    public async Task<IActionResult> ApplyRetention(
        Guid serverId,
        [FromQuery] int keepCount,
        CancellationToken cancellationToken)
    {
        if (keepCount < 1)
            return BadRequest(new { error = "Keep count must be at least 1." });

        var serverResult = await _serverService.GetServerByIdAsync(serverId, cancellationToken);
        if (serverResult.IsFailure)
            return serverResult.ToActionResult();

        var result = await _backupService.ApplyRetentionPolicyAsync(serverId, keepCount, cancellationToken);
        if (result.IsFailure)
            return result.ToActionResult();

        return Ok(new { deletedCount = result.Value, message = $"Deleted {result.Value} old backups." });
    }
}

/// <summary>
/// Controller for global backup operations (across all servers).
/// </summary>
[ApiController]
[Route("api/backups")]
[IgnoreAntiforgeryToken]
[Authorize]
public class GlobalBackupsController(IBackupService backupService) : ControllerBase
{
    private readonly IBackupService _backupService = backupService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BackupListItemDto>>> GetAllBackups(CancellationToken cancellationToken)
    {
        var backups = await _backupService.GetAllBackupsAsync(cancellationToken);
        return Ok(backups);
    }

    [HttpGet("{backupId:guid}")]
    public async Task<IActionResult> GetBackup(Guid backupId, CancellationToken cancellationToken)
    {
        var result = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);
        return result.ToActionResult();
    }
}
