namespace PatientAccess.Domain.Enums;

/// <summary>
/// Communication channel used to contact the patient (FR-007).
/// </summary>
public enum CommunicationChannel
{
    /// <summary>SMS via Twilio Programmable SMS.</summary>
    SMS,

    /// <summary>Email with PDF attachment via SendGrid.</summary>
    Email,
}
