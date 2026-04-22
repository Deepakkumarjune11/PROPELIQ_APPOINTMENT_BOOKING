namespace ClinicalIntelligence.Application.Documents.Services;

/// <summary>
/// Detects clinically meaningful conflicts between fact values in a patient's 360-view
/// and persists encrypted conflict flags (US_022, AIR-004).
///
/// Implemented in the Data layer (<c>ClinicalIntelligence.Data</c>) so that EF Core
/// and <c>IDataProtectionProvider</c> dependencies are kept out of the Application layer.
/// </summary>
public interface IConflictDetectionService
{
    /// <summary>
    /// Runs conflict detection for the patient associated with <paramref name="documentId"/>.
    ///
    /// Steps:
    /// <list type="number">
    ///   <item>Resolve patient from document (AIR-S02 ownership guard).</item>
    ///   <item>Load and decrypt <c>PatientView360.ConsolidatedFacts</c>.</item>
    ///   <item>Group facts by FactType; compare pairwise via embedding cosine distance (AIR-004).</item>
    ///   <item>Circuit-open fallback: string-inequality comparison (AIR-O02).</item>
    ///   <item>Encrypt detected <see cref="Models.ConflictFlag"/> entries individually (DR-015).</item>
    ///   <item>Persist encrypted array and update <c>VerificationStatus</c>.</item>
    ///   <item>Write <c>ConflictDetectionCompleted</c> AuditLog without PHI values (AIR-S03).</item>
    /// </list>
    /// </summary>
    /// <param name="documentId">Source document that triggered the 360-view update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved patient ID for the triggering document.</returns>
    Task<Guid> DetectAndSaveAsync(Guid documentId, CancellationToken ct = default);
}
