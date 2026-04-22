using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatientAccess.Data;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Presentation.Controllers;

/// <summary>
/// Staff-facing API for AI-assisted clinical code suggestions (US_023, FR-014).
///
/// Endpoints:
/// <list type="bullet">
///   <item><c>GET  api/v1/patients/{patientId}/code-suggestions</c> — list suggestions for review (SCR-019).</item>
///   <item><c>POST api/v1/code-suggestions/confirm</c>             — accept or reject a code (AC-7).</item>
/// </list>
///
/// All endpoints require the <c>Staff</c> role.
/// AuditLog entries do not include PHI values (AIR-S03).
/// Agreement rate metrics are emitted as structured log events (NFR-018);
/// metric emission failures are swallowed and logged as warnings so the request is never failed.
/// </summary>
[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1")]
public sealed class CodeSuggestionController(
    PropelIQDbContext                   db,
    IDataProtectionProvider             dataProtectionProvider,
    ILogger<CodeSuggestionController>   logger)
    : ControllerBase
{
    private const string FactTextPurpose = "PropelIQ.ExtractedFacts.FactText";

    private readonly PropelIQDbContext                  _db                     = db;
    private readonly IDataProtectionProvider            _dataProtectionProvider = dataProtectionProvider;
    private readonly ILogger<CodeSuggestionController>  _logger                 = logger;

    // ── GET api/v1/patients/{patientId}/code-suggestions ─────────────────────

    /// <summary>
    /// Returns active (non-deleted) code suggestions for a patient, ordered by confidence
    /// score descending. Each suggestion includes evidence facts with decrypted summaries
    /// for clinical context (SCR-019).
    /// </summary>
    [HttpGet("patients/{patientId:guid}/code-suggestions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSuggestions(Guid patientId, CancellationToken ct)
    {
        var patientExists = await _db.Patients
            .AnyAsync(p => p.Id == patientId, ct);

        if (!patientExists)
            return NotFound(new { message = "Patient not found." });

        var suggestions = await _db.CodeSuggestions
            .Where(c => c.PatientId == patientId && !c.IsDeleted)
            .OrderByDescending(c => c.ConfidenceScore)
            .ToListAsync(ct);

        if (suggestions.Count == 0)
            return Ok(Array.Empty<CodeSuggestionResponseDto>());

        // Collect evidence fact IDs to load + decrypt in batch
        var allFactIds = suggestions
            .SelectMany(s => s.EvidenceFactIds)
            .Distinct()
            .ToList();

        var factLookup = new Dictionary<Guid, string>(allFactIds.Count);

        if (allFactIds.Count > 0)
        {
            var facts = await _db.ExtractedFacts
                .IgnoreQueryFilters()
                .Where(f => allFactIds.Contains(f.Id))
                .ToListAsync(ct);

            var protector = _dataProtectionProvider.CreateProtector(FactTextPurpose);

            foreach (var fact in facts)
            {
                try
                {
                    var plain = protector.Unprotect(fact.FactText);
                    // Truncate to 200 chars for UI summary (SCR-019 evidence chip)
                    factLookup[fact.Id] = plain.Length > 200 ? plain[..200] : plain;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "CodeSuggestionController: decrypt failed for fact {FactId}; using placeholder.",
                        fact.Id);
                    factLookup[fact.Id] = "[encrypted — decryption error]";
                }
            }
        }

        var dtos = suggestions.Select(s => new CodeSuggestionResponseDto(
            Id:              s.Id,
            Code:            s.CodeValue,
            Description:     s.Description,
            CodeType:        s.CodeType == CodeType.Icd10 ? "ICD-10" : "CPT",
            ConfidenceScore: s.ConfidenceScore,
            EvidenceFacts:   s.EvidenceFactIds
                              .Select(fid => new EvidenceFactDto(
                                  FactId:      fid,
                                  FactSummary: factLookup.TryGetValue(fid, out var v) ? v : "[not found]"))
                              .ToList(),
            StaffReviewed:    s.StaffReviewed,
            ReviewOutcome:    s.ReviewOutcome,
            Justification:    s.ReviewJustification
        )).ToList();

        return Ok(dtos);
    }

    // ── POST api/v1/code-suggestions/confirm ─────────────────────────────────

    /// <summary>
    /// Records a staff decision (accept/reject) on a code suggestion (AC-7, UC-005).
    ///
    /// Business rules:
    /// <list type="bullet">
    ///   <item>ReviewOutcome = "rejected" requires a non-empty justification (AC-7).</item>
    ///   <item>Sets StaffReviewed = true, ReviewedAt = UtcNow.</item>
    ///   <item>Emits AuditLog without PHI (AIR-S03).</item>
    ///   <item>Emits structured metrics for agreement rate tracking (NFR-018).</item>
    /// </list>
    /// </summary>
    [HttpPost("code-suggestions/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmCode(
        [FromBody] ConfirmCodeRequest request,
        CancellationToken ct)
    {
        // AC-7: validate outcome value
        if (!string.Equals(request.ReviewOutcome, "accepted", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.ReviewOutcome, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = $"Invalid reviewOutcome '{request.ReviewOutcome}'. Must be 'accepted' or 'rejected'." });
        }

        // AC-7: rejected codes require justification
        if (string.Equals(request.ReviewOutcome, "rejected", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.Justification))
        {
            return BadRequest(new { error = "Justification is required when reviewOutcome is 'rejected'." });
        }

        var suggestion = await _db.CodeSuggestions
            .FirstOrDefaultAsync(c => c.Id == request.CodeId && !c.IsDeleted, ct);

        if (suggestion is null)
            return NotFound(new { message = "Code suggestion not found." });

        suggestion.StaffReviewed        = true;
        suggestion.ReviewOutcome        = request.ReviewOutcome.ToLowerInvariant();
        suggestion.ReviewJustification  = request.Justification;
        suggestion.ReviewedAt           = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = GetStaffId(),
            ActorType      = AuditActorType.Staff,
            ActionType     = AuditActionType.ClinicalDataModification,
            TargetEntityId = suggestion.Id,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(new
            {
                action        = "CodeReviewed",
                CodeId        = suggestion.Id,
                ReviewOutcome = suggestion.ReviewOutcome,
                // No PHI values (AIR-S03)
            }),
        });

        await _db.SaveChangesAsync(ct);

        // NFR-018: emit structured agreement-rate metrics; failures must not fail the request
        try
        {
            _logger.LogInformation(
                "Metric:AgreementRate_Total +1 for code {CodeId} patient {PatientId}.",
                suggestion.Id, suggestion.PatientId);

            if (string.Equals(suggestion.ReviewOutcome, "accepted", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Metric:AgreementRate_Agreed +1 for code {CodeId} patient {PatientId}.",
                    suggestion.Id, suggestion.PatientId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CodeSuggestionController: metric emission failed for code {CodeId}; continuing.", suggestion.Id);
        }

        return NoContent();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Guid GetStaffId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}

// ── Request / Response DTOs ───────────────────────────────────────────────────

/// <summary>Payload for <c>POST /code-suggestions/confirm</c>.</summary>
public sealed record ConfirmCodeRequest(
    Guid   CodeId,
    string ReviewOutcome,
    string? Justification);

/// <summary>Single evidence fact projected for the GET suggestions response.</summary>
public sealed record EvidenceFactDto(
    Guid   FactId,
    string FactSummary);

/// <summary>Full code suggestion response DTO for <c>GET /patients/{id}/code-suggestions</c>.</summary>
public sealed record CodeSuggestionResponseDto(
    Guid                    Id,
    string                  Code,
    string                  Description,
    string                  CodeType,
    float                   ConfidenceScore,
    IReadOnlyList<EvidenceFactDto> EvidenceFacts,
    bool                    StaffReviewed,
    string?                 ReviewOutcome,
    string?                 Justification);
