namespace ClinicalIntelligence.Application.Documents.Dtos;

/// <summary>
/// A staged chunk row retrieved from the <c>document_chunk_embeddings</c> table
/// that does not yet have an embedding vector.
/// Used by <c>EmbeddingGenerationJob</c> to batch-generate vectors (US_019/task_002).
/// </summary>
/// <param name="Id">Primary key of the <c>DocumentChunkEmbedding</c> row.</param>
/// <param name="ChunkIndex">Zero-based sequential position within the document.</param>
/// <param name="ChunkText">Decoded chunk text sent to the embeddings API.</param>
public record EmbeddingChunkDto(Guid Id, int ChunkIndex, string ChunkText);

/// <summary>
/// Carries a computed embedding vector back to <c>IEmbeddingChunkRepository.UpdateEmbeddingsAsync</c>
/// for bulk-persistence into the pgvector column.
/// </summary>
/// <param name="ChunkId">FK matching <see cref="EmbeddingChunkDto.Id"/>.</param>
/// <param name="Vector">1536-dimensional <c>float[]</c> from Azure OpenAI <c>text-embedding-3-small</c>.</param>
public record EmbeddingChunkUpdateDto(Guid ChunkId, float[] Vector);

/// <summary>
/// A single result returned by
/// <c>IEmbeddingChunkRepository.SearchSimilarAsync</c> (AIR-R02, AIR-S02).
/// </summary>
/// <param name="DocumentId">Source document GUID.</param>
/// <param name="ChunkIndex">Position of this chunk within the source document.</param>
/// <param name="ChunkText">The text content of this chunk, used as RAG context.</param>
/// <param name="TokenCount">Number of GPT-4 <c>cl100k_base</c> tokens in <see cref="ChunkText"/>.</param>
/// <param name="Similarity">Cosine similarity score in <c>[0, 1]</c>; higher = more relevant.</param>
public record ChunkSearchResultDto(
    Guid   DocumentId,
    int    ChunkIndex,
    string ChunkText,
    int    TokenCount,
    float  Similarity);
