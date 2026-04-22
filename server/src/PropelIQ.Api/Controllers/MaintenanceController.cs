using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PropelIQ.Api.Infrastructure.Maintenance;
using PropelIQ.Api.Infrastructure.Uptime;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Admin-only maintenance mode toggle endpoints (US_034, AC-4).
///
/// Security (OWASP A01): activate/deactivate require <c>Admin</c> role.
/// <c>GET status</c> is <c>[AllowAnonymous]</c> — response contains no PHI or secrets,
/// only operational timing data needed by front-end maintenance banners.
/// Security (OWASP A09): all mutating operations log the calling admin identity.
/// </summary>
[ApiController]
[Route("api/v1/admin/maintenance")]
[Authorize(Roles = "Admin")]
public sealed class MaintenanceController(
    IMaintenanceModeService maintenance,
    IUptimeTracker uptime,
    ILogger<MaintenanceController> logger) : ControllerBase
{
    /// <summary>Activates maintenance mode; blocks all non-exempt API requests with HTTP 503.</summary>
    [HttpPost("activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(
        [FromBody] ActivateMaintenanceRequest request, CancellationToken ct)
    {
        var estimatedMinutes = Math.Clamp(request.EstimatedMinutes, 1, 480); // max 8 hours
        await maintenance.ActivateAsync(estimatedMinutes, ct).ConfigureAwait(false);
        await uptime.RecordDowntimeStartAsync("api", ct).ConfigureAwait(false);

        logger.LogWarning(
            "Maintenance mode ACTIVATED by {User}; estimated duration: {Minutes} min",
            User.Identity?.Name, estimatedMinutes);

        return Ok(new { message = "Maintenance mode activated", estimatedMinutes });
    }

    /// <summary>Deactivates maintenance mode; API resumes serving requests.</summary>
    [HttpPost("deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(CancellationToken ct)
    {
        await maintenance.DeactivateAsync(ct).ConfigureAwait(false);
        await uptime.RecordDowntimeEndAsync("api", ct).ConfigureAwait(false);

        logger.LogInformation(
            "Maintenance mode DEACTIVATED by {User}", User.Identity?.Name);

        return Ok(new { message = "Maintenance mode deactivated" });
    }

    /// <summary>Returns the current maintenance mode state. Public — no auth required for status banner display.</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType<MaintenanceStatus>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await maintenance.GetStatusAsync(ct).ConfigureAwait(false);
        return Ok(status);
    }
}

/// <summary>Request body for <c>POST /api/v1/admin/maintenance/activate</c>.</summary>
public sealed record ActivateMaintenanceRequest(int EstimatedMinutes);
