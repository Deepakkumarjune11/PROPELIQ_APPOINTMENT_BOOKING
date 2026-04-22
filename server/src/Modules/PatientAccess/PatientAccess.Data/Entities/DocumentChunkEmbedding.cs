using Pgvector;

namespace PatientAccess.Data.Entities;

/// <summary>
/// Represents a single text chunk from a <see cref="ClinicalDocument"/> with its
/// 1536-dimensional embedding vector for pgvector cosine similarity search (DR-016, TR-015).
///
/// Staging lifecycle:
/// - <c>DocumentExtractionJob</c> (US_019/task_001) inserts rows with <c>Embedding = null</c>.
/// - <c>EmbeddingGenerationJob</c> (US_019/task_002) fills <c>Embedding</c> via <see cref="SetEmbedding"/>.
///
/// The composite unique constraint on <c>(DocumentId, ChunkIndex)</c> prevents duplicate
/// chunks when a document is re-processed (e.g. after a failed extraction retry).
/// </summary>
public class DocumentChunkEmbedding
{
    public Guid Id          { get; private set; }
    public Guid DocumentId  { get; private set; }
    public int  ChunkIndex  { get; private set; }
    public string ChunkText { get; private set; } = string.Empty;
    public int  TokenCount  { get; private set; }

    /// <summary>
    /// 1536-dimensional float vector for <c>text-embedding-3-small</c> (DR-016).
    /// Null while the row is staged but not yet processed by the embedding job.
    /// </summary>
    public Vector? Embedding { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation
    public ClinicalDocument Document { get; private set; } = null!;

    // EF Core parameterless constructor
    private DocumentChunkEmbedding() { }

    public DocumentChunkEmbedding(
        Guid    documentId,
        int     chunkIndex,
        string  chunkText,
        int     tokenCount,
        Vector? embedding = null)
    {
        Id         = Guid.NewGuid();
        DocumentId = documentId;
        ChunkIndex = chunkIndex;
        ChunkText  = chunkText;
        TokenCount = tokenCount;
        Embedding  = embedding;
        CreatedAt  = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Persists the computed embedding vector produced by <c>EmbeddingGenerationJob</c>.
    /// </summary>
    public void SetEmbedding(Vector vector) => Embedding = vector;
}
