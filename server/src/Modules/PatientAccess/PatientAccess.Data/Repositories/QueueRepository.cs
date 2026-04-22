using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Staff.Dtos;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IQueueRepository"/> for same-day queue operations (US_017).
/// </summary>
public sealed class QueueRepository : IQueueRepository
{
    private readonly PropelIQDbContext _db;

    public QueueRepository(PropelIQDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueueEntryDto>> GetTodayQueueAsync(
        CancellationToken cancellationToken = default)
    {
        // Use >= / < range on the raw DateTime column so Postgres uses the
        // ix_appointments_slot_datetime index instead of a full table scan.
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd   = todayStart.AddDays(1);

        return await _db.Appointments
            .AsNoTracking()
            .Where(a => a.SlotDatetime >= todayStart
                        && a.SlotDatetime < todayEnd
                        && a.QueuePosition != null
                        && !a.IsDeleted
                        && a.Status != AppointmentStatus.Left
                        && a.Status != AppointmentStatus.Completed)
            .OrderBy(a => a.QueuePosition)
            .Include(a => a.Patient)
            .Select(a => new QueueEntryDto(
                a.Id,
                a.QueuePosition!.Value,
                a.Patient.Name,
                new DateTimeOffset(a.SlotDatetime, TimeSpan.Zero),
                a.Status.ToString(),
                "Walk-In"))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task BulkUpdateQueuePositionsAsync(
        IReadOnlyList<Guid> orderedIds,
        CancellationToken   cancellationToken = default)
    {
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var position = i + 1;
            var id       = orderedIds[i];

            await _db.Appointments
                .Where(a => a.Id == id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(a => a.QueuePosition, position),
                    cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<AppointmentStatus> UpdateAppointmentStatusAsync(
        Guid              appointmentId,
        AppointmentStatus newStatus,
        Guid              staffId,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var appointment = await _db.Appointments
            .SingleOrDefaultAsync(
                a => a.Id == appointmentId
                     && DateOnly.FromDateTime(a.SlotDatetime) == today
                     && !a.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException("Appointment not found in today's queue.");

        var previousStatus = appointment.Status;
        appointment.UpdateStatus(newStatus);

        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = staffId,
            ActorType      = AuditActorType.Staff,
            ActionType     = AuditActionType.AppointmentChange,
            TargetEntityId = appointmentId,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(new
            {
                action = "AppointmentStatusUpdated",
                from   = previousStatus.ToString(),
                to     = newStatus.ToString(),
            }),
        });

        await _db.SaveChangesAsync(cancellationToken);

        return previousStatus;
    }
}
