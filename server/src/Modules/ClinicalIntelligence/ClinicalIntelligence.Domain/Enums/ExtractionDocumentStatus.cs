namespace ClinicalIntelligence.Domain.Enums;

/// <summary>
/// AI-extraction pipeline state for a clinical document — US_018 / DR-004.
/// Stored as <c>string</c> via EF Core <c>HasConversion&lt;string&gt;()</c> so that
/// future values can be added without an ALTER TYPE migration (NFR-012).
/// </summary>
public enum ExtractionDocumentStatus
{
    /// <summary>Document uploaded; extraction job is queued but not yet started.</summary>
    Queued,

    /// <summary>Hangfire extraction job is actively running.</summary>
    Processing,

    /// <summary>AI extraction succeeded (confidence ≥ 70% per AIR-007).</summary>
    Completed,

    /// <summary>AI extraction confidence &lt; 70%; requires staff review (AIR-007).</summary>
    ManualReview,

    /// <summary>Extraction job failed after all Hangfire retries.</summary>
    Failed,
}
