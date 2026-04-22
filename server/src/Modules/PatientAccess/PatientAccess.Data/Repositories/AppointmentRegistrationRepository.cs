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
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // ── a. Patient upsert ───────────────────────────────────────────────
        // IgnoreQueryFilters bypasses the soft-delete global filter so an IsDeleted=true
        // row with the same email doesn't trigger a false "not found" result (AC-4).
        var patient = await _db.Patients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Email == email, ct);

        if (patient is null)
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
            // AC-4: reuse existing record; refresh mutable demographic fields on re-visit.
            patient.Name      = name;
            patient.Phone     = phone;
            patient.UpdatedAt = DateTime.UtcNow;
        }

        // ── b. Slot reservation ─────────────────────────────────────────────
        var slot = await _db.Appointments.FindAsync([slotId], ct)
            ?? throw new NotFoundException($"Appointment slot {slotId} was not found.");

        if (slot.Status != AppointmentStatus.Available)
            throw new ConflictException($"Appointment slot {slotId} is no longer available.");

        slot.PatientId      = patient.Id;
        slot.Status         = AppointmentStatus.Booked;
        slot.NoShowRiskScore = noShowRiskScore;
        slot.UpdatedAt      = DateTime.UtcNow;

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
            throw new SlotAlreadyBookedException(slotId);
        }

        await tx.CommitAsync(ct);

        return new AppointmentRegistrationData(patient.Id);
    }
}
