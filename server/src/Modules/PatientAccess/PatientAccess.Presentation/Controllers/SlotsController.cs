using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PatientAccess.Application.Slots.Dtos;
using PatientAccess.Application.Slots.Queries.GetSlotAvailability;

namespace PatientAccess.Presentation.Controllers;

/// <summary>
/// Exposes slot availability information for the preferred slot calendar on SCR-009.
/// Base route: <c>api/v1/slots</c>
/// </summary>
[ApiController]
[Route("api/v1/slots")]
[Authorize]
public sealed class SlotsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SlotsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns all appointment slots for a given provider and calendar month.
    /// Each slot includes an <c>isAvailable</c> flag used by SCR-009 to distinguish:
    /// <list type="bullet">
    ///   <item><c>isAvailable = false</c> — booked/arrived; selectable for watchlist enrollment (AC-1).</item>
    ///   <item><c>isAvailable = true</c>  — open for direct booking; disabled in SCR-009 calendar.</item>
    /// </list>
    /// NOTE: <paramref name="providerId"/> is accepted for forward-compatibility.
    /// Provider-level filtering requires a schema change that is out of scope for this task.
    /// </summary>
    /// <param name="providerId">Provider identifier (accepted, not yet filtered).</param>
    /// <param name="year">Calendar year, e.g. 2026.</param>
    /// <param name="month">Calendar month, 1-indexed (1=January … 12=December).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    ///   <c>200 OK</c> with <see cref="IReadOnlyList{SlotAvailabilityDto}"/>.<br/>
    ///   <c>400 Bad Request</c> when <paramref name="month"/> is outside 1–12.<br/>
    ///   <c>401 Unauthorized</c> when the request is not authenticated.
    /// </returns>
    [HttpGet("availability")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(typeof(IReadOnlyList<SlotAvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSlotAvailability(
        [FromQuery] Guid? providerId,
        [FromQuery] int   year,
        [FromQuery] int   month,
        CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12)
        {
            return Problem(
                detail: "month must be between 1 and 12.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid month");
        }

        var query  = new GetSlotAvailabilityQuery(providerId, year, month);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
