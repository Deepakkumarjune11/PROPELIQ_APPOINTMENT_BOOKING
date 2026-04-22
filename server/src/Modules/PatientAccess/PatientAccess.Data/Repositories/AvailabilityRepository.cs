using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Repositories;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAvailabilityRepository"/>.
/// Queries <see cref="PropelIQDbContext.Appointments"/> for rows with
/// <c>Status = Available</c> within the requested date window.
///
/// <see cref="AsNoTracking"/> is used throughout — read-only query, no change-tracking overhead.
/// Projects directly to <see cref="AvailabilitySlotData"/> to keep the Application layer
/// independent of EF Core entity types.
/// </summary>
public sealed class AvailabilityRepository : IAvailabilityRepository
{
    private readonly PropelIQDbContext _db;

    public AvailabilityRepository(PropelIQDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AvailabilitySlotData>> GetAvailableSlotsAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        // Convert DateOnly to UTC DateTime range for the SlotDatetime column comparison.
        var startUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc   = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _db.Appointments
            .AsNoTracking()
            .Where(a =>
                a.Status == AppointmentStatus.Available &&
                a.SlotDatetime >= startUtc &&
                a.SlotDatetime <= endUtc &&
                !a.IsDeleted)
            .OrderBy(a => a.SlotDatetime)
            .Select(a => new AvailabilitySlotData(a.Id, a.SlotDatetime, a.NoShowRiskScore))
            .ToListAsync(ct);
    }
}
