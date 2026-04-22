namespace PatientAccess.Application.Jobs;

/// <summary>
/// Discriminated outcome of a single watchlist slot-swap attempt (US_015, AC-3/AC-5).
/// Used by <c>SlotSwapJob</c> to decide whether to enqueue a notification.
/// </summary>
public enum SlotSwapResult
{
    /// <summary>Preferred slot was free; appointment datetime updated; watchlist cleared.</summary>
    Swapped,

    /// <summary>Preferred slot is still occupied by another booking; watchlist entry preserved (AC-5).</summary>
    SlotStillTaken,

    /// <summary>Preferred slot datetime is in the past; watchlist entry cleared; expiry notification enqueued.</summary>
    SlotExpired,

    /// <summary>
    /// Appointment no longer has a preferred slot ID when the transaction executes —
    /// idempotent guard against double-execution.
    /// </summary>
    NotOnWatchlist,
}
