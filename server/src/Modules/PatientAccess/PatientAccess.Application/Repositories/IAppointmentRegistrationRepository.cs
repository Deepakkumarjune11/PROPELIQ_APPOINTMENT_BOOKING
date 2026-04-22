using PatientAccess.Application.Exceptions;

namespace PatientAccess.Application.Repositories;

/// <summary>
/// Result returned after a successful registration transaction.
/// </summary>
/// <param name="PatientId">The ID of the created or matched patient record.</param>
public sealed record AppointmentRegistrationData(Guid PatientId);

/// <summary>
/// Repository contract for the appointment registration transaction.
/// Executes patient-upsert, slot-booking, and audit-log insertion in a single DB transaction.
/// Defined in Application so handlers depend on the abstraction — implemented in PatientAccess.Data.
/// </summary>
public interface IAppointmentRegistrationRepository
{
    /// <summary>
    /// Returns the <c>SlotDatetime</c> for the given appointment slot.
    /// Used by the handler to calculate no-show risk before the registration transaction.
    /// </summary>
    /// <exception cref="NotFoundException">Thrown when <paramref name="slotId"/> does not exist.</exception>
    Task<DateTime> GetSlotDatetimeAsync(Guid slotId, CancellationToken ct = default);

    /// <summary>
    /// Runs the full registration transaction.
    /// </summary>
    /// <param name="slotId">Appointment slot to transition from Available → Booked.</param>
    /// <param name="email">Patient email — natural key for upsert (AC-4).</param>
    /// <param name="name">Patient full name (updated on existing record).</param>
    /// <param name="dob">Patient date of birth.</param>
    /// <param name="phone">Patient contact phone (updated on existing record).</param>
    /// <param name="insuranceProvider">Optional insurance provider name.</param>
    /// <param name="insuranceMemberId">Optional insurance member ID.</param>
    /// <param name="insuranceStatus">Pre-computed validation result string to persist.</param>
    /// <param name="noShowRiskScore">Pre-computed no-show risk score to persist atomically (AC-3).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="AppointmentRegistrationData"/> on success.</returns>
    /// <exception cref="NotFoundException">Thrown when <paramref name="slotId"/> does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the slot status is not Available.</exception>
    /// <exception cref="SlotAlreadyBookedException">Thrown when a concurrent booking wins the xmin race (AC-4).</exception>
    Task<AppointmentRegistrationData> RegisterAsync(
        Guid     slotId,
        string   email,
        string   name,
        DateOnly dob,
        string   phone,
        string?  insuranceProvider,
        string?  insuranceMemberId,
        string   insuranceStatus,
        decimal? noShowRiskScore,
        CancellationToken ct = default);
}
