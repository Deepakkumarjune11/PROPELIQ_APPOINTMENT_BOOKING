namespace ClinicalIntelligence.Application.Documents.Models;

/// <summary>
/// Represents a single text window produced by <c>DocumentChunker</c>.
/// Shared between the extraction job (US_019/task_001) and the embedding pipeline
/// (US_019/task_002). Plain record — no EF Core dependency.
///
/// Chunking spec (AIR-R01): window = 512 tokens, step = 384 tokens (25% overlap).
/// </summary>
/// <param name="DocumentId">FK to the source <c>ClinicalDocument</c>.</param>
/// <param name="ChunkIndex">Zero-based sequential position within the document.</param>
/// <param name="ChunkText">Decoded text for this window.</param>
/// <param name="TokenCount">Number of GPT-4 <c>cl100k_base</c> tokens in <paramref name="ChunkText"/>.</param>
public record DocumentChunk(
    Guid   DocumentId,
    int    ChunkIndex,
    string ChunkText,
    int    TokenCount);
