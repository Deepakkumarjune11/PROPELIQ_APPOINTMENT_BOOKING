using MediatR;
using PatientAccess.Application.Staff.Dtos;

namespace PatientAccess.Application.Appointments.Commands.BookWalkIn;

/// <summary>
/// Books a same-day walk-in appointment or places the patient on the wait queue (US_016, AC-4/AC-5).
/// The queue position is assigned atomically within a SERIALIZABLE transaction.
/// </summary>
/// <param name="PatientId">Existing patient to book for.</param>
/// <param name="VisitType">Reason for visit (General, Follow-Up, Urgent Care).</param>
/// <param name="StaffId">Acting staff member's ID — written to AuditLog.</param>
public sealed record BookWalkInCommand(
    Guid   PatientId,
    string VisitType,
    Guid   StaffId
) : IRequest<WalkInBookingResultDto>;
