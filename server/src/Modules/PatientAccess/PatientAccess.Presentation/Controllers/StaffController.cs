using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PatientAccess.Application.Appointments.Commands.BookWalkIn;
using PatientAccess.Application.Appointments.Commands.UpdateAppointmentStatus;
using PatientAccess.Application.Staff.Commands.ReorderQueue;
using PatientAccess.Application.Staff.Dtos;
using PatientAccess.Application.Staff.Queries.GetDashboardSummary;
using PatientAccess.Application.Staff.Queries.GetSameDayQueue;
using PatientAccess.Domain.Enums;
using System.Security.Claims;

namespace PatientAccess.Presentation.Controllers;

/// <summary>
/// Staff-only operations: walk-in booking and dashboard summary (US_016, FR-008).
/// All endpoints require <c>Staff</c> role — non-staff requests return 403 (AC-1).
/// Base routes:
///   <c>api/v1/appointments/walk-in</c>
///   <c>api/v1/staff/dashboard/summary</c>
/// </summary>
[ApiController]
[Authorize(Roles = "Staff")]
public sealed class StaffController : ControllerBase
{
    private readonly IMediator _mediator;

    public StaffController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Books a same-day walk-in appointment or places the patient on the wait queue.
    /// Queue position is assigned atomically within a SERIALIZABLE transaction (AC-4/AC-5).
    /// </summary>
    /// <param name="request">Patient ID and visit type.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>200 OK</c> with <see cref="WalkInBookingResultDto"/> (<c>waitQueue: false</c> when slot assigned,
    ///   <c>waitQueue: true</c> when no slot available).<br/>
    ///   <c>409 Conflict</c> when the patient already has a walk-in today.<br/>
    ///   <c>403 Forbidden</c> for non-Staff roles.
    /// </returns>
    [HttpPost("api/v1/appointments/walk-in")]
    [ProducesResponseType(typeof(WalkInBookingResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BookWalkIn(
        [FromBody] BookWalkInRequest request,
        CancellationToken cancellationToken)
    {
        var staffId = Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

        var command = new BookWalkInCommand(request.PatientId, request.VisitType, staffId);
        var result  = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns aggregate summary counts for the staff dashboard: walk-ins today,
    /// queue length, verification pending, and critical conflicts (SCR-010).
    /// </summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>200 OK</c> with <see cref="DashboardSummaryDto"/>.<br/>
    ///   <c>403 Forbidden</c> for non-Staff roles.
    /// </returns>
    [HttpGet("api/v1/staff/dashboard/summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDashboardSummaryQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns today's same-day queue ordered by queue position (US_017, AC-1, AC-4).
    /// Response is Redis-cached for 30 seconds; on cache miss the DB is queried directly.
    /// </summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>200 OK</c> with ordered <see cref="QueueEntryDto"/> array.<br/>
    ///   <c>403 Forbidden</c> for non-Staff roles.
    /// </returns>
    [HttpGet("api/v1/staff/queue")]
    [ProducesResponseType(typeof(IReadOnlyList<QueueEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSameDayQueue(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSameDayQueueQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Persists a drag-and-drop reorder of the same-day queue (US_017, AC-2).
    /// Updates <c>queue_position</c> for each appointment and broadcasts <c>QueueUpdated</c>.
    /// </summary>
    /// <param name="request">New ordered appointment IDs array.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>204 No Content</c> on success.<br/>
    ///   <c>403 Forbidden</c> for non-Staff roles.
    /// </returns>
    [HttpPatch("api/v1/staff/queue/reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReorderQueue(
        [FromBody] ReorderQueueRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReorderQueueCommand(request.OrderedAppointmentIds);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Transitions a same-day appointment status (US_017, AC-3).
    /// Valid targets: <c>arrived</c>, <c>in-room</c>, <c>left</c>.
    /// Writes an AuditLog entry and broadcasts <c>QueueUpdated</c> via SignalR.
    /// </summary>
    /// <param name="id">Appointment GUID.</param>
    /// <param name="request">New status string.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>204 No Content</c> on success.<br/>
    ///   <c>404 Not Found</c> when appointment is not in today's queue.<br/>
    ///   <c>422 Unprocessable Entity</c> for disallowed status values.<br/>
    ///   <c>403 Forbidden</c> for non-Staff roles.
    /// </returns>
    [HttpPatch("api/v1/appointments/{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAppointmentStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        // Parse status string to enum value (case-insensitive).
        if (!Enum.TryParse<AppointmentStatus>(
                request.Status,
                ignoreCase: true,
                out var newStatus))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Invalid status value.",
                Detail = $"'{request.Status}' is not a valid status. Allowed: arrived, in-room, left.",
                Status = StatusCodes.Status422UnprocessableEntity,
            });
        }

        var staffId = Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier), out var sid) ? sid : Guid.Empty;

        var command = new UpdateAppointmentStatusCommand(id, newStatus, staffId);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }
}

/// <summary>Request body for <see cref="StaffController.BookWalkIn"/>.</summary>
/// <param name="PatientId">Patient to book a walk-in for (must exist).</param>
/// <param name="VisitType">Reason for visit: General, Follow-Up, Urgent Care.</param>
public sealed record BookWalkInRequest(
    Guid   PatientId,
    string VisitType);

/// <summary>Request body for <see cref="StaffController.ReorderQueue"/>.</summary>
/// <param name="OrderedAppointmentIds">Appointment GUIDs in the desired new queue order.</param>
public sealed record ReorderQueueRequest(IReadOnlyList<Guid> OrderedAppointmentIds);

/// <summary>Request body for <see cref="StaffController.UpdateAppointmentStatus"/>.</summary>
/// <param name="Status">Target status string: <c>arrived</c>, <c>in-room</c>, <c>left</c>.</param>
public sealed record UpdateStatusRequest(string Status);

