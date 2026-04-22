using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PatientAccess.Application.Commands.SubmitIntake;
using PatientAccess.Application.Models;
using PatientAccess.Application.Patients.Commands.CreatePatientByStaff;
using PatientAccess.Application.Patients.Dtos;
using PatientAccess.Application.Patients.Queries.SearchPatients;
using PatientAccess.Application.Services;
using System.Security.Claims;

namespace PatientAccess.Presentation.Controllers;

/// <summary>
/// Exposes patient-scoped actions.
/// Base route: <c>api/v1/patients</c>
/// </summary>
[ApiController]
[Route("api/v1/patients")]
[Authorize]
public sealed class PatientsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConversationalIntakeService _conversationalIntakeService;

    public PatientsController(
        IMediator mediator,
        IConversationalIntakeService conversationalIntakeService)
    {
        _mediator                    = mediator;
        _conversationalIntakeService = conversationalIntakeService;
    }

    /// <summary>
    /// Searches patients by email or phone (partial, case-insensitive). Staff-only (FR-008, AC-2).
    /// </summary>
    /// <param name="q">Search term — minimum 2 characters.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>200 OK</c> with up to 10 matching <see cref="PatientSearchResultDto"/> records.<br/>
    ///   <c>400 Bad Request</c> when <paramref name="q"/> is shorter than 2 characters.<br/>
    ///   <c>403 Forbidden</c> for non-Staff roles (AC-1).
    /// </returns>
    [HttpGet("search")]
    [Authorize(Roles = "Staff")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchPatients(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(new ProblemDetails
            {
                Title  = "Search term too short.",
                Detail = "The search term must be at least 2 characters.",
                Status = StatusCodes.Status400BadRequest,
            });

        var results = await _mediator.Send(new SearchPatientsQuery(q.Trim()), cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Creates a minimal patient profile from the staff walk-in booking flow (FR-008, AC-3).
    /// Returns 409 when a patient with the same email already exists.
    /// </summary>
    /// <param name="request">Minimal patient fields: full name, email, phone.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>201 Created</c> with the new <see cref="PatientSearchResultDto"/>.<br/>
    ///   <c>409 Conflict</c> when email is already registered.<br/>
    ///   <c>403 Forbidden</c> for non-Staff roles.
    /// </returns>
    [HttpPost]
    [Authorize(Roles = "Staff")]
    [ProducesResponseType(typeof(PatientSearchResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePatientByStaff(
        [FromBody] CreatePatientByStaffRequest request,
        CancellationToken cancellationToken)
    {
        var staffId = Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

        var command = new CreatePatientByStaffCommand(
            request.FullName,
            request.Email,
            request.Phone,
            staffId);

        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(
            actionName: nameof(SearchPatients),
            routeValues: null,
            value: result);
    }

    /// <summary>
    /// Submits the patient's intake questionnaire answers for a booked appointment.
    /// Each call creates an independent <c>IntakeResponse</c> record; double-submit is allowed
    /// (clinical history retains all submissions). Answers are encrypted at rest (DR-015, OWASP A02).
    /// </summary>
    /// <param name="patientId">The patient's unique identifier (from <c>booking-store.patientDetails.patientId</c>).</param>
    /// <param name="request">Intake mode and answers dictionary.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>201 Created</c> with a <see cref="SubmitIntakeResponse"/> body containing the new <c>IntakeResponseId</c>.<br/>
    ///   <c>404 Not Found</c> when <paramref name="patientId"/> does not resolve to an existing patient.<br/>
    ///   <c>401 Unauthorized</c> when the request is not authenticated (NFR-004).
    /// </returns>
    [HttpPost("{patientId:guid}/intake")]
    [ProducesResponseType(typeof(SubmitIntakeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitIntake(
        [FromRoute] Guid patientId,
        [FromBody] SubmitIntakeRequest request,
        CancellationToken cancellationToken)
    {
        var command  = new SubmitIntakeCommand(patientId, request.Mode, request.Answers);
        var response = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(
            actionName: nameof(SubmitIntake),
            routeValues: new { patientId },
            value: response);
    }

    /// <summary>
    /// Sends a patient message to the AI intake assistant and returns the next AI response.
    /// Conversation history is managed server-side in Redis (TTL 10 minutes).
    /// On completion, structured answers are persisted as an <c>IntakeResponse</c> with
    /// <c>mode="conversational"</c> (AC-4). On AI service unavailability, returns
    /// <c>fallbackToManual: true</c> — no exception is propagated (AC-5 / AIR-O02).
    /// </summary>
    /// <param name="patientId">The patient's unique identifier.</param>
    /// <param name="request">Incoming message text and client-side conversation history.</param>
    /// <param name="cancellationToken">Request cancellation token (supports AIR-Q02 p95 ≤ 3s enforcement).</param>
    /// <returns>
    ///   <c>200 OK</c> with <see cref="IntakeChatResponse"/> body.<br/>
    ///   <c>404 Not Found</c> when <paramref name="patientId"/> does not exist.<br/>
    ///   <c>401 Unauthorized</c> when the request is not authenticated (NFR-004).
    /// </returns>
    [HttpPost("{patientId:guid}/intake/chat")]
    [ProducesResponseType(typeof(IntakeChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendIntakeChatMessage(
        [FromRoute] Guid patientId,
        [FromBody] IntakeChatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _conversationalIntakeService.SendMessageAsync(
            patientId, request, cancellationToken);
        return Ok(result);
    }
}

/// <summary>Request body for <see cref="PatientsController.SubmitIntake"/>.</summary>
/// <param name="Mode">Intake channel — "manual" or "conversational".</param>
/// <param name="Answers">Map of questionId → free-text answer. Empty payload is accepted.</param>
public sealed record SubmitIntakeRequest(
    string Mode,
    Dictionary<string, string> Answers);

/// <summary>Request body for <see cref="PatientsController.CreatePatientByStaff"/>.</summary>
/// <param name="FullName">Patient full name (required).</param>
/// <param name="Email">Patient email — must be unique (required).</param>
/// <param name="Phone">Patient phone number (required).</param>
public sealed record CreatePatientByStaffRequest(
    string FullName,
    string Email,
    string Phone);
