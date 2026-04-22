namespace PatientAccess.Domain.Enums;

/// <summary>
/// Action category recorded in AuditLog — DR-008, NFR-007.
/// Values match HIPAA audit categories: patient data access, appointment changes,
/// clinical data modifications, and code confirmations.
/// </summary>
public enum AuditActionType
{
    PatientDataAccess,
    AppointmentChange,
    AppointmentBooked,
    ClinicalDataModification,
    CodeConfirmation,
    DocumentUpload,
    IntakeSubmission,
    UserLogin,
    UserLogout,
    AdminAction
}
    