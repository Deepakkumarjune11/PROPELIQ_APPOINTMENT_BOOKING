using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.DTOs;
using PatientAccess.Data;

namespace PatientAccess.Presentation.Controllers;

/// <summary>
/// Read-only compliance endpoint for audit log retrieval (US_026, AC-3).
/// Restricted to Admin role — non-Admin requests return 403 (OWASP A01).
/// </summary>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = "Admin")]
public sealed class AuditLogController(PropelIQDbContext db) : ControllerBase
{
    /// <summary>
    /// Returns a paginated, filtered view of the immutable audit log (AC-3, DR-008).
    /// Optimised with <c>AsNoTracking()</c> and composite indexes for sub-2s response
    /// on 1M rows (NFR-013).
    /// </summary>
    /// <param name="dateFrom">Inclusive lower bound on <c>OccurredAt</c> (UTC).</param>
    /// <param name="dateTo">Inclusive upper bound on <c>OccurredAt</c> (UTC).</param>
    /// <param name="actorId">Filter by actor (Staff/Admin) Guid.</param>
    /// <param name="actionType">Filter by action type string (e.g., "UserLogin").</param>
    /// <param name="entityType">Filter by target entity type (e.g., "Patient").</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Page size clamped to [1, 100] to prevent resource exhaustion (OWASP A01).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogPagedResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] DateTime?  dateFrom   = null,
        [FromQuery] DateTime?  dateTo     = null,
        [FromQuery] Guid?      actorId    = null,
        [FromQuery] string?    actionType = null,
        [FromQuery] string?    entityType = null,
        [FromQuery] int        page       = 1,
        [FromQuery] int        pageSize   = 50,
        CancellationToken ct = default)
    {
        // Clamp page size to prevent resource exhaustion (OWASP A01 — unrestricted resource consumption).
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

        var query = db.AuditLogs.AsNoTracking();

        if (dateFrom.HasValue)
            query = query.Where(a => a.OccurredAt >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(a => a.OccurredAt <= dateTo.Value);
        if (actorId.HasValue)
            query = query.Where(a => a.ActorId == actorId.Value);
        if (!string.IsNullOrWhiteSpace(actionType))
            query = query.Where(a => a.ActionType.ToString() == actionType);
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.TargetEntityType == entityType);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id,
                a.ActorType.ToString(),
                a.ActorId,
                a.ActionType.ToString(),
                a.TargetEntityType,
                a.TargetEntityId,
                a.IpAddress,
                a.OldValues,
                a.NewValues,
                a.OccurredAt,       // exposed as CreatedAt in the DTO
                a.ChainHash))
            .ToListAsync(ct);

        return Ok(new AuditLogPagedResult(
            items,
            totalCount,
            page,
            pageSize,
            (int)Math.Ceiling(totalCount / (double)pageSize)));
    }
}
