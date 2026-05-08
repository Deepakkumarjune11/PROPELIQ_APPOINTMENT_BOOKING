using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Staff.Dtos;

namespace PatientAccess.Application.Appointments.Commands.BookWalkIn;

/// <summary>
/// Handles <see cref="BookWalkInCommand"/> — delegates the atomic SERIALIZABLE transaction
/// to <see cref="IWalkInBookingRepository"/> which enforces queue-position integrity.
/// Redis cache is invalidated and a SignalR broadcast is sent after a successful commit (AC-4).
/// </summary>
public sealed class BookWalkInHandler
    : IRequestHandler<BookWalkInCommand, WalkInBookingResultDto>
{
    private const string QueueCacheKey = "staff:queue:today";

    private readonly IWalkInBookingRepository    _repo;
    private readonly ICacheService               _cache;
    private readonly IQueueBroadcastService      _broadcast;
    private readonly ILogger<BookWalkInHandler>  _logger;

    public BookWalkInHandler(
        IWalkInBookingRepository   repo,
        ICacheService              cache,
        IQueueBroadcastService     broadcast,
        ILogger<BookWalkInHandler> logger)
    {
        _repo      = repo;
        _cache     = cache;
        _broadcast = broadcast;
        _logger    = logger;
    }

    public async Task<WalkInBookingResultDto> Handle(
        BookWalkInCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _repo.BookWalkInAsync(command, cancellationToken);

        _logger.LogInformation(
            "Walk-in booked: patient={PatientId} queuePos={Pos} waitQueue={Wait}",
            command.PatientId, result.QueuePosition, result.WaitQueue);

        // Invalidate Redis queue cache after commit so next GET queue read reflects the new entry.
        await _cache.RemoveAsync(QueueCacheKey, cancellationToken);

        // Broadcast to all connected staff so their queue and dashboard caches are invalidated.
        await _broadcast.BroadcastQueueUpdatedAsync(cancellationToken);

        return result;
    }
}
