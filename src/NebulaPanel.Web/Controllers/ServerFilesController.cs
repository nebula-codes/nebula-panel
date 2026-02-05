using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Web.Controllers;

/// <summary>
/// API controller for managing game server files.
/// </summary>
[ApiController]
[Route("api/servers/{serverId:guid}/files")]
[IgnoreAntiforgeryToken]
[Authorize]
public class ServerFilesController(
    IFileExplorerService fileService,
    IGameServerService serverService) : ControllerBase
{
    private readonly IFileExplorerService _fileService = fileService;
    private readonly IGameServerService _serverService = serverService;

    /// <summary>
    /// Gets the contents of a directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Relative path to the directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Directory contents.</returns>
    [HttpGet]
    public async Task<ActionResult<FileSystemEntryDto>> GetDirectory(
        Guid serverId,
        [FromQuery] string path = "",
        CancellationToken ct = default)
    {
        if (!await HasAccessAsync(serverId, ct))
        {
            return Forbid();
        }

        var result = await _fileService.GetDirectoryContentsAsync(serverId, path, ct);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = result.Error });
            }
            if (result.Error!.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Reads a text file.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Relative path to the file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>File content and metadata.</returns>
    [HttpGet("content")]
    public async Task<ActionResult<ReadFileResponse>> ReadFile(
        Guid serverId,
        [FromQuery] string path,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return BadRequest(new { error = "Path is required." });
        }

        if (!await HasAccessAsync(serverId, ct))
        {
            return Forbid();
        }

        var result = await _fileService.ReadFileAsync(serverId, path, ct);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = result.Error });
            }
            if (result.Error!.Contains("too large", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = result.Error });
            }
            if (result.Error!.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Writes content to a text file.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="request">Write request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPut("content")]
    public async Task<ActionResult> WriteFile(
        Guid serverId,
        [FromBody] WriteFileRequest request,
        CancellationToken ct = default)
    {
        if (!await HasAccessAsync(serverId, ct))
        {
            return Forbid();
        }

        var result = await _fileService.WriteFileAsync(serverId, request, ct);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = result.Error });
            }
            if (result.Error!.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Downloads a file.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Relative path to the file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>File content stream.</returns>
    [HttpGet("download")]
    public async Task<ActionResult> DownloadFile(
        Guid serverId,
        [FromQuery] string path,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return BadRequest(new { error = "Path is required." });
        }

        if (!await HasAccessAsync(serverId, ct))
        {
            return Forbid();
        }

        var result = await _fileService.DownloadFileAsync(serverId, path, ct);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = result.Error });
            }
            if (result.Error!.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            return BadRequest(new { error = result.Error });
        }

        var download = result.Value!;
        return File(download.Content, download.MimeType, download.FileName);
    }

    /// <summary>
    /// Uploads files.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Target directory path.</param>
    /// <param name="files">Files to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Upload results.</returns>
    [HttpPost("upload")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB
    public async Task<ActionResult<BulkUploadResult>> UploadFiles(
        Guid serverId,
        [FromQuery] string path = "",
        [FromForm] IFormFileCollection files = null!,
        CancellationToken ct = default)
    {
        if (files is null || files.Count == 0)
        {
            return BadRequest(new { error = "No files provided." });
        }

        if (!await HasAccessAsync(serverId, ct))
        {
            return Forbid();
        }

        var fileData = files.Select(f => (f.FileName, f.OpenReadStream(), f.Length));
        var result = await _fileService.UploadFilesAsync(serverId, path, fileData, ct);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Deletes a file or directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Relative path to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete]
    public async Task<ActionResult> Delete(
        Guid serverId,
        [FromQuery] string path,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return BadRequest(new { error = "Path is required." });
        }

        if (!await HasAccessAsync(serverId, ct))
        {
            return Forbid();
        }

        var result = await _fileService.DeleteAsync(serverId, path, ct);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = result.Error });
            }
            if (result.Error!.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Renames a file or directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="request">Rename request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("rename")]
    public async Task<ActionResult> Rename(
        Guid serverId,
        [FromBody] RenameRequest request,
        CancellationToken ct = default)
    {
        if (!await HasAccessAsync(serverId, ct))
        {
            return Forbid();
        }

        var result = await _fileService.RenameAsync(serverId, request, ct);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = result.Error });
            }
            if (result.Error!.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = result.Error });
            }
            if (result.Error!.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Creates a new directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="request">Create directory request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Created on success.</returns>
    [HttpPost("directory")]
    public async Task<ActionResult> CreateDirectory(
        Guid serverId,
        [FromBody] CreateDirectoryRequest request,
        CancellationToken ct = default)
    {
        if (!await HasAccessAsync(serverId, ct))
        {
            return Forbid();
        }

        var result = await _fileService.CreateDirectoryAsync(serverId, request, ct);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = result.Error });
            }
            if (result.Error!.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            return BadRequest(new { error = result.Error });
        }

        return Created();
    }

    /// <summary>
    /// Creates a ZIP archive of a file or directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Path to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Archive file download.</returns>
    [HttpPost("archive")]
    public async Task<ActionResult> CreateArchive(
        Guid serverId,
        [FromQuery] string path,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return BadRequest(new { error = "Path is required." });
        }

        if (!await HasAccessAsync(serverId, ct))
        {
            return Forbid();
        }

        var result = await _fileService.CreateArchiveAsync(serverId, path, ct);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = result.Error });
            }
            if (result.Error!.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            return BadRequest(new { error = result.Error });
        }

        var download = result.Value!;
        return File(download.Content, download.MimeType, download.FileName);
    }

    /// <summary>
    /// Extracts an archive to a directory.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="path">Target directory path.</param>
    /// <param name="archive">Archive file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("extract")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB
    public async Task<ActionResult> ExtractArchive(
        Guid serverId,
        [FromQuery] string path = "",
        [FromForm] IFormFile? archive = null,
        CancellationToken ct = default)
    {
        if (archive is null)
        {
            return BadRequest(new { error = "Archive file is required." });
        }

        if (!await HasAccessAsync(serverId, ct))
        {
            return Forbid();
        }

        await using var stream = archive.OpenReadStream();
        var result = await _fileService.ExtractArchiveAsync(serverId, path, stream, ct);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    #region Private Methods

    /// <summary>
    /// Checks if the current user has access to the server.
    /// For now, checks if user is the owner. Can be extended for permissions.
    /// </summary>
    private async Task<bool> HasAccessAsync(Guid serverId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return false;
        }

        var serverResult = await _serverService.GetServerByIdAsync(serverId, ct);
        if (serverResult.IsFailure || serverResult.Value is null)
        {
            return false;
        }

        // Owner always has access
        if (serverResult.Value.OwnerId == userId)
        {
            return true;
        }

        // TODO: Check for additional permissions (servers.{id}.files)
        return false;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    #endregion
}
