using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Shared;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPatientOwnershipValidator"/> (US_015, OWASP A01).
/// Performs a minimal read to verify appointment ownership without loading full entities.
/// Returns false for both "not found" and "wrong owner" to prevent timing-based enumeration attacks.
/// </summary>
public sealed class PatientOwnershipValidator : IPatientOwnershipValidator
{
    private readonly PropelIQDbContext _db;

    public PatientOwnershipValidator(PropelIQDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<bool> IsOwnerAsync(
        Guid appointmentId,
        Guid patientId,
        CancellationToken ct = default)
    {
        // Single-column projection — minimal DB read; no PHI returned to the application layer.
        return await _db.Appointments
            .AsNoTracking()
            .Where(a => a.Id == appointmentId && !a.IsDeleted)
            .Select(a => a.PatientId)
            .AnyAsync(pid => pid == patientId, ct);
    }
}
