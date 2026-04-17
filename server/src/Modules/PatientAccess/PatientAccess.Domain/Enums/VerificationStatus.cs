namespace PatientAccess.Domain.Enums;

/// <summary>
/// PatientView360 clinical review lifecycle — DR-006.
/// Pending is set on initial assembly by the AI pipeline;
/// Verified/Rejected are set by the clinical reviewer (FR-012).
/// </summary>
public enum VerificationStatus
{
    Pending,
    InReview,
    Verified,
    Rejected
}
