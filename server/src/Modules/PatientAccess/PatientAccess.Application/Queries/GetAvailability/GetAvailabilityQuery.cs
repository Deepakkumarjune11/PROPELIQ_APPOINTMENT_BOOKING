using MediatR;

namespace PatientAccess.Application.Queries.GetAvailability;

/// <summary>
/// MediatR query — returns available appointment slots for the given date range.
/// Dates are <see cref="DateOnly"/> structs; ASP.NET Core model binding handles
/// ISO-8601 date string → struct conversion with no raw string parsing (OWASP A03).
/// </summary>
/// <param name="StartDate">Inclusive start of the search window.</param>
/// <param name="EndDate">Inclusive end of the search window.</param>
public sealed record GetAvailabilityQuery(DateOnly StartDate, DateOnly EndDate)
    : IRequest<IReadOnlyList<AvailabilitySlotDto>>;
