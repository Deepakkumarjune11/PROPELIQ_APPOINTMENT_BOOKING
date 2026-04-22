using Microsoft.AspNetCore.SignalR;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Presentation.Hubs;

namespace PatientAccess.Presentation.Services;

/// <summary>
/// SignalR implementation of <see cref="IQueueBroadcastService"/>.
/// Sends <c>QueueUpdated</c> to all clients in the <c>QueueStaff</c> group (US_017, AC-4).
/// Lives in the Presentation layer so the Application layer stays free of SignalR dependencies.
/// </summary>
public sealed class SignalRQueueBroadcastService : IQueueBroadcastService
{
    private readonly IHubContext<QueueHub> _hubContext;

    public SignalRQueueBroadcastService(IHubContext<QueueHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task BroadcastQueueUpdatedAsync(CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients
            .Group(QueueHub.GroupName)
            .SendAsync(QueueHub.QueueUpdatedEvent, cancellationToken: cancellationToken);
    }
}
