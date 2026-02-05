namespace NebulaPanel.Web.Controllers;

using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NebulaPanel.Application.DTOs;

/// <summary>
/// API controller for detailed health check information.
/// The detailed endpoint requires admin authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController(HealthCheckService healthCheckService) : ControllerBase
{
    /// <summary>
    /// Gets a detailed health report with all check results.
    /// Requires admin role.
    /// </summary>
    [HttpGet("detailed")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DetailedHealthReportDto>> GetDetailedHealth(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = await healthCheckService.CheckHealthAsync(ct);
        stopwatch.Stop();

        var entries = report.Entries.Select(e => new HealthCheckEntryDto(
            Name: e.Key,
            Status: e.Value.Status.ToString(),
            Description: e.Value.Description,
            Duration: e.Value.Duration,
            Data: e.Value.Data?.Count > 0
                ? e.Value.Data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                : null
        )).ToList();

        var result = new DetailedHealthReportDto(
            Status: report.Status.ToString(),
            TotalDuration: stopwatch.Elapsed,
            Entries: entries,
            Timestamp: DateTime.UtcNow
        );

        return Ok(result);
    }

    /// <summary>
    /// Gets a simple health status (public endpoint, same as /health but at API path).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<HealthStatusDto>> GetHealth(CancellationToken ct)
    {
        var report = await healthCheckService.CheckHealthAsync(ct);

        var result = new HealthStatusDto(
            Status: report.Status.ToString(),
            Timestamp: DateTime.UtcNow
        );

        // Return 503 if unhealthy
        if (report.Status == HealthStatus.Unhealthy)
        {
            return StatusCode(503, result);
        }

        return Ok(result);
    }
}
