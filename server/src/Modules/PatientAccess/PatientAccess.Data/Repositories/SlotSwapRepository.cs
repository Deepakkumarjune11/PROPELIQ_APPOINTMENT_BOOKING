using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Jobs;
using PatientAccess.Application.Repositories;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core + raw SQL implementation of <see cref="ISlotSwapRepository"/> (US_015, AC-3).
///
/// Swap correctness guarantees:
/// - SERIALIZABLE isolation prevents phantom reads between the "slot free?" check and the update.
/// - <c>SELECT … FOR UPDATE</c> on the appointment row prevents concurrent job workers from
///   processing the same watchlist entry simultaneously (AC-5 race-condition guard).
/// - <c>AuditLog</c> entry is written inside the same transaction so it is atomically committed
///   or rolled back with the appointment change (DR-008).
/// </summary>
public sealed class SlotSwapRepository : ISlotSwapRepository
{
    private readonly PropelIQDbContext _db;

    public SlotSwapRepository(PropelIQDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<WatchlistEntry>> GetActiveWatchlistEntriesAsync(
        CancellationToken ct = default)
    {
        return await _db.Appointments
            .AsNoTracking()
            .Where(a => a.PreferredSlotId != null && !a.IsDeleted)
            .Include(a => a.PreferredSlot)
            .Include(a => a.Patient)
            .Select(a => new WatchlistEntry(
                a.Id,
                a.PatientId,
                a.SlotDatetime,
                a.PreferredSlotId!.Value,
                a.PreferredSlot!.SlotDatetime,
                a.Patient.Phone,
                a.Patient.Email,
                a.Patient.Name))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<SlotSwapResult> TryAtomicSwapAsync(
        Guid appointmentId,
        Guid patientId,
        CancellationToken ct = default)
    {
        // Open a SERIALIZABLE transaction to prevent phantom-read anomalies when
        // checking slot availability. The isolation level is set at the ADO.NET level
        // because EF Core's BeginTransactionAsync wraps the underlying connection.
        await using var tx = await _db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            // Pessimistic row lock — prevents another worker from swapping the same
            // appointment concurrently. Uses raw SQL because EF Core has no SKIP LOCKED / FOR UPDATE API.
            var appointment = await _db.Appointments
                .FromSqlRaw(
                    """SELECT * FROM "appointment" WHERE "Id" = {0} AND "IsDeleted" = false FOR UPDATE""",
                    appointmentId)
                .Include(a => a.PreferredSlot)
                .Include(a => a.Patient)
                .SingleOrDefaultAsync(ct);

            // Idempotent guard — entry was cleared by a concurrent worker (AC-5).
            if (appointment?.PreferredSlotId is null)
            {
                await tx.RollbackAsync(ct);
                return SlotSwapResult.NotOnWatchlist;
            }

            var preferredSlot     = appointment.PreferredSlot!;
            var preferredDatetime = preferredSlot.SlotDatetime;

            // Step 2: Reject if preferred slot datetime has already passed.
            if (preferredDatetime <= DateTime.UtcNow)
            {
                appointment.PreferredSlotId = null;
                appointment.UpdatedAt       = DateTime.UtcNow;

                _db.AuditLogs.Add(BuildAuditLog(
                    AuditActionType.AppointmentChange,
                    patientId,
                    appointmentId,
                    new { action = "WatchlistExpired", preferredDatetime }));

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return SlotSwapResult.SlotExpired;
            }

            // Step 3: Check whether the preferred slot is now free.
            // A slot is free when no other appointment has the same SlotDatetime AND is Booked/Arrived.
            // We exclude the watchlisting appointment itself (it has a different datetime currently).
            bool slotOccupied = await _db.Appointments
                .AnyAsync(a =>
                    a.SlotDatetime == preferredDatetime &&
                    a.Id != appointmentId &&
                    (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Arrived) &&
                    !a.IsDeleted,
                    ct);

            if (slotOccupied)
            {
                // Slot is still taken — preserve watchlist entry (AC-5).
                await tx.RollbackAsync(ct);
                return SlotSwapResult.SlotStillTaken;
            }

            // Step 4: Atomic swap — update datetime, release original slot implicit via status,
            // clear preferred_slot_id, write audit log.
            var originalDatetime       = appointment.SlotDatetime;
            appointment.SlotDatetime   = preferredDatetime;
            appointment.PreferredSlotId = null;
            appointment.UpdatedAt       = DateTime.UtcNow;

            _db.AuditLogs.Add(BuildAuditLog(
                AuditActionType.AppointmentChange,
                patientId,
                appointmentId,
                new
                {
                    action    = "SlotSwapExecuted",
                    fromUtc   = originalDatetime.ToString("o"),
                    toUtc     = preferredDatetime.ToString("o"),
                }));

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return SlotSwapResult.Swapped;
        }
        catch
        {
            // Ensure rollback on any unexpected failure so partially applied changes are never committed.
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AuditLog BuildAuditLog(
        AuditActionType actionType,
        Guid            actorId,
        Guid            targetEntityId,
        object          payload) =>
        new()
        {
            Id             = Guid.NewGuid(),
            ActorId        = actorId,
            ActorType      = AuditActorType.System,
            ActionType     = actionType,
            TargetEntityId = targetEntityId,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(payload),
        };
}
