namespace PatientAccess.Domain.Enums;

/// <summary>
/// PatientView360 clinical review lifecycle — DR-006.
/// Pending is set on initial assembly by the AI pipeline;
/// NeedsReview is set when ConflictDetectionJob finds conflicts (US_022, AIR-004);
/// Verified/Rejected are set by the clinical reviewer (FR-012).
/// </summary>
public enum VerificationStatus
{
    Pending,
    InReview,
    Verified,
    Rejected,
    NeedsReview
}
