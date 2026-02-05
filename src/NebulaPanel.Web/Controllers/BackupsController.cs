using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;

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
    public async Task<ActionResult<IReadOnlyList<BackupListItemDto>>> GetBackups(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var serverResult = await _serverService.GetServerByIdAsync(serverId, cancellationToken);
        if (serverResult.IsFailure)
        {
            return NotFound(new { error = serverResult.Error });
        }

        var backups = await _backupService.GetBackupsByServerIdAsync(serverId, cancellationToken);
        return Ok(backups);
    }

    [HttpGet("{backupId:guid}")]
    public async Task<ActionResult<BackupDto>> GetBackup(
        Guid serverId,
        Guid backupId,
        CancellationToken cancellationToken)
    {
        var result = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        // Verify backup belongs to the server
        if (result.Value!.ServerId != serverId)
        {
            return NotFound(new { error = "Backup not found for this server." });
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<BackupDto>> CreateBackup(
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

        if (result.IsFailure)
        {
            if (result.Error!.Contains("not found"))
            {
                return NotFound(new { error = result.Error });
            }
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(
            nameof(GetBackup),
            new { serverId, backupId = result.Value!.Id },
            result.Value);
    }

    [HttpPost("{backupId:guid}/restore")]
    public async Task<ActionResult> RestoreBackup(
        Guid serverId,
        Guid backupId,
        [FromQuery] bool stopServer = true,
        [FromQuery] bool createPreRestoreBackup = true,
        CancellationToken cancellationToken = default)
    {
        // Verify backup exists and belongs to server
        var backupResult = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);
        if (backupResult.IsFailure)
        {
            return NotFound(new { error = backupResult.Error });
        }

        if (backupResult.Value!.ServerId != serverId)
        {
            return NotFound(new { error = "Backup not found for this server." });
        }

        var result = await _backupService.RestoreBackupAsync(
            backupId,
            stopServer,
            createPreRestoreBackup,
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { message = "Backup restored successfully." });
    }

    [HttpGet("{backupId:guid}/download")]
    public async Task<ActionResult> DownloadBackup(
        Guid serverId,
        Guid backupId,
        CancellationToken cancellationToken)
    {
        // Verify backup exists and belongs to server
        var backupResult = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);
        if (backupResult.IsFailure)
        {
            return NotFound(new { error = backupResult.Error });
        }

        if (backupResult.Value!.ServerId != serverId)
        {
            return NotFound(new { error = "Backup not found for this server." });
        }

        var streamResult = await _backupService.DownloadBackupAsync(backupId, cancellationToken);
        if (streamResult.IsFailure)
        {
            return NotFound(new { error = streamResult.Error });
        }

        var fileName = Path.GetFileName(backupResult.Value.FilePath);
        return File(streamResult.Value!, "application/zip", fileName);
    }

    [HttpGet("{backupId:guid}/contents")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetBackupContents(
        Guid serverId,
        Guid backupId,
        CancellationToken cancellationToken)
    {
        // Verify backup exists and belongs to server
        var backupResult = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);
        if (backupResult.IsFailure)
        {
            return NotFound(new { error = backupResult.Error });
        }

        if (backupResult.Value!.ServerId != serverId)
        {
            return NotFound(new { error = "Backup not found for this server." });
        }

        var result = await _backupService.GetBackupContentsAsync(backupId, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{backupId:guid}")]
    public async Task<ActionResult> DeleteBackup(
        Guid serverId,
        Guid backupId,
        CancellationToken cancellationToken)
    {
        // Verify backup exists and belongs to server
        var backupResult = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);
        if (backupResult.IsFailure)
        {
            return NotFound(new { error = backupResult.Error });
        }

        if (backupResult.Value!.ServerId != serverId)
        {
            return NotFound(new { error = "Backup not found for this server." });
        }

        var result = await _backupService.DeleteBackupAsync(backupId, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<BackupSummaryDto>> GetSummary(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var serverResult = await _serverService.GetServerByIdAsync(serverId, cancellationToken);
        if (serverResult.IsFailure)
        {
            return NotFound(new { error = serverResult.Error });
        }

        var summary = await _backupService.GetServerBackupSummaryAsync(serverId, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("retention")]
    public async Task<ActionResult> ApplyRetention(
        Guid serverId,
        [FromQuery] int keepCount,
        CancellationToken cancellationToken)
    {
        if (keepCount < 1)
        {
            return BadRequest(new { error = "Keep count must be at least 1." });
        }

        var serverResult = await _serverService.GetServerByIdAsync(serverId, cancellationToken);
        if (serverResult.IsFailure)
        {
            return NotFound(new { error = serverResult.Error });
        }

        var result = await _backupService.ApplyRetentionPolicyAsync(serverId, keepCount, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

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
    public async Task<ActionResult<BackupDto>> GetBackup(Guid backupId, CancellationToken cancellationToken)
    {
        var result = await _backupService.GetBackupByIdAsync(backupId, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
