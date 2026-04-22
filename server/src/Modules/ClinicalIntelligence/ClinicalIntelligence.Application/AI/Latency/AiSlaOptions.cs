namespace ClinicalIntelligence.Application.AI.Latency;

/// <summary>
/// Configuration POCO for AI SLA monitoring and schema validation error responses
/// (US_032, AIR-Q02, AIR-Q03).
/// Bound from <c>appsettings.json → "AiSla"</c>.
/// </summary>
public sealed class AiSlaOptions
{
    public const string SectionName = "AiSla";

    /// <summary>
    /// P95 latency alert threshold in milliseconds (AIR-Q02: 3 seconds).
    /// When the p95 of the sliding latency window exceeds this value, Serilog logs at
    /// <c>Error</c> level — the task_002 SLA auto-disable hook consumes this signal.
    /// </summary>
    public int P95ThresholdMs { get; set; } = 3000;

    /// <summary>
    /// Number of latency samples retained per featureContext in the Redis sliding window.
    /// Older entries beyond this limit are trimmed via LTRIM to bound Redis memory usage.
    /// </summary>
    public int SampleWindowSize { get; set; } = 200;

    /// <summary>
    /// User-facing message returned after 3 consecutive schema validation failures (AC-3).
    /// Safe for display in clinical staff UI — no technical details.
    /// </summary>
    public string SchemaErrorMessage { get; set; } =
        "The AI response could not be validated. Please try again or contact support.";
}
