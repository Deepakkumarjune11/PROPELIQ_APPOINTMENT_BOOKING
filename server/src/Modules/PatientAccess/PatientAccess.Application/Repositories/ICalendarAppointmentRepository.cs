namespace PatientAccess.Application.Repositories;

/// <summary>
/// Minimal appointment data needed to build a calendar event (AC-3).
/// </summary>
/// <param name="SlotDatetime">UTC datetime of the appointment start.</param>
public sealed record AppointmentCalendarData(DateTime SlotDatetime);

/// <summary>
/// Read-only repository for retrieving appointment data required by calendar sync (TR-012).
/// Defined in Application so <see cref="ICalendarService"/> implementations do not reference
/// the Data layer directly, maintaining layered architecture (DR-001).
/// </summary>
public interface ICalendarAppointmentRepository
{
    /// <summary>
    /// Returns calendar-relevant data for the given appointment, or <see langword="null"/>
    /// if the appointment does not exist.
    /// </summary>
    Task<AppointmentCalendarData?> GetCalendarDataAsync(Guid appointmentId, CancellationToken ct = default);
}
