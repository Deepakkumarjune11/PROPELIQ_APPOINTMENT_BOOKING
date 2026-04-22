namespace PatientAccess.Domain.Enums;

/// <summary>
/// Document AI-extraction pipeline state — DR-004.
/// <para>Queued = newly uploaded, awaiting extraction worker.</para>
/// <para>ManualReview = extraction confidence below AIR-007 70% threshold; requires staff review.</para>
/// <para>Failed = extraction job threw unrecoverable error.</para>
/// </summary>
public enum ExtractionStatus
{
    Pending,
    Queued,
    Processing,
    Completed,
    ManualReview,
    Failed
}
