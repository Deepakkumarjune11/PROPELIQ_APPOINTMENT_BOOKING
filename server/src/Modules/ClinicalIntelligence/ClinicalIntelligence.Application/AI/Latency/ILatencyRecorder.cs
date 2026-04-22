namespace ClinicalIntelligence.Application.AI.Latency;

/// <summary>
/// Records end-to-end AI gateway latency samples and computes the p95 percentile
/// for SLA breach detection (US_032, AC-1, AIR-Q02).
///
/// Samples are stored per featureContext in a Redis sliding window of size
/// <see cref="AiSlaOptions.SampleWindowSize"/> (default 200 entries).
/// </summary>
public interface ILatencyRecorder
{
    /// <summary>
    /// Appends a latency sample for the given <paramref name="featureContext"/> and trims
    /// the Redis list to the configured <see cref="AiSlaOptions.SampleWindowSize"/>.
    /// </summary>
    /// <param name="featureContext">Feature context key (e.g. <c>"FactExtraction"</c>).</param>
    /// <param name="latencyMs">Wall-clock elapsed time in milliseconds for the gateway call.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordAsync(string featureContext, long latencyMs, CancellationToken ct = default);

    /// <summary>
    /// Computes the p95 latency (in milliseconds) over the retained sample window for
    /// <paramref name="featureContext"/>. Returns 0 when no samples are recorded.
    /// </summary>
    /// <param name="featureContext">Feature context key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<double> GetP95Async(string featureContext, CancellationToken ct = default);
}
