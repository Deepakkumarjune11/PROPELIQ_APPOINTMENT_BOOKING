using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Appointments.Dtos;
using PatientAccess.Application.Repositories;

namespace PatientAccess.Application.Appointments.Queries.GetPatientAppointments;

/// <summary>
/// Returns all appointments for the authenticated patient via the watchlist repository (US_015, AC-4).
/// Projects repository records to <see cref="AppointmentDto"/> with watchlist status.
/// </summary>
public sealed class GetPatientAppointmentsHandler
    : IRequestHandler<GetPatientAppointmentsQuery, IReadOnlyList<AppointmentDto>>
{
    private readonly IWatchlistRepository _repo;
    private readonly ILogger<GetPatientAppointmentsHandler> _logger;

    public GetPatientAppointmentsHandler(
        IWatchlistRepository repo,
        ILogger<GetPatientAppointmentsHandler> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppointmentDto>> Handle(
        GetPatientAppointmentsQuery request,
        CancellationToken cancellationToken)
    {
        var records = await _repo.GetPatientAppointmentsAsync(request.PatientId, cancellationToken);

        _logger.LogDebug(
            "GetPatientAppointments: patientId={PatientId} count={Count}",
            request.PatientId, records.Count);

        return records
            .Select(r => new AppointmentDto(
                Id:                   r.Id,
                SlotDatetime:         r.SlotDatetime.ToString("o"),
                ProviderName:         null,   // Not stored on Appointment entity in current schema
                ProviderId:           null,   // Not stored on Appointment entity in current schema
                VisitType:            null,   // Not stored on Appointment entity in current schema
                Status:               Enum.Parse<PatientAccess.Domain.Enums.AppointmentStatus>(r.Status, ignoreCase: true),
                PreferredSlotDatetime: r.PreferredSlotDatetime.HasValue
                    ? r.PreferredSlotDatetime.Value.ToString("o")
                    : null))
            .ToList()
            .AsReadOnly();
    }
}
