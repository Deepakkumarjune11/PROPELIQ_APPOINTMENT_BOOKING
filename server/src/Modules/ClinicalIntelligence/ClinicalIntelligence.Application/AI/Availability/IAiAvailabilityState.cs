namespace ClinicalIntelligence.Application.AI.Availability;

/// <summary>
/// Tracks AI service availability state for the gateway degradation pipeline (US_030 / AC-5).
///
/// Consumed by <see cref="AzureOpenAiGateway"/> to determine whether to attempt live
/// Azure OpenAI calls or route directly to <see cref="IAiDegradationHandler"/>.
///
/// <b>US_031 seam</b>: <see cref="InMemoryAvailabilityState"/> is the placeholder
/// implementation. US_031 replaces it with a Polly circuit-breaker–driven implementation
/// without changing this interface.
/// </summary>
public interface IAiAvailabilityState
{
    /// <summary>
    /// Returns <c>true</c> when the AI service is considered available for live calls.
    /// Safe to call from any thread.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Marks the AI service as degraded. Called by <see cref="AzureOpenAiHealthCheck"/>
    /// when the probe fails or times out, and by the gateway on unrecoverable errors.
    /// </summary>
    /// <param name="reason">Human-readable reason for degradation (logged, not surfaced to users).</param>
    void MarkDegraded(string reason);

    /// <summary>
    /// Marks the AI service as recovered. Called by <see cref="AzureOpenAiHealthCheck"/>
    /// when a subsequent probe succeeds.
    /// </summary>
    void MarkRecovered();
}
