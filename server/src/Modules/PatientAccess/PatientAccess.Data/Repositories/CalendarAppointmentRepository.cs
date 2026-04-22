using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Repositories;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICalendarAppointmentRepository"/>.
/// Projects only the <c>SlotDatetime</c> column — no PHI fields are read (DR-015).
/// </summary>
public sealed class CalendarAppointmentRepository : ICalendarAppointmentRepository
{
    private readonly PropelIQDbContext _dbContext;

    public CalendarAppointmentRepository(PropelIQDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<AppointmentCalendarData?> GetCalendarDataAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        return await _dbContext.Appointments
            .Where(a => a.Id == appointmentId)
            .Select(a => new AppointmentCalendarData(a.SlotDatetime))
            .FirstOrDefaultAsync(ct);
    }
}
