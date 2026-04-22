using PatientAccess.Application.Appointments.Dtos;
using PatientAccess.Application.Slots.Dtos;

namespace PatientAccess.Application.Repositories;

/// <summary>
/// Data record returned by the watchlist appointment query — keeps Application independent of EF Core entities.
/// </summary>
/// <param name="Id">Appointment identifier.</param>
/// <param name="SlotDatetime">UTC datetime of the booked slot.</param>
/// <param name="Status">Current appointment lifecycle status.</param>
/// <param name="PreferredSlotDatetime">Preferred slot UTC datetime, or null when not on watchlist.</param>
public sealed record PatientAppointmentData(
    Guid     Id,
    DateTime SlotDatetime,
    string   Status,                     // stored as string per AppointmentConfiguration
    DateTime? PreferredSlotDatetime);

/// <summary>
/// Data record for slot availability — keeps Application independent of EF Core entities.
/// </summary>
/// <param name="Id">Slot (appointment row) identifier.</param>
/// <param name="SlotDatetime">UTC datetime of the slot.</param>
/// <param name="IsAvailable">True when slot is open; false when booked/arrived.</param>
public sealed record SlotData(Guid Id, DateTime SlotDatetime, bool IsAvailable);

/// <summary>
/// Repository contract for preferred slot swap watchlist operations (US_015).
/// Implemented in PatientAccess.Data; consumed by CQRS handlers in PatientAccess.Application.
/// </summary>
public interface IWatchlistRepository
{
    /// <summary>
    /// Returns all non-deleted appointments for the given patient, ordered by slot datetime descending.
    /// Includes the preferred slot datetime when the patient is on the watchlist.
    /// </summary>
    Task<IReadOnlyList<PatientAppointmentData>> GetPatientAppointmentsAsync(
        Guid patientId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all appointment slots in the given calendar month, with availability flags.
    /// IsAvailable = false when <c>Status IN (Booked, Arrived)</c> — eligible for watchlist.
    /// IsAvailable = true  when <c>Status = Available</c> — patient can book directly.
    /// </summary>
    Task<IReadOnlyList<SlotData>> GetSlotsForMonthAsync(
        int year,
        int month,
        CancellationToken ct = default);

    /// <summary>
    /// Finds the appointment whose <c>SlotDatetime</c> matches the preferred datetime
    /// and whose <c>Status</c> is <c>Booked</c> or <c>Arrived</c>.
    /// Returns null when no such appointment exists (slot is available or does not exist).
    /// </summary>
    Task<Guid?> FindBookedSlotByDatetimeAsync(
        DateTime preferredSlotUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Sets <c>appointment.PreferredSlotId = preferredSlotId</c> and writes an immutable
    /// audit log entry in the same <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync"/> call (DR-008).
    /// </summary>
    Task RegisterPreferredSlotAsync(
        Guid appointmentId,
        Guid preferredSlotId,
        Guid patientId,
        CancellationToken ct = default);
}
