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
[Authorize(Roles = "Staff,Admin")]
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

        // When no 360-view record exists yet (AI pipeline still running or unconfigured),
        // return a pending-assembly response instead of 404 so the frontend can display
        // "Summary is being assembled…" rather than an error state (BUG-011).
        if (view360 is null)
        {
            // Confirm the patient actually exists — genuine 404 if not.
            var pendingPatient = await _db.Patients
                .Where(p => p.Id == patientId)
                .Select(p => new { p.Id, p.Email, p.Dob, p.InsuranceMemberId, p.InsuranceProvider })
                .FirstOrDefaultAsync(ct);

            if (pendingPatient is null)
                return NotFound(new { message = "Patient not found." });

            var docCount = await _db.ClinicalDocuments
                .CountAsync(d => d.PatientId == patientId && !d.IsDeleted, ct);

            var pendingResponse = new PatientView360ResponseDto(
                patientId,
                new PatientIdentityDto(
                    pendingPatient.Email,
                    pendingPatient.Dob.ToString("yyyy-MM-dd"),
                    pendingPatient.InsuranceMemberId,
                    pendingPatient.InsuranceProvider),
                0,
                new Dictionary<string, List<ConsolidatedFactEntry>>(),
                "pending",
                docCount,
                DateTime.UtcNow);

            return Ok(pendingResponse);
        }

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
        var now = DateTime.UtcNow;

        // ── Track 1: patients with an assembled PatientView360 (Pending or InReview) ─
        // Project to non-PHI columns only — avoids materialising Patient.Name / Phone /
        // InsuranceMemberId which are encrypted at rest.  PhiEncryptedConverter.Unprotect
        // throws for rows written by the dev seeder (stored as plaintext), crashing the
        // entire queue endpoint (BUG-010).  Email is intentionally NOT encrypted (DR-015
        // edge case — it is the login key and has a unique index).
        var assembledRows = await _db.PatientViews360
            .Where(v => v.VerificationStatus == VerificationStatus.Pending
                     || v.VerificationStatus == VerificationStatus.InReview)
            .Select(v => new
            {
                v.PatientId,
                PatientEmail    = v.Patient.Email,
                ConflictCount   = v.ConflictFlags.Length,
                NextAppointment = v.Patient.Appointments
                    .Where(a => a.SlotDatetime >= now && !a.IsDeleted)
                    .Min(a => (DateTime?)a.SlotDatetime),
                DocumentCount   = _db.ClinicalDocuments
                    .Count(d => d.PatientId == v.PatientId && !d.IsDeleted),
                HasConflict     = v.ConflictFlags.Length > 0,
            })
            .ToListAsync(ct);

        var assembledPatientIds = assembledRows.Select(r => r.PatientId).ToHashSet();

        // ── Track 2: patients whose documents hit ManualReview but have NO PatientView360 yet.
        // This happens when Azure OpenAI is unconfigured or the AI pipeline is unavailable —
        // the job chain short-circuits to ManualReview without ever creating a 360-view record,
        // leaving these patients invisible to staff (BUG-011 — AI pipeline gap).
        var manualReviewRows = await _db.ClinicalDocuments
            .Where(d => !d.IsDeleted
                     && (d.ExtractionStatus == ExtractionStatus.ManualReview
                      || d.ExtractionStatus == ExtractionStatus.Processing
                      || d.ExtractionStatus == ExtractionStatus.Queued)
                     && !assembledPatientIds.Contains(d.PatientId))
            .GroupBy(d => d.PatientId)
            .Select(g => new
            {
                PatientId     = g.Key,
                PatientEmail  = g.First().Patient.Email,
                DocumentCount = g.Count(),
                NextAppointment = g.First().Patient.Appointments
                    .Where(a => a.SlotDatetime >= now && !a.IsDeleted)
                    .Min(a => (DateTime?)a.SlotDatetime),
            })
            .ToListAsync(ct);

        if (assembledRows.Count == 0 && manualReviewRows.Count == 0)
            return Ok(Array.Empty<VerificationQueueEntryDto>());

        // ── Merge both tracks into a single ordered list ──────────────────
        var dtos = assembledRows
            .Select(r => new VerificationQueueEntryDto(
                r.PatientId,
                r.PatientEmail,
                r.PatientId.ToString()[..8].ToUpperInvariant(),
                r.NextAppointment,
                r.DocumentCount,
                r.ConflictCount,
                r.HasConflict ? "conflict" : "pending"))
            .Concat(manualReviewRows.Select(r => new VerificationQueueEntryDto(
                r.PatientId,
                r.PatientEmail,
                r.PatientId.ToString()[..8].ToUpperInvariant(),
                r.NextAppointment,
                r.DocumentCount,
                0,
                "pending")))
            .OrderBy(d => d.AppointmentDatetime ?? DateTime.MaxValue)
            .ToList();

        return Ok(dtos);
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
/// <remarks>
/// Field names are intentionally camelCase-aligned with the frontend VerificationQueueEntry
/// TypeScript interface: AppointmentDatetime → appointmentDatetime, Priority → priority.
/// </remarks>
public record VerificationQueueEntryDto(
    Guid      PatientId,
    string    PatientName,
    string    Mrn,
    DateTime? AppointmentDatetime,   // was NextAppointment — renamed to match frontend
    int       DocumentCount,
    int       ConflictCount,
    string    Priority);              // "conflict" | "pending" — matches frontend union type
