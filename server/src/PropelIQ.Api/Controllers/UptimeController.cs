using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Api.Infrastructure.Uptime;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Monthly uptime SLA reporting endpoint (US_034, AC-5).
/// Computes uptime % from Redis-persisted downtime intervals.
/// </summary>
[ApiController]
[Route("api/v1/admin/uptime")]
[Authorize(Roles = "Staff,Admin")]
public sealed class UptimeController(IUptimeTracker tracker) : ControllerBase
{
    // Allow-list prevents arbitrary Redis key segment injection (OWASP A03)
    private static readonly HashSet<string> AllowedServices =
        ["api", "postgresql", "redis", "azure-openai", "hangfire"];

    /// <summary>Returns the monthly uptime report for a given service, year, and month.</summary>
    [HttpGet("monthly")]
    [ProducesResponseType<UptimeReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonthlyUptime(
        [FromQuery] string service = "api",
        [FromQuery] int? year      = null,
        [FromQuery] int? month     = null,
        CancellationToken ct       = default)
    {
        var y = year  ?? DateTime.UtcNow.Year;
        var m = month ?? DateTime.UtcNow.Month;

        if (m < 1 || m > 12)
            return BadRequest(new { error = "month must be between 1 and 12" });

        // OWASP A03 — reject service values not in the fixed allow-list
        if (!AllowedServices.Contains(service))
            return BadRequest(new { error = "unknown service name" });

        var report = await tracker.GetMonthlyUptimeAsync(service, y, m, ct)
            .ConfigureAwait(false);
        return Ok(report);
    }
}
