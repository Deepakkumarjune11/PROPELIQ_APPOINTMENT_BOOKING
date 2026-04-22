using ClinicalIntelligence.Application.Documents.Models;

namespace ClinicalIntelligence.Application.Infrastructure;

/// <summary>
/// Persists <see cref="DocumentChunk"/> records as staging rows in the database
/// (with a <c>null</c> embedding vector) ready for the embedding pipeline (US_019/task_002).
///
/// The real implementation (<c>DocumentChunkEmbeddingRepository</c>) is registered by
/// <c>us_019/task_003_db_vector_embedding_schema</c> once the <c>DocumentChunkEmbedding</c>
/// entity and migration exist. Until then a <c>NullChunkStagingService</c> is registered
/// as a stub so the DI container and build remain healthy.
/// </summary>
public interface IChunkStagingService
{
    /// <summary>
    /// Consumes the async enumerable and persists each chunk as a staging row.
    /// </summary>
    /// <param name="chunks">Streamed chunks from <see cref="DocumentChunker"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task StageChunksAsync(IAsyncEnumerable<DocumentChunk> chunks, CancellationToken ct = default);
}
