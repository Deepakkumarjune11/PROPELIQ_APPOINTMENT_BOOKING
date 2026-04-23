using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Repositories;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAppointmentRegistrationRepository"/>.
/// Executes patient-upsert, slot-booking, and audit-log insertion in one DB transaction (FR-002).
/// </summary>
public sealed class AppointmentRegistrationRepository : IAppointmentRegistrationRepository
{
    private readonly PropelIQDbContext _db;

    public AppointmentRegistrationRepository(PropelIQDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<DateTime> GetSlotDatetimeAsync(Guid slotId, CancellationToken ct = default)
    {
        var datetime = await _db.Appointments
            .Where(a => a.Id == slotId)
            .Select(a => (DateTime?)a.SlotDatetime)
            .FirstOrDefaultAsync(ct);

        if (datetime is null)
            throw new NotFoundException($"Appointment slot {slotId} was not found.");

        return datetime.Value;
    }

    /// <inheritdoc/>
    public async Task<AppointmentRegistrationData> RegisterAsync(
        Guid     slotId,
        string   email,
        string   name,
        DateOnly dob,
        string   phone,
        string?  insuranceProvider,
        string?  insuranceMemberId,
        string   insuranceStatus,
        decimal? noShowRiskScore,
        CancellationToken ct = default)
    {
        // NpgsqlRetryingExecutionStrategy blocks direct BeginTransactionAsync calls (BUG-008).
        // Wrap in CreateExecutionStrategy().ExecuteAsync() to satisfy the retry contract.
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // ── a. Patient upsert ───────────────────────────────────────────────
            // Project to Id only — materialising the full Patient entity would invoke
            // PhiEncryptedConverter.Unprotect on every PHI column.  Rows inserted by
            // the development seeder are stored as plaintext, so Unprotect throws
            // CryptographicException.  Selecting only the non-encrypted Id column
            // sidesteps the converter entirely (OWASP A02 / BUG-010).
            var existingId = await _db.Patients
                .IgnoreQueryFilters()
                .Where(p => p.Email == email)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);

            Patient patient;
            if (existingId is null)
            {
                patient = new Patient
                {
                    Id        = Guid.NewGuid(),
                    Email     = email,
                    Name      = name,
                    Dob       = dob,
                    Phone     = phone,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _db.Patients.Add(patient);
            }
            else
            {
                // AC-4: attach a stub entity so EF generates a targeted UPDATE without
                // ever reading (and therefore decrypting) the existing PHI column values.
                patient = new Patient { Id = existingId.Value };
                _db.Patients.Attach(patient);
                patient.Name      = name;
                patient.Phone     = phone;
                patient.UpdatedAt = DateTime.UtcNow;
            }

            // ── b. Slot reservation ─────────────────────────────────────────────
            var slot = await _db.Appointments.FindAsync([slotId], ct)
                ?? throw new NotFoundException($"Appointment slot {slotId} was not found.");

            if (slot.Status != AppointmentStatus.Available)
                throw new ConflictException($"Appointment slot {slotId} is no longer available.");

            slot.PatientId       = patient.Id;
            slot.Status          = AppointmentStatus.Booked;
            slot.NoShowRiskScore = noShowRiskScore;
            slot.UpdatedAt       = DateTime.UtcNow;

            // ── c. Insurance fields ─────────────────────────────────────────────
            patient.InsuranceProvider  = insuranceProvider;
            patient.InsuranceMemberId  = insuranceMemberId;
            patient.InsuranceStatus    = insuranceStatus;

            // ── d. Audit entry (DR-008, NFR-007 — immutable append-only) ───────
            _db.AuditLogs.Add(new AuditLog
            {
                Id             = Guid.NewGuid(),
                ActorId        = patient.Id,
                ActorType      = AuditActorType.Patient,
                ActionType     = AuditActionType.AppointmentBooked,
                TargetEntityId = slot.Id,
                OccurredAt     = DateTime.UtcNow,
                Details        = JsonSerializer.Serialize(new
                {
                    slotId          = slot.Id,
                    patientId       = patient.Id,
                    insuranceStatus,
                }),
            });

            // ── e. Persist & commit (AC-3: single transaction covers patient, slot, score, audit) ─
            // DbUpdateConcurrencyException arises when xmin on the Appointment row changed between
            // our FindAsync and this SaveChangesAsync — a concurrent booking won the race (AC-4).
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                throw new SlotAlreadyBookedException(slotId);
            }

            await tx.CommitAsync(ct);

            return new AppointmentRegistrationData(patient.Id);
        });
    }
}
