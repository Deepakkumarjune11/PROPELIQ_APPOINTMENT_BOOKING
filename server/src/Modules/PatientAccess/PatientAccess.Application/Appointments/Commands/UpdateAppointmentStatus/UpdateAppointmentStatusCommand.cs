using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Application.Repositories;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Application.Appointments.Commands.UpdateAppointmentStatus;

/// <summary>
/// Transitions a same-day appointment to a new arrival status (US_017, AC-3).
/// Valid target statuses: <c>Arrived</c>, <c>InRoom</c>, <c>Left</c>.
/// </summary>
/// <param name="AppointmentId">The appointment to update.</param>
/// <param name="NewStatus">The desired new status.</param>
/// <param name="StaffId">Acting staff member — written to the AuditLog (DR-008).</param>
public sealed record UpdateAppointmentStatusCommand(
    Guid              AppointmentId,
    AppointmentStatus NewStatus,
    Guid              StaffId) : IRequest;

/// <summary>
/// Handles <see cref="UpdateAppointmentStatusCommand"/>.
/// Validates the transition, delegates DB mutation + AuditLog to <see cref="IQueueRepository"/>,
/// then invalidates Redis cache and broadcasts <c>QueueUpdated</c> via SignalR (AC-3).
/// </summary>
public sealed class UpdateAppointmentStatusHandler : IRequestHandler<UpdateAppointmentStatusCommand>
{
    private const string CacheKey = "staff:queue:today";

    private static readonly HashSet<AppointmentStatus> AllowedTargets =
    [
        AppointmentStatus.Arrived,
        AppointmentStatus.InRoom,
        AppointmentStatus.Left,
    ];

    private readonly IQueueRepository                          _repo;
    private readonly ICacheService                             _cache;
    private readonly IQueueBroadcastService                    _broadcast;
    private readonly ILogger<UpdateAppointmentStatusHandler>   _logger;

    public UpdateAppointmentStatusHandler(
        IQueueRepository                        repo,
        ICacheService                            cache,
        IQueueBroadcastService                   broadcast,
        ILogger<UpdateAppointmentStatusHandler>  logger)
    {
        _repo      = repo;
        _cache     = cache;
        _broadcast = broadcast;
        _logger    = logger;
    }

    public async Task Handle(UpdateAppointmentStatusCommand command, CancellationToken cancellationToken)
    {
        // Validate the requested target status (whitelist — prevents invalid transitions).
        if (!AllowedTargets.Contains(command.NewStatus))
            throw new UnprocessableEntityException(
                $"Invalid status transition. Allowed: {string.Join(", ", AllowedTargets)}.");

        // Delegate DB mutation + AuditLog write to the Data layer.
        var previousStatus = await _repo.UpdateAppointmentStatusAsync(
            command.AppointmentId,
            command.NewStatus,
            command.StaffId,
            cancellationToken);

        // Invalidate Redis cache + broadcast after commit (AC-3).
        await _cache.RemoveAsync(CacheKey, cancellationToken);
        await _broadcast.BroadcastQueueUpdatedAsync(cancellationToken);

        _logger.LogInformation(
            "Appointment {Id}: status {From} => {To} by staff {StaffId}.",
            command.AppointmentId, previousStatus, command.NewStatus, command.StaffId);
    }
}
