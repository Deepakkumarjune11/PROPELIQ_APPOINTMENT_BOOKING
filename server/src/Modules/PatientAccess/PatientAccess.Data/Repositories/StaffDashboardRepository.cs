using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Staff.Dtos;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IStaffDashboardRepository"/>.
/// Runs 4 aggregate COUNT queries in parallel (read-only, no transaction needed).
/// </summary>
public sealed class StaffDashboardRepository : IStaffDashboardRepository
{
    private readonly PropelIQDbContext _db;

    public StaffDashboardRepository(PropelIQDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        // Use >= / < range filter on SlotDatetime so EF Core translates to a
        // sargable predicate that hits ix_appointments_slot_datetime_status_active.
        // DateOnly.FromDateTime() in a WHERE clause is not translatable to an index seek.
        // HasQueryFilter on Appointment already excludes IsDeleted=true rows.
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd   = todayStart.AddDays(1);

        // DbContext is not thread-safe — run sequentially.
        var walkInsToday = await _db.Appointments
            .CountAsync(a => a.SlotDatetime >= todayStart
                             && a.SlotDatetime < todayEnd
                             && a.IsWalkIn, cancellationToken);

        var queueLength = await _db.Appointments
            .CountAsync(a => a.SlotDatetime >= todayStart
                             && a.SlotDatetime < todayEnd
                             && a.IsWalkIn
                             && a.Status == AppointmentStatus.Booked, cancellationToken);

        var verificationPending = await _db.PatientViews360
            .CountAsync(v => v.VerificationStatus == VerificationStatus.Pending, cancellationToken);

        var criticalConflicts = await _db.PatientViews360
            .CountAsync(v => v.ConflictFlags.Any(), cancellationToken);

        return new DashboardSummaryDto(
            WalkInsToday:        walkInsToday,
            QueueLength:         queueLength,
            VerificationPending: verificationPending,
            CriticalConflicts:   criticalConflicts);
    }
}
