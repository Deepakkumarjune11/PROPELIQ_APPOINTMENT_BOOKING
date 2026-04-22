namespace PatientAccess.Domain.Enums;

/// <summary>
/// Actor type performing the audited action — DR-008, NFR-007.
/// System covers automated pipeline actions (AI extraction, scheduled jobs)
/// where there is no human actor ID.
/// </summary>
public enum AuditActorType
{
    Patient,
    Staff,
    Admin,
    System
}
