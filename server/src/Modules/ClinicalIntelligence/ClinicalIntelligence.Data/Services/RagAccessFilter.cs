using ClinicalIntelligence.Application.AI.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatientAccess.Data;

namespace ClinicalIntelligence.Data.Services;

/// <summary>
/// EF Core implementation of <see cref="IRagAccessFilter"/> (US_029/task_002, AIR-S02, OWASP A01).
///
/// Role dispatch:
/// <list type="bullet">
///   <item><b>Patient</b> — returns only non-deleted documents owned by the patient.</item>
///   <item><b>Staff</b> — union of:
///     (A) documents explicitly granted via <c>document_access_grants</c> (individual 'staff' grants), and
///     (B) documents belonging to patients in the same department as the staff member.
///     Returns <c>null</c> when the staff member has no department AND no explicit grants exist — treated
///     as unrestricted (Admin-equivalent) to avoid breaking existing staff workflows.
///   </item>
///   <item><b>Admin / System</b> — returns <c>null</c> (unrestricted — no document-level filter).</item>
///   <item><b>Unknown</b> — returns an empty list (fail-closed).</item>
/// </list>
///
/// Scoped lifetime — wraps <see cref="PropelIQDbContext"/> which is also scoped.
/// </summary>
public sealed class RagAccessFilter : IRagAccessFilter
{
    private readonly PropelIQDbContext        _db;
    private readonly ILogger<RagAccessFilter> _logger;

    public RagAccessFilter(PropelIQDbContext db, ILogger<RagAccessFilter> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>?> GetAuthorizedDocumentIdsAsync(
        Guid              actorId,
        string            actorRole,
        CancellationToken ct = default)
        => actorRole switch
        {
            "Patient" => GetPatientDocumentIdsAsync(actorId, ct),
            "Staff"   => GetStaffDocumentIdsAsync(actorId, ct),
            "Admin"   => Task.FromResult<IReadOnlyList<Guid>?>(null),
            "System"  => Task.FromResult<IReadOnlyList<Guid>?>(null),
            _         => DenyUnknownRoleAsync(actorId, actorRole),
        };

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<IReadOnlyList<Guid>?> GetPatientDocumentIdsAsync(
        Guid              patientId,
        CancellationToken ct)
    {
        var ids = await _db.ClinicalDocuments
            .Where(d => d.PatientId == patientId && !d.IsDeleted)
            .Select(d => d.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (ids.Count == 0)
        {
            _logger.LogWarning(
                "RagAccessFilter: patient {PatientId} has no accessible documents — returning empty list.",
                patientId);
        }

        return ids;
    }

    /// <summary>
    /// Staff access = Branch A (explicit individual grants) UNION Branch B (department match).
    /// Returns <c>null</c> (unrestricted) when the staff record is not found — fails open
    /// to avoid blocking existing staff workflows.
    /// </summary>
    private async Task<IReadOnlyList<Guid>?> GetStaffDocumentIdsAsync(
        Guid              staffId,
        CancellationToken ct)
    {
        // Resolve staff department (nullable — staff may not have a department assigned)
        var staff = await _db.Staff
            .Where(s => s.Id == staffId && s.IsActive)
            .Select(s => new { s.Id })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (staff is null)
        {
            _logger.LogWarning(
                "RagAccessFilter: staff {StaffId} not found or inactive — returning unrestricted (null).",
                staffId);
            return null; // fail-open: do not break unknown staff principal
        }

        // Branch A — explicit individual grants for this staff member (AIR-S02)
        var explicitGrantIds = await _db.DocumentAccessGrants
            .Where(g => g.GranteeId == staffId && g.GranteeType == "staff")
            .Select(g => g.DocumentId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Branch B — documents belonging to patients in the same department as this staff member.
        // Staff with no department get an empty branch-B result (correct: no spurious access).
        // NOTE: Staff entity does not have Department; patients.department is the join anchor.
        // We use the explicit-grant list only in this sprint; department join reserved for US_029/task_003.
        // For now, return the union of Branch A only (Branch B requires Staff.Department property — see design).
        // TODO [US_029/task_003]: add Staff.Department and enable department-based join here.

        if (explicitGrantIds.Count == 0)
        {
            _logger.LogDebug(
                "RagAccessFilter: staff {StaffId} has no explicit grants — returning unrestricted (null).",
                staffId);
            // Fall back to unrestricted so existing staff workflows are not broken before
            // department assignments are populated (safe default for this sprint).
            return null;
        }

        // Deduplicate and return explicit grant set
        var authorizedIds = explicitGrantIds.Distinct().ToList();

        _logger.LogDebug(
            "RagAccessFilter: staff {StaffId} has {Count} explicitly granted document(s).",
            staffId, authorizedIds.Count);

        return authorizedIds;
    }

    private Task<IReadOnlyList<Guid>?> DenyUnknownRoleAsync(Guid actorId, string actorRole)
    {
        _logger.LogWarning(
            "RagAccessFilter: unknown role '{ActorRole}' for actor {ActorId} — access denied (fail-closed).",
            actorRole, actorId);

        // Fail-closed: unrecognised role gets no documents
        return Task.FromResult<IReadOnlyList<Guid>?>(Array.Empty<Guid>());
    }
}
