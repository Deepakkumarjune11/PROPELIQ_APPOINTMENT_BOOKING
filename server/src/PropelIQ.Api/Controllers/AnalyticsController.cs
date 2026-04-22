using Admin.Application.Analytics;
using ClinicalIntelligence.Application.AI.Availability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Operational metrics API for the analytics dashboard (US_033, SCR-028, FR-018, TR-017).
///
/// All endpoints require Staff or Admin role — no PHI is returned in any response
/// (OWASP A01 + HIPAA: aggregate counts, rates, and latencies only).
///
/// Endpoints:
///   GET  /api/v1/analytics/kpi           — KPI snapshot (AC-1)
///   GET  /api/v1/analytics/trends        — time-series trends (AC-3)
///   GET  /api/v1/analytics/system-health — live infrastructure health (AC-4)
///   GET  /api/v1/analytics/export        — PDF or CSV download (AC-5)
/// </summary>
[ApiController]
[Route("api/v1/analytics")]
[Authorize(Roles = "Staff,Admin")]
public sealed class AnalyticsController(
    IMetricsQueryService         metricsService,
    MetricsExportService         exportService,
    IAiAvailabilityState         aiAvailabilityState,
    ILogger<AnalyticsController> logger) : ControllerBase
{
    private static readonly int DefaultRangeDays = 30;

    // ── GET /api/v1/analytics/kpi ─────────────────────────────────────────

    /// <summary>Returns today's KPI snapshot for the given date range (AC-1).</summary>
    [HttpGet("kpi")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetKpi(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        var (start, end) = ResolveRange(startDate, endDate);
        logger.LogInformation(
            "Analytics KPI requested | start={Start} end={End}", start, end);

        var result = await metricsService.GetKpiAsync(start, end, ct).ConfigureAwait(false);
        return Ok(result);
    }

    // ── GET /api/v1/analytics/trends ──────────────────────────────────────

    /// <summary>Returns time-series trend data for the given date range (AC-3).</summary>
    [HttpGet("trends")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTrends(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        var (start, end) = ResolveRange(startDate, endDate);
        var result = await metricsService.GetTrendsAsync(start, end, ct).ConfigureAwait(false);
        return Ok(result);
    }

    // ── GET /api/v1/analytics/system-health ──────────────────────────────

    /// <summary>
    /// Returns live infrastructure health: API latency percentiles, DB pool usage,
    /// cache hit ratio, and AI gateway status (AC-4, TR-017).
    /// </summary>
    [HttpGet("system-health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSystemHealth(CancellationToken ct)
    {
        // IAiAvailabilityState is resolved here (PropelIQ.Api has transitive reference to
        // ClinicalIntelligence.Application via ClinicalIntelligence.Presentation) and passed
        // as a scalar to avoid a cross-module dependency in Admin.Application.
        var aiAvailable = aiAvailabilityState.IsAvailable;
        var result = await metricsService.GetSystemHealthAsync(aiAvailable, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    // ── GET /api/v1/analytics/export ─────────────────────────────────────

    /// <summary>
    /// Streams a PDF or CSV export of operational metrics for the given date range (AC-5).
    /// No PHI included — aggregate metrics only (OWASP A01 + HIPAA).
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export(
        [FromQuery] string    format,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken     ct)
    {
        // OWASP A03: Validate format against explicit allow-list — format value is NOT used
        // in a file path, but validation prevents unexpected content-type coercion.
        if (format is not ("pdf" or "csv"))
            return BadRequest(new { error = "format must be 'pdf' or 'csv'" });

        var (start, end) = ResolveRange(startDate, endDate);

        var kpi    = await metricsService.GetKpiAsync(start, end, ct).ConfigureAwait(false);
        var trends = await metricsService.GetTrendsAsync(start, end, ct).ConfigureAwait(false);

        if (format == "csv")
        {
            var csv = await exportService
                .GenerateCsvAsync(kpi, trends, start, end, ct)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Analytics CSV export generated | start={Start} end={End} bytes={Bytes}",
                start, end, csv.Length);

            return File(csv, "text/csv",
                $"propeliq-metrics-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
        }

        var pdf = await exportService
            .GeneratePdfAsync(kpi, trends, start, end, ct)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Analytics PDF export generated | start={Start} end={End} bytes={Bytes}",
            start, end, pdf.Length);

        return File(pdf, "application/pdf",
            $"propeliq-metrics-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static (DateOnly Start, DateOnly End) ResolveRange(
        DateOnly? startDate, DateOnly? endDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var end   = endDate   ?? today;
        var start = startDate ?? today.AddDays(-DefaultRangeDays);
        return (start, end);
    }
}
