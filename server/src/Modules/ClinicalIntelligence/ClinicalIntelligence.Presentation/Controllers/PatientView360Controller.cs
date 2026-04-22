using System.Security.Claims;
using System.Text.Json;
using ClinicalIntelligence.Application.Documents.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatientAccess.Data;
using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Presentation.Controllers;

/// <summary>
/// Staff-facing API for the 360-degree patient clinical view (US_021, FR-015).
///
/// Endpoints:
/// <list type="bullet">
///   <item><c>GET api/v1/patients/{patientId}/360-view</c> — returns the assembled view for a patient.</item>
///   <item><c>GET api/v1/facts/{factId}/source</c> — returns the source-text citation for one fact.</item>
///   <item><c>GET api/v1/staff/verification-queue</c> — returns patients awaiting clinical verification.</item>
/// </list>
///
/// All endpoints require the <c>Staff</c> role (AC-1).
/// AuditLog entries are written without PHI values (AIR-S03).
/// </summary>
[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1")]
public sealed class PatientView360Controller(
    PropelIQDbContext           db,
    IDataProtectionProvider     dataProtectionProvider,
    ILogger<PatientView360Controller> logger)
    : ControllerBase
{
    private readonly PropelIQDbContext       _db                     = db;
    private readonly IDataProtectionProvider _dataProtectionProvider = dataProtectionProvider;
    private readonly ILogger<PatientView360Controller> _logger       = logger;

    // ── GET api/v1/patients/{patientId}/360-view ──────────────────────────

    /// <summary>
    /// Returns the assembled 360-degree clinical view for the specified patient.
    /// Returns 404 when no view exists yet (e.g., documents still processing).
    /// </summary>
    [HttpGet("patients/{patientId:guid}/360-view")]
    [ProducesResponseType(typeof(PatientView360ResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientView360(
        Guid              patientId,
        CancellationToken ct)
    {
        var staffId = GetStaffId();

        var view360 = await _db.PatientViews360
            .Include(v => v.Patient)
            .FirstOrDefaultAsync(v => v.PatientId == patientId, ct);

        if (view360 is null)
            return NotFound(new { message = "360-view not yet assembled for this patient." });

        var patient = view360.Patient;

        // Decrypt consolidated facts blob (DR-015) — PHI only in memory
        List<ConsolidatedFactEntry> consolidated;
        try
        {
            var protector = _dataProtectionProvider.CreateProtector("PropelIQ.PatientView360.ConsolidatedFacts");
            var json      = protector.Unprotect(view360.ConsolidatedFacts);
            consolidated  = JsonSerializer.Deserialize<List<ConsolidatedFactEntry>>(json) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PatientView360Controller: failed to decrypt 360-view for patient {PatientId}.", patientId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Unable to decrypt patient view. Please contact support." });
        }

        // Group facts by category (FactType) for frontend consumption
        var factsByCategory = consolidated
            .GroupBy(f => f.FactType)
            .ToDictionary(g => g.Key, g => g.ToList());

        var documentCount = await _db.ClinicalDocuments
            .CountAsync(d => d.PatientId == patientId && !d.IsDeleted, ct);

        // AuditLog — no PHI values (AIR-S03)
        _db.AuditLogs.Add(new PatientAccess.Data.Entities.AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = staffId,
            ActorType      = PatientAccess.Domain.Enums.AuditActorType.Staff,
            ActionType     = PatientAccess.Domain.Enums.AuditActionType.PatientDataAccess,
            TargetEntityId = patientId,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(new
            {
                action    = "PatientView360Viewed",
                FactCount = consolidated.Count,
            }),
        });
        await _db.SaveChangesAsync(ct);

        var response = new PatientView360ResponseDto(
            patientId,
            new PatientIdentityDto(
                patient.Name,
                patient.Dob.ToString("yyyy-MM-dd"),
                patient.InsuranceMemberId,
                patient.InsuranceProvider),
            view360.ConflictFlags.Length,
            factsByCategory,
            view360.VerificationStatus.ToString(),
            documentCount,
            view360.LastUpdated);

        return Ok(response);
    }

    // ── GET api/v1/facts/{factId}/source ──────────────────────────────────

    /// <summary>
    /// Returns the source-text citation for a single extracted fact (AIR-006).
    /// </summary>
    [HttpGet("facts/{factId:guid}/source")]
    [ProducesResponseType(typeof(SourceCitationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFactSource(Guid factId, CancellationToken ct)
    {
        var fact = await _db.ExtractedFacts
            .IgnoreQueryFilters()
            .Include(f => f.Document)
            .FirstOrDefaultAsync(f => f.Id == factId, ct);

        if (fact is null)
            return NotFound();

        // Locate the chunk that contains the source character offset for citation display.
        // If offset is not set or no matching chunk exists, return an empty source text (AIR-006).
        string sourceText = string.Empty;
        if (fact.SourceCharOffset.HasValue && fact.SourceCharLength.HasValue)
        {
            var chunks = await _db.DocumentChunkEmbeddings
                .Where(c => c.DocumentId == fact.DocumentId)
                .OrderBy(c => c.ChunkIndex)
                .Select(c => new { c.ChunkText })
                .ToListAsync(ct);

            // Find the chunk whose accumulated text covers the source offset
            int accumulated = 0;
            foreach (var chunk in chunks)
            {
                var chunkLen = chunk.ChunkText.Length;
                if (fact.SourceCharOffset.Value < accumulated + chunkLen)
                {
                    var localOffset = Math.Max(0, fact.SourceCharOffset.Value - accumulated);
                    var localEnd    = Math.Min(localOffset + fact.SourceCharLength.Value, chunkLen);
                    sourceText = chunk.ChunkText[localOffset..localEnd];
                    break;
                }
                accumulated += chunkLen;
            }
        }

        var dto = new SourceCitationDto(
            fact.Id,
            fact.DocumentId,
            fact.Document.OriginalFileName,
            fact.Document.UploadedAt.ToString("o"),
            sourceText,
            fact.SourceCharOffset,
            fact.SourceCharLength,
            fact.ConfidenceScore);

        return Ok(dto);
    }

    // ── GET api/v1/staff/verification-queue ──────────────────────────────

    /// <summary>
    /// Returns the list of patients whose 360-views are pending clinical staff verification
    /// (US_021, SCR-016). Patients are ordered by next appointment time (soonest first).
    /// </summary>
    [HttpGet("staff/verification-queue")]
    [ProducesResponseType(typeof(IReadOnlyList<VerificationQueueEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVerificationQueue(CancellationToken ct)
    {
        // Fetch pending/in-review views with patient + appointments
        var views = await _db.PatientViews360
            .Where(v => v.VerificationStatus == VerificationStatus.Pending
                     || v.VerificationStatus == VerificationStatus.InReview)
            .Include(v => v.Patient)
                .ThenInclude(p => p.Appointments)
            .ToListAsync(ct);

        if (views.Count == 0)
            return Ok(Array.Empty<VerificationQueueEntryDto>());

        // Batch document counts — single DB query avoids N+1 per patient (AIR-P01)
        var patientIds = views.Select(v => v.PatientId).ToList();
        var docCounts  = await _db.ClinicalDocuments
            .Where(d => patientIds.Contains(d.PatientId) && !d.IsDeleted)
            .GroupBy(d => d.PatientId)
            .Select(g => new { PatientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PatientId, x => x.Count, ct);

        var now = DateTime.UtcNow;

        var entries = views
            .Select(v =>
            {
                var nextAppt = v.Patient.Appointments
                    .Where(a => a.SlotDatetime >= now)
                    .Min(a => (DateTime?)a.SlotDatetime);
                docCounts.TryGetValue(v.PatientId, out var docCount);
                return new VerificationQueueEntryDto(
                    v.PatientId,
                    v.Patient.Name,
                    v.Patient.InsuranceMemberId ?? string.Empty,
                    nextAppt,
                    docCount,
                    v.ConflictFlags.Length,
                    v.VerificationStatus.ToString());
            })
            .OrderBy(e => e.NextAppointment ?? DateTime.MaxValue)
            .ToList();

        return Ok(entries);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private Guid GetStaffId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

/// <summary>Resolved patient identity fields (no PHI beyond name/DOB — displayed to authorised staff).</summary>
public record PatientIdentityDto(
    string  Name,
    string  DateOfBirth,
    string? InsuranceMemberId,
    string? InsuranceProvider);

/// <summary>Full 360-view response shape for <c>GET /api/v1/patients/{patientId}/360-view</c>.</summary>
public record PatientView360ResponseDto(
    Guid                                    PatientId,
    PatientIdentityDto                      Patient,
    int                                     ConflictCount,
    Dictionary<string, List<ConsolidatedFactEntry>> FactsByCategory,
    string                                  AssemblyStatus,
    int                                     DocumentCount,
    DateTime                                LastUpdated);

/// <summary>Source-text citation for a single fact (AIR-006).</summary>
public record SourceCitationDto(
    Guid    FactId,
    Guid    DocumentId,
    string  DocumentName,
    string  UploadedAt,
    string  SourceText,
    int?    SourceCharOffset,
    int?    SourceCharLength,
    float   ConfidenceScore);

/// <summary>Single entry in the staff verification queue.</summary>
public record VerificationQueueEntryDto(
    Guid      PatientId,
    string    PatientName,
    string    Mrn,
    DateTime? NextAppointment,
    int       DocumentCount,
    int       ConflictCount,
    string    VerificationStatus);
