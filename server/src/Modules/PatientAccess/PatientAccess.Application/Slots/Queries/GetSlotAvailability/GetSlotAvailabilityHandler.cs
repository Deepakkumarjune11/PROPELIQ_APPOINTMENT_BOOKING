using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Slots.Dtos;

namespace PatientAccess.Application.Slots.Queries.GetSlotAvailability;

/// <summary>
/// Returns all appointment slots in the requested calendar month with availability flags.
/// IsAvailable = false → booked / arrived (watchlist eligible for SCR-009).
/// IsAvailable = true  → available for direct booking (disabled in SCR-009 calendar).
/// </summary>
public sealed class GetSlotAvailabilityHandler
    : IRequestHandler<GetSlotAvailabilityQuery, IReadOnlyList<SlotAvailabilityDto>>
{
    private readonly IWatchlistRepository _repo;
    private readonly ILogger<GetSlotAvailabilityHandler> _logger;

    public GetSlotAvailabilityHandler(
        IWatchlistRepository repo,
        ILogger<GetSlotAvailabilityHandler> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SlotAvailabilityDto>> Handle(
        GetSlotAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Month is < 1 or > 12)
        {
            _logger.LogWarning(
                "GetSlotAvailabilityQuery received invalid month {Month}. Returning empty.",
                request.Month);
            return Array.Empty<SlotAvailabilityDto>();
        }

        var slots = await _repo.GetSlotsForMonthAsync(request.Year, request.Month, cancellationToken);

        _logger.LogDebug(
            "GetSlotAvailability: year={Year} month={Month} count={Count}",
            request.Year, request.Month, slots.Count);

        return slots
            .Select(s => new SlotAvailabilityDto(s.SlotDatetime.ToString("o"), s.IsAvailable))
            .ToList()
            .AsReadOnly();
    }
}
