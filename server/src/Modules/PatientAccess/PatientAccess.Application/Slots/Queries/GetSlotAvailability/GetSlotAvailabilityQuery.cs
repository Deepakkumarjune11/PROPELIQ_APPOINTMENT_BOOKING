using MediatR;
using PatientAccess.Application.Slots.Dtos;

namespace PatientAccess.Application.Slots.Queries.GetSlotAvailability;

/// <summary>
/// Returns all appointment slots for a given calendar month with availability flags.
/// Used by SCR-009 to distinguish watchlist-eligible (booked) slots from open (directly bookable) slots.
/// </summary>
/// <param name="ProviderId">
///   Provider identifier from the frontend request.
///   Accepted for forward-compatibility; provider filtering requires a future schema change.
/// </param>
/// <param name="Year">Calendar year (e.g., 2026).</param>
/// <param name="Month">Calendar month, 1-indexed (1=January … 12=December).</param>
public sealed record GetSlotAvailabilityQuery(Guid? ProviderId, int Year, int Month)
    : IRequest<IReadOnlyList<SlotAvailabilityDto>>;
