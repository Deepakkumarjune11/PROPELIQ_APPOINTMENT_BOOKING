namespace ClinicalIntelligence.Application.AI.Availability;

/// <summary>
/// Provides a fallback <see cref="GatewayResponse"/> when the Azure OpenAI service
/// is unavailable (US_030 / AC-5, AIR-S03).
///
/// Implementation strategy (priority order):
/// 1. Redis cache lookup for the last successful response keyed by <c>ai_cache:{featureContext}</c>.
/// 2. Static degradation message from <see cref="AzureOpenAiOptions.DegradationMessage"/>.
///
/// All code paths log with <c>source=cached</c> or <c>source=static</c> so operators can
/// distinguish stale AI results from live results (AIR-S03).
/// </summary>
public interface IAiDegradationHandler
{
    /// <summary>
    /// Returns the best available degraded response for the given feature context.
    ///
    /// Never throws — all internal exceptions are caught and logged. Guaranteed to
    /// return a non-null <see cref="GatewayResponse"/> on every code path.
    /// </summary>
    /// <param name="featureContext">Feature key used to look up the Redis cache entry, e.g. <c>"FactExtraction"</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GatewayResponse> GetDegradedResponseAsync(string featureContext, CancellationToken ct = default);
}
