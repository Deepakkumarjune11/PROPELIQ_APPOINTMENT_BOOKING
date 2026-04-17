namespace PatientAccess.Domain.Enums;

/// <summary>
/// Clinical fact classification — DR-005.
/// Values match the five categories used by the RAG extraction pipeline (AIR-001).
/// </summary>
public enum FactType
{
    Vitals,
    Medications,
    History,
    Diagnoses,
    Procedures
}
