namespace ClinicalIntelligence.Application.AI;

/// <summary>
/// Circuit breaker thresholds for the Polly v8 resilience pipeline (US_031, AIR-O02).
/// Bound from <c>appsettings.json → "AzureOpenAiGateway:CircuitBreaker"</c>.
///
/// Pipeline design: [CircuitBreaker (outer)] → [Retry 3× (inner)] → [GPT call]
/// The circuit breaker counts one failure per request after all retries are exhausted — correct
/// semantics for AC-1 "5 consecutive failures".
/// </summary>
public sealed class AzureOpenAiCircuitBreakerOptions
{
    /// <summary>
    /// Minimum number of consecutive failures within the sampling window to open the circuit (AC-1: 5).
    /// Maps to Polly <c>MinimumThroughput</c> with <c>FailureRatio = 1.0</c>.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Sliding window in seconds during which failures are counted (AC-1: 60 s).
    /// Maps to Polly <c>SamplingDuration</c>.
    /// </summary>
    public int SamplingWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Duration in seconds the circuit stays open before entering half-open state (AC-1: 30 s).
    /// After this duration Polly allows one test request through (AC-2 half-open probe).
    /// </summary>
    public int BreakDurationSeconds { get; set; } = 30;
}
