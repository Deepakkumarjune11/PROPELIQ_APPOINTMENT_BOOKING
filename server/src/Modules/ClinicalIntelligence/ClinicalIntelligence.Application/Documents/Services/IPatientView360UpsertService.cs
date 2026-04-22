namespace ClinicalIntelligence.Application.Documents.Services;

/// <summary>
/// Orchestrates the full 360-degree patient-view assembly pipeline (US_021):
/// gather patient facts → de-duplicate → encrypt → upsert <c>PatientView360</c>.
///
/// Implemented in the Data layer (<c>PatientView360UpsertService</c>) to co-locate
/// the EF Core / Data Protection dependencies; consumed as a thin interface here in
/// the Application layer so jobs remain testable and Data-layer–agnostic.
/// </summary>
public interface IPatientView360UpsertService
{
    /// <summary>
    /// Resolves the patient from <paramref name="documentId"/>, gathers all active
    /// facts for that patient, de-duplicates them and upserts the
    /// <c>PatientView360</c> record with optimistic-concurrency retry (DR-018).
    /// </summary>
    /// <param name="documentId">
    /// The document whose completion triggered the 360-view refresh (US_020 AC-4).
    /// Used to resolve the target patient ID.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task UpsertAsync(Guid documentId, CancellationToken ct = default);
}
