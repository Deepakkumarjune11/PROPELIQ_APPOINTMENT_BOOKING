using ClinicalIntelligence.Application.Documents.Dtos;

namespace ClinicalIntelligence.Application.Documents.Services;

/// <summary>
/// RAG retrieval interface — similarity search over the patient's staged document chunks
/// using pgvector cosine distance (TR-015, AIR-R02, AIR-S02).
///
/// Used by the clinical intelligence retrieval pipeline (US_019/task_002, US_020).
/// </summary>
public interface IDocumentSearchService
{
    /// <summary>
    /// Embeds <paramref name="queryText"/> and returns the top-5 most similar document
    /// chunks owned by <paramref name="patientId"/> with cosine similarity ≥ 0.7.
    ///
    /// Ownership filter enforced via <c>document_id IN (patient's document IDs)</c>
    /// before the vector query — prevents cross-patient information leakage (AIR-S02).
    /// </summary>
    /// <param name="queryText">Natural language query from the RAG retrieval pipeline.</param>
    /// <param name="patientId">Restricts results to this patient's documents (AIR-S02).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Top-5 chunks ordered by descending similarity, filtered to similarity ≥ 0.7 (AIR-R02).
    /// Returns an empty list when the AI gateway circuit is open or no matching chunks exist.
    /// </returns>
    Task<IReadOnlyList<ChunkSearchResultDto>> SearchAsync(
        string            queryText,
        Guid              patientId,
        CancellationToken ct = default);

    /// <summary>
    /// Embeds <paramref name="queryText"/> and returns the top-5 most similar chunks
    /// belonging to the specific <paramref name="documentId"/> (AIR-S02 document-scoped).
    ///
    /// Used by <c>FactExtractionJob</c> where ownership is pre-verified by the job
    /// trigger — retrieval is scoped directly to the single document being extracted.
    /// </summary>
    /// <param name="queryText">The extraction query text to embed.</param>
    /// <param name="documentId">Restricts results to this exact document's chunks.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ChunkSearchResultDto>> SearchByDocumentAsync(
        string            queryText,
        Guid              documentId,
        CancellationToken ct = default);

    /// <summary>
    /// Role-aware RAG retrieval entry point (US_029/task_001, AIR-S02, OWASP A01).
    ///
    /// Derives the permitted document set for <paramref name="actorId"/> /
    /// <paramref name="actorRole"/> via <c>IRagAccessFilter</c> before executing the
    /// similarity query.  Returns an empty list when the filter denies access.
    /// </summary>
    /// <param name="queryText">Natural language query from the RAG pipeline.</param>
    /// <param name="actorId">Authenticated principal identifier (Patient or Staff Guid).</param>
    /// <param name="actorRole">JWT role claim, e.g. <c>"Patient"</c>, <c>"Staff"</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ChunkSearchResultDto>> SearchAsync(
        string            queryText,
        Guid              actorId,
        string            actorRole,
        CancellationToken ct = default);
}
