using PatientAccess.Application.Appointments.Commands.BookWalkIn;
using PatientAccess.Application.Staff.Dtos;

namespace PatientAccess.Application.Repositories;

/// <summary>
/// Data-layer contract for atomically booking a same-day walk-in appointment (US_016, AC-4).
/// Implementations must use SERIALIZABLE isolation to prevent duplicate queue positions.
/// </summary>
public interface IWalkInBookingRepository
{
    /// <summary>
    /// Atomically books a walk-in or adds to the wait queue.
    /// Throws <c>ConflictException</c> when the patient already has a walk-in today.
    /// </summary>
    Task<WalkInBookingResultDto> BookWalkInAsync(
        BookWalkInCommand command,
        CancellationToken cancellationToken = default);
}
