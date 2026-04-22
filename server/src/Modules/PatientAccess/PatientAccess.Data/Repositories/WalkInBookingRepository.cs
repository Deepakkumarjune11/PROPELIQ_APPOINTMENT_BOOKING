using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Appointments.Commands.BookWalkIn;
using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Staff.Dtos;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWalkInBookingRepository"/>.
/// Uses SERIALIZABLE isolation to prevent phantom reads when computing MAX(QueuePosition).
/// </summary>
public sealed class WalkInBookingRepository : IWalkInBookingRepository
{
    private readonly PropelIQDbContext _db;

    public WalkInBookingRepository(PropelIQDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<WalkInBookingResultDto> BookWalkInAsync(
        BookWalkInCommand command,
        CancellationToken cancellationToken = default)
    {
        // NpgsqlRetryingExecutionStrategy blocks direct BeginTransactionAsync calls.
        // Wrapping in CreateExecutionStrategy().ExecuteAsync() satisfies the retry contract
        // while still allowing a user-initiated SERIALIZABLE transaction (BUG-008).
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                // Use >= / < range on SlotDatetime — .Date is not translatable by EF Core/Npgsql.
                var todayStart = DateTime.UtcNow.Date;
                var todayEnd   = todayStart.AddDays(1);

                // Duplicate guard: same patient booked twice today → 409.
                bool alreadyBooked = await _db.Appointments
                    .AnyAsync(
                        a => a.PatientId == command.PatientId &&
                             a.IsWalkIn &&
                             a.SlotDatetime >= todayStart &&
                             a.SlotDatetime < todayEnd,
                        cancellationToken);

                if (alreadyBooked)
                    throw new ConflictException("Patient already has a walk-in appointment today.");

                // Check for an available same-day slot (slot = Available status appointment today).
                bool slotAvailable = await _db.Appointments
                    .AnyAsync(
                        a => a.SlotDatetime >= todayStart &&
                             a.SlotDatetime < todayEnd &&
                             a.Status == AppointmentStatus.Available,
                        cancellationToken);

                // Assign next queue position atomically (MAX + 1 within SERIALIZABLE tx).
                int nextPosition = ((await _db.Appointments
                    .Where(a => a.IsWalkIn &&
                                a.SlotDatetime >= todayStart &&
                                a.SlotDatetime < todayEnd)
                    .MaxAsync(a => (int?)a.QueuePosition, cancellationToken)) ?? 0) + 1;

                var appointment = new Appointment
                {
                    Id            = Guid.NewGuid(),
                    PatientId     = command.PatientId,
                    SlotDatetime  = DateTime.UtcNow,
                    Status        = AppointmentStatus.Booked,
                    IsWalkIn      = true,
                    QueuePosition = nextPosition,
                    CreatedAt     = DateTime.UtcNow,
                    UpdatedAt     = DateTime.UtcNow,
                };

                _db.Appointments.Add(appointment);

                _db.AuditLogs.Add(new AuditLog
                {
                    Id             = Guid.NewGuid(),
                    ActorId        = command.StaffId,
                    ActorType      = AuditActorType.Staff,
                    ActionType     = AuditActionType.AppointmentBooked,
                    TargetEntityId = appointment.Id,
                    OccurredAt     = DateTime.UtcNow,
                    Details        = JsonSerializer.Serialize(new
                    {
                        action        = "WalkInBooked",
                        patientId     = command.PatientId,
                        queuePosition = nextPosition,
                        waitQueue     = !slotAvailable,
                        visitType     = command.VisitType,
                    }),
                });

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return new WalkInBookingResultDto(
                    AppointmentId: appointment.Id,
                    QueuePosition: nextPosition,
                    WaitQueue:     !slotAvailable);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
