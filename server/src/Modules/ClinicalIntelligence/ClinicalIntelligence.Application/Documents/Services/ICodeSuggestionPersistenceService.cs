using ClinicalIntelligence.Application.Documents.Models;

namespace ClinicalIntelligence.Application.Documents.Services;

/// <summary>
/// Provides data operations for the AI code suggestion pipeline (US_023/task_002).
///
/// Implemented in the Data layer (<c>CodeSuggestionPersistenceService</c>) to co-locate
/// EF Core / Data Protection dependencies; consumed here in the Application layer so
/// <c>CodeSuggestionJob</c> remains testable and Data-layer–agnostic.
/// </summary>
public interface ICodeSuggestionPersistenceService
{
    /// <summary>
    /// Loads all non-deleted <c>ExtractedFact</c> rows for <paramref name="patientId"/>
    /// (across all active patient documents), decrypts PHI ciphertext, and returns
    /// plain-text fact DTOs suitable for context assembly.
    /// </summary>
    Task<IReadOnlyList<FactForAssemblyDto>> GetPlainTextFactsAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes all existing <c>CodeSuggestion</c> rows for <paramref name="patientId"/>
    /// and inserts new rows from <paramref name="results"/> to support idempotent re-generation
    /// (DR-017 soft-delete).
    /// </summary>
    Task PersistAsync(Guid patientId, IReadOnlyList<CodeSuggestionResult> results, CancellationToken ct = default);
}
