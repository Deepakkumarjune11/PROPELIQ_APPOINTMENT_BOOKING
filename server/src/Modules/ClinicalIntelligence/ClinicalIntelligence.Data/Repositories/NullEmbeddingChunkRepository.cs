using ClinicalIntelligence.Application.Documents.Dtos;
using ClinicalIntelligence.Application.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ClinicalIntelligence.Data.Repositories;

/// <summary>
/// No-op stub implementation of <see cref="IEmbeddingChunkRepository"/> registered until
/// <c>us_019/task_003_db_vector_embedding_schema</c> creates the
/// <c>DocumentChunkEmbedding</c> entity, its EF Core configuration, and the pgvector
/// migration.
///
/// Behaviour:
/// - <see cref="GetUnembeddedChunksAsync"/> always returns an empty list (EmbeddingGenerationJob
///   exits early without calling the AI API — no waste).
/// - <see cref="UpdateEmbeddingsAsync"/> is a no-op.
/// - <see cref="SearchSimilarAsync"/> always returns an empty list.
///
/// To replace: implement <c>DocumentChunkEmbeddingRepository : IEmbeddingChunkRepository</c>
/// (with pgvector cosine query via <c>Pgvector.EntityFrameworkCore</c>) and update the DI
/// registration in <c>ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs</c>.
/// </summary>
public sealed class NullEmbeddingChunkRepository : IEmbeddingChunkRepository
{
    private readonly ILogger<NullEmbeddingChunkRepository> _logger;

    public NullEmbeddingChunkRepository(ILogger<NullEmbeddingChunkRepository> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EmbeddingChunkDto>> GetUnembeddedChunksAsync(
        Guid              documentId,
        CancellationToken ct = default)
    {
        _logger.LogWarning(
            "NullEmbeddingChunkRepository: GetUnembeddedChunksAsync called for {DocumentId} — " +
            "DocumentChunkEmbedding table not yet created (us_019/task_003).",
            documentId);

        return Task.FromResult<IReadOnlyList<EmbeddingChunkDto>>(Array.Empty<EmbeddingChunkDto>());
    }

    /// <inheritdoc />
    public Task UpdateEmbeddingsAsync(
        IReadOnlyList<EmbeddingChunkUpdateDto> updates,
        CancellationToken                      ct = default)
    {
        _logger.LogWarning(
            "NullEmbeddingChunkRepository: UpdateEmbeddingsAsync called with {Count} update(s) — " +
            "DocumentChunkEmbedding table not yet created (us_019/task_003). Updates discarded.",
            updates.Count);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ChunkSearchResultDto>> SearchSimilarAsync(
        Guid              patientId,
        float[]           queryVector,
        int               limit,
        float             threshold,
        CancellationToken ct = default)
    {
        _logger.LogWarning(
            "NullEmbeddingChunkRepository: SearchSimilarAsync called for patient {PatientId} — " +
            "DocumentChunkEmbedding table not yet created (us_019/task_003). Returning empty.",
            patientId);

        return Task.FromResult<IReadOnlyList<ChunkSearchResultDto>>(Array.Empty<ChunkSearchResultDto>());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ChunkSearchResultDto>> SearchSimilarByDocumentAsync(
        Guid              documentId,
        float[]           queryVector,
        int               limit,
        float             threshold,
        CancellationToken ct = default)
    {
        _logger.LogWarning(
            "NullEmbeddingChunkRepository: SearchSimilarByDocumentAsync called for document {DocumentId} — stub returning empty.",
            documentId);

        return Task.FromResult<IReadOnlyList<ChunkSearchResultDto>>(Array.Empty<ChunkSearchResultDto>());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ChunkSearchResultDto>> SearchSimilarByAuthorizedIdsAsync(
        IReadOnlyList<Guid>? authorizedIds,
        float[]              queryVector,
        int                  limit,
        float                threshold,
        CancellationToken    ct = default)
    {
        _logger.LogWarning(
            "NullEmbeddingChunkRepository: SearchSimilarByAuthorizedIdsAsync called — " +
            "DocumentChunkEmbedding table not yet created (us_019/task_003). Returning empty.");

        return Task.FromResult<IReadOnlyList<ChunkSearchResultDto>>(Array.Empty<ChunkSearchResultDto>());
    }
}
