using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Application.Repositories;

namespace PatientAccess.Application.Staff.Commands.ReorderQueue;

/// <summary>
/// Persists drag-and-drop reorder of the same-day queue (US_017, AC-2).
/// Bulk-updates <c>queue_position</c> values and broadcasts <c>QueueUpdated</c> to all staff.
/// </summary>
/// <param name="OrderedAppointmentIds">Appointment GUIDs in the desired new queue order (position 1 = first element).</param>
public sealed record ReorderQueueCommand(IReadOnlyList<Guid> OrderedAppointmentIds) : IRequest;

/// <summary>
/// Handles <see cref="ReorderQueueCommand"/>.
/// Uses EF Core <c>ExecuteUpdateAsync</c> for a bulk position update (no entity load).
/// Redis cache is invalidated and SignalR broadcast is sent post-commit (AC-2).
/// </summary>
public sealed class ReorderQueueHandler : IRequestHandler<ReorderQueueCommand>
{
    private const string CacheKey = "staff:queue:today";

    private readonly IQueueRepository                _repo;
    private readonly ICacheService                   _cache;
    private readonly IQueueBroadcastService          _broadcast;
    private readonly ILogger<ReorderQueueHandler>    _logger;

    public ReorderQueueHandler(
        IQueueRepository             repo,
        ICacheService                cache,
        IQueueBroadcastService       broadcast,
        ILogger<ReorderQueueHandler> logger)
    {
        _repo      = repo;
        _cache     = cache;
        _broadcast = broadcast;
        _logger    = logger;
    }

    public async Task Handle(ReorderQueueCommand command, CancellationToken cancellationToken)
    {
        if (command.OrderedAppointmentIds.Count == 0)
            return;

        await _repo.BulkUpdateQueuePositionsAsync(command.OrderedAppointmentIds, cancellationToken);

        // Invalidate Redis cache so next GET queue read reflects the updated order.
        await _cache.RemoveAsync(CacheKey, cancellationToken);

        // Broadcast to all connected staff — each client invalidates its local React Query cache.
        await _broadcast.BroadcastQueueUpdatedAsync(cancellationToken);

        _logger.LogInformation(
            "Queue reordered: {Count} appointments updated.",
            command.OrderedAppointmentIds.Count);
    }
}
