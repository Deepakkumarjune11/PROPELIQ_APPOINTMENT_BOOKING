using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Repositories;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWatchlistRepository"/> (US_015).
/// Uses <c>AsNoTracking</c> for read queries and change-tracked loads for mutations.
/// Writes PreferredSlotId update and audit log in a single <see cref="PropelIQDbContext.SaveChangesAsync"/> call.
/// </summary>
public sealed class WatchlistRepository : IWatchlistRepository
{
    private readonly PropelIQDbContext _db;

    public WatchlistRepository(PropelIQDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PatientAppointmentData>> GetPatientAppointmentsAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        return await _db.Appointments
            .AsNoTracking()
            .Where(a => a.PatientId == patientId && !a.IsDeleted)
            .Include(a => a.PreferredSlot)
            .OrderByDescending(a => a.SlotDatetime)
            .Select(a => new PatientAppointmentData(
                a.Id,
                a.SlotDatetime,
                a.Status.ToString(),
                a.PreferredSlot != null ? a.PreferredSlot.SlotDatetime : (DateTime?)null))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SlotData>> GetSlotsForMonthAsync(
        int year,
        int month,
        CancellationToken ct = default)
    {
        // Build UTC range for the requested calendar month.
        var startUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc   = startUtc.AddMonths(1);

        return await _db.Appointments
            .AsNoTracking()
            .Where(a =>
                a.SlotDatetime >= startUtc &&
                a.SlotDatetime < endUtc &&
                !a.IsDeleted)
            .OrderBy(a => a.SlotDatetime)
            .Select(a => new SlotData(
                a.Id,
                a.SlotDatetime,
                // IsAvailable = true only for unbooked slots; Booked/Arrived are watchlist-eligible
                a.Status == AppointmentStatus.Available))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Guid?> FindBookedSlotByDatetimeAsync(
        DateTime preferredSlotUtc,
        CancellationToken ct = default)
    {
        // Slot is watchlist-eligible when Status is Booked or Arrived — meaning another
        // patient has claimed it.  Available / Completed / Cancelled / NoShow are ineligible.
        var slot = await _db.Appointments
            .AsNoTracking()
            .Where(a =>
                a.SlotDatetime == preferredSlotUtc &&
                (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Arrived) &&
                !a.IsDeleted)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);

        return slot;
    }

    /// <inheritdoc />
    public async Task RegisterPreferredSlotAsync(
        Guid appointmentId,
        Guid preferredSlotId,
        Guid patientId,
        CancellationToken ct = default)
    {
        // Change-tracked load so EF Core detects the property mutation.
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == appointmentId && !a.IsDeleted, ct);

        if (appointment is null)
            throw new Application.Exceptions.NotFoundException(
                $"Appointment {appointmentId} not found.");

        appointment.PreferredSlotId = preferredSlotId;
        appointment.UpdatedAt       = DateTime.UtcNow;

        // Audit log — append-only per DR-008. PatientId is the actor; AppointmentId is the target.
        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = patientId,
            ActorType      = AuditActorType.Patient,
            ActionType     = AuditActionType.AppointmentChange,
            TargetEntityId = appointmentId,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(new
            {
                action          = "WatchlistRegistered",
                preferredSlotId = preferredSlotId.ToString(),
            }),
        });

        await _db.SaveChangesAsync(ct);
    }
}
