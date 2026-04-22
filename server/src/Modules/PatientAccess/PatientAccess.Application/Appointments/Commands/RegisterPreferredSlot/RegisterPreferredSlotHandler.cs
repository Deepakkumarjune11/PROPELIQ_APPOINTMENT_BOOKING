using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Repositories;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Application.Appointments.Commands.RegisterPreferredSlot;

/// <summary>
/// Handles watchlist registration for a preferred slot swap (US_015, FR-004).
///
/// Eligibility validation chain (task spec steps 1–5):
/// <list type="number">
///   <item>Ownership: appointment PatientId == currentPatientId → 403 if mismatch (checked in controller via <c>IPatientOwnershipValidator</c>).</item>
///   <item>Status: appointment must be <c>Booked</c> → <see cref="ConflictException"/> (400) if not.</item>
///   <item>Slot availability: preferred slot must NOT be available → <see cref="UnprocessableEntityException"/> (422) if available/missing.</item>
///   <item>Future datetime: preferred slot must be in the future → <see cref="ConflictException"/> (400) if past.</item>
///   <item>Persist: <c>appointment.PreferredSlotId = preferredSlotId</c> + audit log in one SaveChanges.</item>
/// </list>
/// NOTE: Ownership (step 1) is performed in the controller before the command is dispatched
/// to avoid an extra DB round-trip on the happy path. The handler assumes the controller already
/// enforced this guard and proceeds to validate status and slot eligibility.
/// </summary>
public sealed class RegisterPreferredSlotHandler : IRequestHandler<RegisterPreferredSlotCommand>
{
    private readonly IWatchlistRepository _repo;
    private readonly ILogger<RegisterPreferredSlotHandler> _logger;

    public RegisterPreferredSlotHandler(
        IWatchlistRepository repo,
        ILogger<RegisterPreferredSlotHandler> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task Handle(
        RegisterPreferredSlotCommand cmd,
        CancellationToken cancellationToken)
    {
        var preferredUtc = cmd.PreferredSlotDatetime.UtcDateTime;

        // ── Step 2: Verify appointment status == Booked ───────────────────────
        // Load patient's appointments to verify the status.
        var appointments = await _repo.GetPatientAppointmentsAsync(cmd.PatientId, cancellationToken);
        var appointment  = appointments.FirstOrDefault(a => a.Id == cmd.AppointmentId);

        if (appointment is null)
            throw new NotFoundException($"Appointment {cmd.AppointmentId} not found.");

        if (!Enum.TryParse<AppointmentStatus>(appointment.Status, ignoreCase: true, out var status)
            || status != AppointmentStatus.Booked)
        {
            throw new ConflictException(
                $"Appointment {cmd.AppointmentId} must be in 'Booked' status to enroll on the watchlist. Current status: {appointment.Status}.");
        }

        // ── Step 3: Verify preferred slot is NOT available (must be booked by another patient) ─
        var preferredSlotId = await _repo.FindBookedSlotByDatetimeAsync(preferredUtc, cancellationToken);
        if (preferredSlotId is null)
        {
            throw new UnprocessableEntityException(
                "The selected slot is currently available. Please book it directly rather than watchlisting it.");
        }

        // ── Step 4: Verify preferred slot datetime is in the future ──────────
        if (preferredUtc <= DateTime.UtcNow)
        {
            throw new ConflictException(
                "The preferred slot datetime must be in the future.");
        }

        // ── Step 5: Persist preferred_slot_id + audit log ────────────────────
        await _repo.RegisterPreferredSlotAsync(
            cmd.AppointmentId,
            preferredSlotId.Value,
            cmd.PatientId,
            cancellationToken);

        _logger.LogInformation(
            "WatchlistRegistered: appointmentId={AppointmentId} patientId={PatientId} preferredSlotId={PreferredSlotId}",
            cmd.AppointmentId, cmd.PatientId, preferredSlotId.Value);
    }
}
