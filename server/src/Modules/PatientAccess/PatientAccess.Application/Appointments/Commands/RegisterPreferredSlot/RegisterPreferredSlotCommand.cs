using MediatR;

namespace PatientAccess.Application.Appointments.Commands.RegisterPreferredSlot;

/// <summary>
/// Registers the patient's preferred swap slot on the watchlist (US_015, AC-2).
/// Updates <c>Appointment.PreferredSlotId</c> and writes an audit log entry.
/// </summary>
/// <param name="AppointmentId">The patient's current booked appointment to enroll on the watchlist.</param>
/// <param name="PatientId">Authenticated patient ID — extracted from JWT sub claim by the controller.</param>
/// <param name="PreferredSlotDatetime">The preferred slot UTC datetime to watchlist.</param>
public sealed record RegisterPreferredSlotCommand(
    Guid             AppointmentId,
    Guid             PatientId,
    DateTimeOffset   PreferredSlotDatetime
) : IRequest;

/// <summary>Request body for <c>POST /api/v1/appointments/{appointmentId}/preferred-slot</c>.</summary>
public sealed record RegisterPreferredSlotRequest(DateTimeOffset PreferredSlotDatetime);
