namespace PatientAccess.Application.Staff.Dtos;

/// <summary>
/// Single entry in the same-day staff queue (US_017, AC-1).
/// </summary>
/// <param name="AppointmentId">Unique identifier of the appointment.</param>
/// <param name="QueuePosition">1-based ordinal position in today's queue.</param>
/// <param name="PatientName">Patient display name.</param>
/// <param name="AppointmentTime">Slot date-time (UTC, ISO 8601).</param>
/// <param name="Status">Current status string: waiting, arrived, in-room, completed, left.</param>
/// <param name="VisitType">Reason for visit (e.g., General, Follow-Up, Urgent Care).</param>
public sealed record QueueEntryDto(
    Guid           AppointmentId,
    int            QueuePosition,
    string         PatientName,
    DateTimeOffset AppointmentTime,
    string         Status,
    string         VisitType);
