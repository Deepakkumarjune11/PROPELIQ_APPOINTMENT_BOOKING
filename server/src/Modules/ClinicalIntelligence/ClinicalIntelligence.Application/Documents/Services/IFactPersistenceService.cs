using ClinicalIntelligence.Application.AI.Models;

namespace ClinicalIntelligence.Application.Documents.Services;

/// <summary>
/// Persistence contract for AI-extracted clinical facts (US_020/task_002).
///
/// Called by <see cref="Jobs.FactExtractionJob"/> after successful GPT-4 extraction
/// and schema validation. Responsible for:
/// - Writing <c>ExtractedFact</c> rows with PHI encryption.
/// - Transitioning <c>ClinicalDocument.ExtractionStatus</c> to <c>Completed</c>.
/// - Enqueuing <c>PatientView360UpdateJob</c> to refresh the unified patient view.
///
/// Implemented by <c>FactPersistenceService</c> in <c>ClinicalIntelligence.Data</c>
/// (created in us_020/task_002). Until then a <c>NullFactPersistenceService</c> stub
/// is registered so the DI container and build remain healthy.
/// </summary>
public interface IFactPersistenceService
{
    /// <summary>
    /// Persists the extracted facts for the specified document atomically and
    /// triggers downstream view-refresh.
    /// </summary>
    /// <param name="documentId">The source <c>ClinicalDocument</c> GUID.</param>
    /// <param name="facts">Validated facts from GPT-4 (PHI — encrypted at write time).</param>
    /// <param name="ct">Cancellation token.</param>
    Task PersistAsync(
        Guid                         documentId,
        IReadOnlyList<ExtractedFactResult> facts,
        CancellationToken            ct = default);
}
