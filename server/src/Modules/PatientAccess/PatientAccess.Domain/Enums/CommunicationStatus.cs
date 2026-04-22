namespace PatientAccess.Domain.Enums;

/// <summary>
/// Outcome status for a communication delivery attempt (FR-007 extension 5a).
/// </summary>
public enum CommunicationStatus
{
    /// <summary>Message was accepted/delivered by the external provider.</summary>
    Sent,

    /// <summary>Message delivery failed after all configured retry attempts.</summary>
    Failed,
}
