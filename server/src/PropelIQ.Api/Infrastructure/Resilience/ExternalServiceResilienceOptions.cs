namespace PropelIQ.Api.Infrastructure.Resilience;

/// <summary>
/// Configuration for external service bulkhead concurrency limits, HTTP retry settings,
/// and API-level rate limiting (US_035, AC-4/AC-1/AC-5).
///
/// Bound from <c>appsettings.json → "ExternalServiceResilience"</c>.
/// </summary>
public sealed class ExternalServiceResilienceOptions
{
    public const string SectionName = "ExternalServiceResilience";

    // HTTP retry (TR-023 — exponential backoff for external API calls)
    public int    RetryCount            { get; set; } = 3;
    public double RetryBaseDelaySeconds { get; set; } = 1.0;  // 1s, 2s, 4s

    // Bulkhead concurrency limits per external dependency (AC-4)
    public int AzureOpenAiMaxConcurrent { get; set; } = 20;
    public int EmailMaxConcurrent       { get; set; } = 10;
    public int SmsMaxConcurrent         { get; set; } = 5;
    public int PagerDutyMaxConcurrent   { get; set; } = 3;
    public int BulkheadTimeoutSeconds   { get; set; } = 5;

    // Rate limiting (AC-1 — p95 < 500ms at 200 concurrent users)
    public int GlobalWindowRequestLimit   { get; set; } = 100; // per user per window
    public int GlobalWindowSeconds        { get; set; } = 10;
    public int AuthBucketTokenLimit       { get; set; } = 5;
    public int AuthBucketReplenishSeconds { get; set; } = 60;
}
