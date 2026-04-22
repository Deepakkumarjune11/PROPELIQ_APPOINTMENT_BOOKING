namespace PatientAccess.Application.Infrastructure;

/// <summary>
/// Abstraction for broadcasting real-time queue events to connected staff clients (US_017).
/// Lives in the Application layer so command handlers depend only on the interface —
/// the SignalR implementation is wired in the Presentation layer (IHubContext).
/// </summary>
public interface IQueueBroadcastService
{
    /// <summary>
    /// Broadcasts a <c>QueueUpdated</c> event to all staff connected to the queue hub.
    /// Called after any mutation that changes the visible queue state.
    /// </summary>
    Task BroadcastQueueUpdatedAsync(CancellationToken cancellationToken = default);
}
