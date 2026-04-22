namespace PatientAccess.Application.Staff.Dtos;

/// <summary>
/// Result returned after a walk-in booking or wait-queue placement (US_016, AC-4/AC-5).
/// </summary>
/// <param name="AppointmentId">Identifier of the created appointment (null when wait-queue only).</param>
/// <param name="QueuePosition">1-based position in today's same-day queue.</param>
/// <param name="WaitQueue">
///   <c>true</c> when no same-day slots were available — patient is on the wait queue (AC-5).
///   <c>false</c> when a slot was assigned and the appointment is booked (AC-4).
/// </param>
public sealed record WalkInBookingResultDto(
    Guid? AppointmentId,
    int   QueuePosition,
    bool  WaitQueue);

/// <summary>
/// Aggregate summary counts for the staff dashboard (US_016, SCR-010).
/// </summary>
/// <param name="WalkInsToday">Total walk-in appointments created today.</param>
/// <param name="QueueLength">Number of booked (not yet arrived/completed) walk-ins today.</param>
/// <param name="VerificationPending">PatientView360 records with Pending verification status.</param>
/// <param name="CriticalConflicts">PatientView360 records that have at least one conflict flag.</param>
public sealed record DashboardSummaryDto(
    int WalkInsToday,
    int QueueLength,
    int VerificationPending,
    int CriticalConflicts);
