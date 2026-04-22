namespace ClinicalIntelligence.Application.AI.Availability;

/// <summary>
/// In-memory implementation of <see cref="IAiAvailabilityState"/> using a
/// <c>volatile bool</c> flag (US_030 / AC-5).
///
/// Registered as singleton — one instance per process, shared across all
/// gateway calls. Thread-safety is provided by the <c>volatile</c> keyword
/// which guarantees read/write atomicity for boolean fields.
///
/// <b>REPLACED BY US_031</b>: US_031 replaces this class with a Polly
/// circuit-breaker implementation that adds automatic half-open probing,
/// configurable failure thresholds, and metrics emission. The
/// <see cref="IAiAvailabilityState"/> interface contract is unchanged.
/// </summary>
public sealed class InMemoryAvailabilityState : IAiAvailabilityState
{
    // volatile: ensures write visibility across CPU cores without a full lock.
    // Safe for single-bit read/write; InMemoryAvailabilityState is a singleton.
    private volatile bool _isAvailable = true;

    /// <inheritdoc />
    public bool IsAvailable => _isAvailable;

    /// <inheritdoc />
    public void MarkDegraded(string reason)
    {
        _isAvailable = false;
        // US_031 will add: Polly circuit-breaker state transition, metrics counter, alert threshold here.
    }

    /// <inheritdoc />
    public void MarkRecovered()
    {
        _isAvailable = true;
        // US_031 will add: circuit-breaker half-open → closed transition here.
    }
}
