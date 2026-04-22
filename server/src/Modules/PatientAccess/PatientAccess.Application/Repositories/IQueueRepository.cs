using PatientAccess.Application.Staff.Dtos;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Application.Repositories;

/// <summary>
/// Data-layer contract for same-day queue read and mutation operations (US_017).
/// </summary>
public interface IQueueRepository
{
    /// <summary>
    /// Returns today's active queue entries ordered by <c>queue_position</c>.
    /// Excludes <c>Left</c> and <c>Completed</c> statuses from the active view.
    /// </summary>
    Task<IReadOnlyList<QueueEntryDto>> GetTodayQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-updates <c>queue_position</c> for the given ordered appointment IDs
    /// using <c>ExecuteUpdateAsync</c> (no entity load, minimal locking).
    /// </summary>
    Task BulkUpdateQueuePositionsAsync(
        IReadOnlyList<Guid> orderedIds,
        CancellationToken   cancellationToken = default);

    /// <summary>
    /// Transitions a same-day appointment's status and writes an AuditLog entry in
    /// one <c>SaveChangesAsync</c> call. Returns the previous status for logging.
    /// Throws <c>NotFoundException</c> when the appointment does not exist in today's queue.
    /// </summary>
    Task<AppointmentStatus> UpdateAppointmentStatusAsync(
        Guid              appointmentId,
        AppointmentStatus newStatus,
        Guid              staffId,
        CancellationToken cancellationToken = default);
}
