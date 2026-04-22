using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PatientAccess.Presentation.Hubs;

/// <summary>
/// ASP.NET Core SignalR Hub for real-time same-day queue broadcasts (US_017, AC-4, AC-5).
/// Maps to <c>/hubs/queue</c>.
///
/// Authentication: JWT Bearer — only <c>Staff</c> role may connect (OWASP A01).
/// On connect: connection is added to the <c>QueueStaff</c> group so that broadcast
/// calls reach all connected staff simultaneously without enumerating individual connections.
/// Hub only broadcasts server→client (<c>QueueUpdated</c>); no client→server methods.
/// </summary>
[Authorize(Roles = "Staff")]
public sealed class QueueHub : Hub
{
    /// <summary>Group name used for all broadcasting to connected staff.</summary>
    public const string GroupName = "QueueStaff";

    /// <summary>Event name sent to clients when the queue state changes.</summary>
    public const string QueueUpdatedEvent = "QueueUpdated";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName);
        await base.OnDisconnectedAsync(exception);
    }
}
