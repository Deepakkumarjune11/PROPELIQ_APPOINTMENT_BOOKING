using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PatientAccess.Application.Appointments.Commands.RegisterPreferredSlot;
using PatientAccess.Application.Appointments.Dtos;
using PatientAccess.Application.Appointments.Queries.GetPatientAppointments;
using PatientAccess.Application.Commands.RegisterForAppointment;
using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Queries.GetAvailability;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Shared;

namespace PatientAccess.Presentation.Controllers;

/// <summary>
/// Exposes appointment-related operations for patients and staff.
/// Base route: <c>api/v1/appointments</c>
/// </summary>
[ApiController]
[Route("api/v1/appointments")]
[Authorize]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IMediator                    _mediator;
    private readonly ICommunicationLogRepository  _communicationLogRepo;
    private readonly IPatientOwnershipValidator   _ownershipValidator;

    public AppointmentsController(
        IMediator                   mediator,
        ICommunicationLogRepository communicationLogRepository,
        IPatientOwnershipValidator  ownershipValidator)
    {
        _mediator             = mediator;
        _communicationLogRepo = communicationLogRepository;
        _ownershipValidator   = ownershipValidator;
    }

    /// <summary>
    /// Returns all available appointment slots within a date range.
    /// Results are served from a 60-second Redis cache (AC-2); on cache miss the database
    /// is queried and the cache is populated (AC-3). p95 response target: 2 seconds (NFR-001).
    /// </summary>
    /// <param name="startDate">Inclusive start date (YYYY-MM-DD, ISO-8601).</param>
    /// <param name="endDate">Inclusive end date (YYYY-MM-DD, ISO-8601).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>200 OK</c> with a list of <see cref="AvailabilitySlotDto"/> records.<br/>
    ///   <c>400 Bad Request</c> when <paramref name="startDate"/> is after <paramref name="endDate"/>.
    /// </returns>
    [HttpGet("availability")]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilitySlotDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        // AC-1/security guard — validate before sending to handler (OWASP A03: no injection surface;
        // DateOnly struct binding means no raw string reaches the database).
        if (startDate > endDate)
        {
            return Problem(
                detail: "startDate must be on or before endDate.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid date range");
        }

        var query  = new GetAvailabilityQuery(startDate, endDate);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Books the specified available appointment slot and creates or identifies the patient record.
    /// Insurance validation is performed as a non-blocking soft-check; the booking always proceeds
    /// regardless of the validation outcome (AC-2, AC-3).
    /// </summary>
    /// <param name="slotId">The appointment slot ID to book.</param>
    /// <param name="request">Patient contact and insurance details.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>201 Created</c> with <see cref="RegisterForAppointmentResponse"/> body.<br/>
    ///   <c>404 Not Found</c> when <paramref name="slotId"/> does not exist.<br/>
    ///   <c>409 Conflict</c> when the slot is already booked.<br/>
    ///   <c>401 Unauthorized</c> when the request is not authenticated.
    /// </returns>
    [HttpPost("{slotId:guid}/register")]
    [ProducesResponseType(typeof(RegisterForAppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterForAppointment(
        [FromRoute] Guid slotId,
        [FromBody]  RegisterForAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
        var command = new RegisterForAppointmentCommand(
            SlotId:            slotId,
            Email:             request.Email,
            Name:              request.Name,
            Dob:               request.Dob,
            Phone:             request.Phone,
            InsuranceProvider: request.InsuranceProvider,
            InsuranceMemberId: request.InsuranceMemberId);

        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(
            actionName:       nameof(RegisterForAppointment),
            routeValues:      new { slotId },
            value:            result);
        }
        catch (SlotAlreadyBookedException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Slot no longer available.",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
    }

    /// <summary>
    /// Returns the PDF appointment confirmation for a completed booking.
    /// Serves the pre-generated PDF stored in <c>communication_log</c> (TR-014).
    /// Returns <c>202 Accepted</c> with a <c>Retry-After: 10</c> header if the PDF job
    /// has not yet run, allowing the frontend to poll.
    /// </summary>
    /// <param name="appointmentId">Booked appointment ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>200 OK</c> with <c>application/pdf</c> content.<br/>
    ///   <c>202 Accepted</c> when the PDF generation job has not yet completed.<br/>
    ///   <c>401 Unauthorized</c> when the request is not authenticated.
    /// </returns>
    [HttpGet("{appointmentId:guid}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAppointmentPdf(
        [FromRoute] Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var pdfBytes = await _communicationLogRepo
            .GetConfirmationPdfBytesAsync(appointmentId, cancellationToken);

        if (pdfBytes is null)
        {
            Response.Headers["Retry-After"] = "10";
            return StatusCode(StatusCodes.Status202Accepted);
        }

        return File(pdfBytes, "application/pdf", "confirmation.pdf");
    }

    // ── US_015: Watchlist endpoints ─────────────────────────────────────────

    /// <summary>
    /// Returns all appointments for the authenticated patient.
    /// Includes <c>preferredSlotDatetime</c> for watchlist badge display on SCR-008.
    /// </summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>200 OK</c> with a list of <see cref="AppointmentDto"/> records.<br/>
    ///   <c>401 Unauthorized</c> when the request is not authenticated.
    /// </returns>
    [HttpGet]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPatientAppointments(CancellationToken cancellationToken)
    {
        var patientId = ExtractPatientId();
        if (patientId is null) return Unauthorized();

        var query  = new GetPatientAppointmentsQuery(patientId.Value);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Registers a preferred swap slot on the watchlist for an existing booked appointment.
    /// Updates <c>Appointment.preferred_slot_id</c> and writes an audit entry (AC-2, DR-008).
    /// </summary>
    /// <param name="appointmentId">The appointment to enroll on the watchlist.</param>
    /// <param name="request">Body containing the preferred slot UTC datetime.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>204 No Content</c> on success.<br/>
    ///   <c>400 Bad Request</c> when appointment status is not <c>Booked</c> or slot datetime is in the past.<br/>
    ///   <c>401 Unauthorized</c> when the request is not authenticated.<br/>
    ///   <c>403 Forbidden</c> when the appointment belongs to a different patient (AC-5, OWASP A01).<br/>
    ///   <c>422 Unprocessable Entity</c> when the selected slot is currently available — patient should book directly.
    /// </returns>
    [HttpPost("{appointmentId:guid}/preferred-slot")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegisterPreferredSlot(
        [FromRoute] Guid appointmentId,
        [FromBody]  RegisterPreferredSlotRequest request,
        CancellationToken cancellationToken)
    {
        var patientId = ExtractPatientId();
        if (patientId is null) return Unauthorized();

        // RBAC: verify appointment belongs to the authenticated patient (OWASP A01 — Broken Access Control).
        var isOwner = await _ownershipValidator.IsOwnerAsync(appointmentId, patientId.Value, cancellationToken);
        if (!isOwner) return Forbid();

        var command = new RegisterPreferredSlotCommand(
            AppointmentId:        appointmentId,
            PatientId:            patientId.Value,
            PreferredSlotDatetime: request.PreferredSlotDatetime);

        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the patient ID from the JWT <c>sub</c> / <c>NameIdentifier</c> claim.
    /// Returns null when the claim is absent or cannot be parsed as a GUID.
    /// </summary>
    private Guid? ExtractPatientId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
    }
}

/// <summary>
/// Request body for <c>POST /api/v1/appointments/{slotId}/register</c>.
/// </summary>
/// <param name="Email">Patient email address.</param>
/// <param name="Name">Patient full name.</param>
/// <param name="Dob">Patient date of birth (ISO-8601 date).</param>
/// <param name="Phone">Patient phone number.</param>
/// <param name="InsuranceProvider">Optional insurance provider name.</param>
/// <param name="InsuranceMemberId">Optional insurance member ID.</param>
public sealed record RegisterForAppointmentRequest(
    string   Email,
    string   Name,
    DateOnly Dob,
    string   Phone,
    string?  InsuranceProvider,
    string?  InsuranceMemberId
);
