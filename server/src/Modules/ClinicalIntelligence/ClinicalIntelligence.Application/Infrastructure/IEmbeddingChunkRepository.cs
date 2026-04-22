using ClinicalIntelligence.Application.Documents.Dtos;

namespace ClinicalIntelligence.Application.Infrastructure;

/// <summary>
/// Data-layer contract for <c>DocumentChunkEmbedding</c> persistence and similarity search.
///
/// Decouples <see cref="Documents.Jobs.EmbeddingGenerationJob"/> and
/// <see cref="Documents.Services.DocumentSearchService"/> from the concrete EF Core entity
/// until <c>us_019/task_003_db_vector_embedding_schema</c> creates the
/// <c>DocumentChunkEmbedding</c> entity and migration.
///
/// Until task_003 is implemented, <c>NullEmbeddingChunkRepository</c> is registered as a
/// no-op stub so the build and DI container remain healthy.
/// </summary>
public interface IEmbeddingChunkRepository
{
    /// <summary>
    /// Returns all staged chunk rows for <paramref name="documentId"/> where the embedding
    /// vector is still <c>null</c> (i.e. not yet processed by <c>EmbeddingGenerationJob</c>).
    /// </summary>
    Task<IReadOnlyList<EmbeddingChunkDto>> GetUnembeddedChunksAsync(
        Guid              documentId,
        CancellationToken ct = default);

    /// <summary>
    /// Bulk-updates the embedding vector for each chunk identified by <see cref="EmbeddingChunkUpdateDto.ChunkId"/>.
    /// Implementations MUST persist all updates atomically (single <c>SaveChangesAsync</c> call).
    /// </summary>
    Task UpdateEmbeddingsAsync(
        IReadOnlyList<EmbeddingChunkUpdateDto> updates,
        CancellationToken                      ct = default);

    /// <summary>
    /// Executes a pgvector cosine similarity query for <paramref name="queryVector"/> restricted
    /// to documents owned by <paramref name="patientId"/> (AIR-S02 ownership guard).
    /// Returns top-<paramref name="limit"/> chunks ordered by ascending cosine distance
    /// (most similar first), filtered to similarity ≥ <paramref name="threshold"/> (AIR-R02).
    /// </summary>
    Task<IReadOnlyList<ChunkSearchResultDto>> SearchSimilarAsync(
        Guid              patientId,
        float[]           queryVector,
        int               limit,
        float             threshold,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a pgvector cosine similarity query scoped to a single document
    /// (used by <c>FactExtractionJob</c> where the document owner is already verified — AIR-S02).
    /// Returns top-<paramref name="limit"/> chunks ordered by ascending cosine distance
    /// (most similar first), filtered to similarity ≥ <paramref name="threshold"/>.
    /// </summary>
    Task<IReadOnlyList<ChunkSearchResultDto>> SearchSimilarByDocumentAsync(
        Guid              documentId,
        float[]           queryVector,
        int               limit,
        float             threshold,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a pgvector cosine similarity query filtered to an explicit set of authorised
    /// document IDs supplied by <see cref="ClinicalIntelligence.Application.AI.Access.IRagAccessFilter"/>
    /// (AIR-S02, OWASP A01).
    ///
    /// When <paramref name="authorizedIds"/> is <c>null</c> the query is unrestricted (Admin /
    /// System actors).  When it is an empty collection the method MUST return an empty list
    /// without executing any vector query — the caller has already determined that access is denied.
    /// </summary>
    /// <param name="authorizedIds">
    /// Permitted document IDs, or <c>null</c> for unrestricted access.
    /// </param>
    Task<IReadOnlyList<ChunkSearchResultDto>> SearchSimilarByAuthorizedIdsAsync(
        IReadOnlyList<Guid>? authorizedIds,
        float[]              queryVector,
        int                  limit,
        float                threshold,
        CancellationToken    ct = default);
}
