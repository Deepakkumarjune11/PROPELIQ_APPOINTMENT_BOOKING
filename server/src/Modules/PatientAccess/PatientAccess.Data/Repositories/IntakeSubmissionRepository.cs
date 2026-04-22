using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Repositories;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IIntakeSubmissionRepository"/>.
/// Inserts an <see cref="IntakeResponse"/> and an immutable <see cref="AuditLog"/> entry
/// within a single <see cref="DbContext.SaveChangesAsync"/> call (no explicit transaction
/// needed — both writes belong to the same unit of work).
/// </summary>
public sealed class IntakeSubmissionRepository : IIntakeSubmissionRepository
{
    private readonly PropelIQDbContext _db;

    public IntakeSubmissionRepository(PropelIQDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<bool> PatientExistsAsync(Guid patientId, CancellationToken ct = default) =>
        await _db.Patients.AnyAsync(p => p.Id == patientId, ct);

    /// <inheritdoc/>
    public async Task<Guid> SubmitIntakeAsync(
        Guid patientId,
        IntakeMode mode,
        string answersJson,
        CancellationToken ct = default)
    {
        // a. Create intake response — ValueConverter in IntakeResponseConfiguration encrypts
        //    Answers before writing to the jsonb column (DR-015, OWASP A02).
        var intakeResponse = new IntakeResponse
        {
            Id        = Guid.NewGuid(),
            PatientId = patientId,
            Mode      = mode,
            Answers   = answersJson,
            CreatedAt = DateTime.UtcNow,
        };
        _db.IntakeResponses.Add(intakeResponse);

        // b. Audit entry (DR-008, NFR-007 — immutable append-only).
        // PHI guard (DR-015): answers content is NOT included in Details — only metadata.
        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = patientId,
            ActorType      = AuditActorType.Patient,
            ActionType     = AuditActionType.IntakeSubmission,
            TargetEntityId = intakeResponse.Id,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(new
            {
                mode          = mode.ToString(),
                questionCount = JsonSerializer.Deserialize<Dictionary<string, string>>(answersJson)?.Count ?? 0,
            }),
        });

        await _db.SaveChangesAsync(ct);

        return intakeResponse.Id;
    }
}
