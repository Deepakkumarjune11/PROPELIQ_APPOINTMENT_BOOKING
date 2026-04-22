namespace PatientAccess.Application.Slots.Dtos;

/// <summary>
/// Slot availability entry for a given datetime.
/// Used by SCR-009 to distinguish watchlist-eligible (booked) slots from directly-bookable ones.
/// </summary>
/// <param name="Datetime">ISO-8601 UTC datetime of the slot, e.g. "2026-04-20T09:00:00Z".</param>
/// <param name="IsAvailable">
///   <see langword="true"/> when the slot is open for direct booking (disabled in SCR-009 calendar).<br/>
///   <see langword="false"/> when the slot is already booked and eligible for watchlist enrollment.
/// </param>
public sealed record SlotAvailabilityDto(string Datetime, bool IsAvailable);
