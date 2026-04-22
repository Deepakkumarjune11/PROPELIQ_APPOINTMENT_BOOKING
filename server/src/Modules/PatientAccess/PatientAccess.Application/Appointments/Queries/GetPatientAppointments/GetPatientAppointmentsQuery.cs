using MediatR;
using PatientAccess.Application.Appointments.Dtos;

namespace PatientAccess.Application.Appointments.Queries.GetPatientAppointments;

/// <summary>
/// Returns all appointments for the authenticated patient, ordered most-recent-first.
/// Each appointment includes <see cref="AppointmentDto.PreferredSlotDatetime"/> for watchlist badge display (AC-4).
/// </summary>
/// <param name="PatientId">ID extracted from the JWT sub claim by the controller.</param>
public sealed record GetPatientAppointmentsQuery(Guid PatientId)
    : IRequest<IReadOnlyList<AppointmentDto>>;
