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
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Presentation.Controllers;

/// <summary>
/// Staff-facing API for clinical conflict detection and resolution (US_022, FR-013).
///
/// Endpoints:
/// <list type="bullet">
///   <item><c>GET  api/v1/patients/{patientId}/conflicts</c>  — list active conflicts (SCR-018).</item>
///   <item><c>POST api/v1/360-view/{view360Id}/resolve-conflict</c> — apply resolution choice.</item>
///   <item><c>PATCH api/v1/360-view/{view360Id}/status</c>   — set verification status (AC-4 guard).</item>
/// </list>
///
/// All endpoints require the <c>Staff</c> role.
/// AuditLog entries do not include PHI values (AIR-S03).
/// </summary>
[ApiController]
[Authorize(Roles = "Staff,Admin")]
[Route("api/v1")]
public sealed class ConflictController(
    PropelIQDbContext               db,
    IDataProtectionProvider         dataProtectionProvider,
    ILogger<ConflictController>     logger)
    : ControllerBase
{
    private readonly PropelIQDbContext           _db                     = db;
    private readonly IDataProtectionProvider     _dataProtectionProvider = dataProtectionProvider;
    private readonly ILogger<ConflictController> _logger                 = logger;

    private const string View360Purpose   = "PropelIQ.PatientView360.ConsolidatedFacts";
    private const string ConflictPurpose  = "PropelIQ.PatientView360.ConflictFlags";
    private const int    ResolveRetries   = 3;

    // ── GET api/v1/patients/{patientId}/conflicts ─────────────────────────

    /// <summary>
    /// Returns the list of active (unresolved) conflicts for the specified patient (SCR-018, AC-2).
    /// Decrypts each element of <c>ConflictFlags[]</c> and maps to <see cref="ConflictItemDto"/>.
    /// </summary>
    [HttpGet("patients/{patientId:guid}/conflicts")]
    [ProducesResponseType(typeof(IReadOnlyList<ConflictItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConflicts(
        Guid              patientId,
        CancellationToken ct)
    {
        // AIR-S02: only return conflicts for the requested patient's own 360-view
        var view360 = await _db.PatientViews360
            .FirstOrDefaultAsync(v => v.PatientId == patientId, ct);

        if (view360 is null)
            return NotFound(new { message = "360-view not found for this patient." });

        if (view360.ConflictFlags.Length == 0)
            return Ok(Array.Empty<ConflictItemDto>());

        var conflictProtector = _dataProtectionProvider.CreateProtector(ConflictPurpose);
        var items             = new List<ConflictItemDto>(view360.ConflictFlags.Length);

        foreach (var encryptedFlag in view360.ConflictFlags)
        {
            ConflictFlag? flag;
            try
            {
                var json = conflictProtector.Unprotect(encryptedFlag);
                flag     = JsonSerializer.Deserialize<ConflictFlag>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ConflictController: failed to decrypt conflict flag for patient {PatientId}; " +
                    "entry skipped.",
                    patientId);
                continue;
            }

            if (flag is null) continue;

            items.Add(new ConflictItemDto(
                flag.ConflictId,
                view360.Id,
                flag.FactType,
                flag.Sources
                    .Select(s => new ConflictSourceDto(
                        s.DocumentId,
                        s.DocumentName,
                        s.Value,
                        s.ConfidenceScore,
                        s.SourceCharOffset))
                    .ToList()));
        }

        return Ok(items);
    }

    // ── POST api/v1/360-view/{view360Id}/resolve-conflict ─────────────────

    /// <summary>
    /// Applies a staff resolution choice to a single conflict (AC-3, DR-018, FR-013).
    ///
    /// Idempotent: if <c>ConflictId</c> is not found (already resolved), returns 200
    /// with the current remaining conflict count — no duplicate AuditLog entry.
    ///
    /// Returns 409 after 3 concurrency retry attempts (<c>DbUpdateConcurrencyException</c>)
    /// with error code <c>conflict_resolution_race</c> (DR-018).
    /// </summary>
    [HttpPost("360-view/{view360Id:guid}/resolve-conflict")]
    [ProducesResponseType(typeof(ResolveConflictResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResolveConflict(
        Guid                    view360Id,
        [FromBody] ResolveConflictRequest request,
        CancellationToken       ct)
    {
        // Validate: manual override requires a value
        if (string.Equals(request.Resolution, "manual", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.ManualValue))
        {
            return BadRequest(new { error = "manualValue is required when resolution is 'manual'" });
        }

        var conflictProtector = _dataProtectionProvider.CreateProtector(ConflictPurpose);
        var factsProtector    = _dataProtectionProvider.CreateProtector(View360Purpose);
        var staffId           = GetStaffId();

        for (int attempt = 0; attempt < ResolveRetries; attempt++)
        {
            var view360 = await _db.PatientViews360
                .FirstOrDefaultAsync(v => v.Id == view360Id, ct);

            if (view360 is null)
                return NotFound(new { message = "360-view not found." });

            // ── Decrypt current conflict flags ───────────────────────────────
            var (flags, decryptErrors) = DecryptConflictFlags(view360.ConflictFlags, conflictProtector);

            if (decryptErrors > 0)
            {
                _logger.LogWarning(
                    "ConflictController: {Errors} conflict flag(s) could not be decrypted for view {View360Id}.",
                    decryptErrors, view360Id);
            }

            // Idempotent path: conflict already resolved
            var targetFlag = flags.FirstOrDefault(f => f.ConflictId == request.ConflictId);
            if (targetFlag is null)
                return Ok(new ResolveConflictResponseDto(view360.ConflictFlags.Length));

            // ── Determine resolved value ─────────────────────────────────────
            string resolvedValue;
            switch (request.Resolution.ToLowerInvariant())
            {
                case "sourcea":
                    resolvedValue = targetFlag.Sources.Count > 0 ? targetFlag.Sources[0].Value : string.Empty;
                    break;
                case "sourceb":
                    resolvedValue = targetFlag.Sources.Count > 1 ? targetFlag.Sources[1].Value : string.Empty;
                    break;
                case "manual":
                    resolvedValue = request.ManualValue!;
                    break;
                default:
                    return BadRequest(new { error = $"Unknown resolution value: '{request.Resolution}'" });
            }

            // ── Update consolidated facts ─────────────────────────────────────
            List<ConsolidatedFactEntry> consolidated;
            try
            {
                var factsJson   = factsProtector.Unprotect(view360.ConsolidatedFacts);
                consolidated    = JsonSerializer.Deserialize<List<ConsolidatedFactEntry>>(factsJson) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ConflictController: failed to decrypt ConsolidatedFacts for view {View360Id}.",
                    view360Id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Failed to decrypt patient data." });
            }

            ApplyResolutionToFacts(consolidated, targetFlag, resolvedValue, request.Resolution);

            var updatedFactsJson      = JsonSerializer.Serialize(consolidated);
            view360.ConsolidatedFacts = factsProtector.Protect(updatedFactsJson);

            // ── Remove resolved conflict from flags array ─────────────────────
            var remainingFlags = flags
                .Where(f => f.ConflictId != request.ConflictId)
                .Select(f => conflictProtector.Protect(JsonSerializer.Serialize(f)))
                .ToArray();

            view360.ConflictFlags = remainingFlags;

            // Update status back to Pending when all conflicts resolved
            if (remainingFlags.Length == 0
                && view360.VerificationStatus == VerificationStatus.NeedsReview)
            {
                view360.VerificationStatus = VerificationStatus.Pending;
            }

            view360.LastUpdated = DateTime.UtcNow;
            view360.Version++;

            // ── AuditLog — no PHI values (AIR-S03) ──────────────────────────
            _db.AuditLogs.Add(new AuditLog
            {
                Id             = Guid.NewGuid(),
                ActorId        = staffId,
                ActorType      = AuditActorType.Staff,
                ActionType     = AuditActionType.ClinicalDataModification,
                TargetEntityId = view360Id,
                OccurredAt     = DateTime.UtcNow,
                Details        = JsonSerializer.Serialize(new
                {
                    action        = "ConflictResolved",
                    ConflictId    = request.ConflictId,
                    FactType      = targetFlag.FactType,
                    Resolution    = request.Resolution,
                    Justification = request.Justification,
                }),
            });

            try
            {
                await _db.SaveChangesAsync(ct);
                return Ok(new ResolveConflictResponseDto(remainingFlags.Length));
            }
            catch (DbUpdateConcurrencyException) when (attempt < ResolveRetries - 1)
            {
                _db.ChangeTracker.Clear();
                _logger.LogWarning(
                    "ConflictController: concurrency conflict for view {View360Id} " +
                    "(attempt {Attempt}/{Max}). Retrying.",
                    view360Id, attempt + 1, ResolveRetries);
            }
        }

        return Conflict(new { error = "conflict_resolution_race" });
    }

    // ── PATCH api/v1/360-view/{view360Id}/status ──────────────────────────

    /// <summary>
    /// Updates the verification status of a 360-view (UC-005).
    ///
    /// AC-4 guard: returns 422 when transitioning to <c>verified</c> while active
    /// conflicts remain in <c>ConflictFlags</c>.
    /// </summary>
    [HttpPatch("360-view/{view360Id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateStatus(
        Guid                      view360Id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken         ct)
    {
        var view360 = await _db.PatientViews360
            .FirstOrDefaultAsync(v => v.Id == view360Id, ct);

        if (view360 is null)
            return NotFound(new { message = "360-view not found." });

        // AC-4: block verification when active conflicts exist (FR-013)
        if (string.Equals(request.Status, "verified", StringComparison.OrdinalIgnoreCase))
        {
            var conflictCount = view360.ConflictFlags.Length;
            if (conflictCount > 0)
            {
                return UnprocessableEntity(new
                {
                    error         = "verification_blocked_by_conflicts",
                    conflictCount,
                });
            }

            // AC-4: block verification when unreviewed code suggestions remain (US_023)
            var hasUnreviewedCodes = await _db.CodeSuggestions
                .AnyAsync(c => c.PatientId == view360.PatientId && !c.IsDeleted && !c.StaffReviewed, ct);

            if (hasUnreviewedCodes)
            {
                return UnprocessableEntity(new
                {
                    error = "verification_blocked_by_unreviewed_codes",
                });
            }

            view360.VerificationStatus = VerificationStatus.Verified;
        }
        else if (string.Equals(request.Status, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            view360.VerificationStatus = VerificationStatus.Rejected;
        }
        else if (string.Equals(request.Status, "inreview", StringComparison.OrdinalIgnoreCase)
              || string.Equals(request.Status, "in-review", StringComparison.OrdinalIgnoreCase))
        {
            view360.VerificationStatus = VerificationStatus.InReview;
        }
        else
        {
            return BadRequest(new { error = $"Unknown status value: '{request.Status}'" });
        }

        view360.LastUpdated = DateTime.UtcNow;
        view360.Version++;

        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = GetStaffId(),
            ActorType      = AuditActorType.Staff,
            ActionType     = AuditActionType.ClinicalDataModification,
            TargetEntityId = view360Id,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(new
            {
                action = "PatientView360StatusUpdated",
                Status = request.Status,
            }),
        });

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Guid GetStaffId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    /// <summary>
    /// Decrypts all elements of <paramref name="encryptedFlags"/> and returns successfully
    /// decrypted <see cref="ConflictFlag"/> objects alongside the count of decryption errors.
    /// </summary>
    private static (List<ConflictFlag> Flags, int Errors) DecryptConflictFlags(
        string[]        encryptedFlags,
        IDataProtector  protector)
    {
        var flags  = new List<ConflictFlag>(encryptedFlags.Length);
        int errors = 0;

        foreach (var enc in encryptedFlags)
        {
            try
            {
                var json = protector.Unprotect(enc);
                var flag = JsonSerializer.Deserialize<ConflictFlag>(json);
                if (flag is not null) flags.Add(flag);
            }
            catch
            {
                errors++;
            }
        }

        return (flags, errors);
    }

    /// <summary>
    /// Updates <paramref name="facts"/> in-place to apply the resolution:
    /// removes the losing entry (for sourceA/sourceB) or both conflicting entries and
    /// inserts a manual-value entry (for manual resolution).
    /// Matching is by value equality within the conflict's FactType group.
    /// </summary>
    private static void ApplyResolutionToFacts(
        List<ConsolidatedFactEntry> facts,
        ConflictFlag                conflict,
        string                      resolvedValue,
        string                      resolution)
    {
        if (conflict.Sources.Count < 2) return;

        var srcA = conflict.Sources[0];
        var srcB = conflict.Sources[1];

        if (string.Equals(resolution, "sourceA", StringComparison.OrdinalIgnoreCase))
        {
            // Remove entry whose value matches source B
            facts.RemoveAll(f =>
                f.FactType == conflict.FactType &&
                string.Equals(f.Value, srcB.Value, StringComparison.Ordinal));
        }
        else if (string.Equals(resolution, "sourceB", StringComparison.OrdinalIgnoreCase))
        {
            // Remove entry whose value matches source A
            facts.RemoveAll(f =>
                f.FactType == conflict.FactType &&
                string.Equals(f.Value, srcA.Value, StringComparison.Ordinal));
        }
        else  // manual
        {
            // Remove both conflicting entries; insert resolved manual entry
            facts.RemoveAll(f =>
                f.FactType == conflict.FactType &&
                (string.Equals(f.Value, srcA.Value, StringComparison.Ordinal) ||
                 string.Equals(f.Value, srcB.Value, StringComparison.Ordinal)));

            var winnerSource = srcA.ConfidenceScore >= srcB.ConfidenceScore ? srcA : srcB;

            facts.Add(new ConsolidatedFactEntry(
                conflict.FactType,
                resolvedValue,
                winnerSource.ConfidenceScore,
                new[] { new FactSourceRef(
                    winnerSource.DocumentId,
                    winnerSource.DocumentName,
                    winnerSource.SourceCharOffset,
                    null) }));
        }
    }
}

// ── Request / Response DTOs ───────────────────────────────────────────────────

/// <summary>Request body for <c>POST /360-view/{view360Id}/resolve-conflict</c>.</summary>
public record ResolveConflictRequest(
    Guid    ConflictId,
    string  Resolution,    // "sourceA" | "sourceB" | "manual"
    string? ManualValue,
    string  Justification);

/// <summary>Request body for <c>PATCH /360-view/{view360Id}/status</c>.</summary>
public record UpdateStatusRequest(string Status);

/// <summary>Response body for <c>POST /360-view/{view360Id}/resolve-conflict</c>.</summary>
public record ResolveConflictResponseDto(int RemainingConflicts);

/// <summary>Single conflict item returned by <c>GET /patients/{patientId}/conflicts</c>.</summary>
public record ConflictItemDto(
    Guid                         ConflictId,
    Guid                         View360Id,
    string                       FactType,
    IReadOnlyList<ConflictSourceDto> Sources);

/// <summary>One source value within a <see cref="ConflictItemDto"/>.</summary>
public record ConflictSourceDto(
    Guid    DocumentId,
    string  DocumentName,
    string  Value,
    float   ConfidenceScore,
    int?    SourceCharOffset);
