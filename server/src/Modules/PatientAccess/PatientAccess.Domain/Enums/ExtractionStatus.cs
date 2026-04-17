namespace PatientAccess.Domain.Enums;

/// <summary>
/// Document AI-extraction pipeline state — DR-004.
/// Failed indicates extraction fell below the AIR-007 70% confidence threshold
/// and requires manual staff review.
/// </summary>
public enum ExtractionStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
