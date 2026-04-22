using ClinicalIntelligence.Application.Documents.Models;
using ClinicalIntelligence.Application.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ClinicalIntelligence.Data.Services;

/// <summary>
/// Stub implementation of <see cref="IChunkStagingService"/> registered until
/// <c>us_019/task_003_db_vector_embedding_schema</c> creates the
/// <c>DocumentChunkEmbedding</c> entity and its real repository.
///
/// Behaviour: drains the async enumerable, counts chunks, then logs a warning so
/// the extraction job completes without error. No data is persisted.
///
/// To replace: implement <c>DocumentChunkEmbeddingRepository : IChunkStagingService</c>
/// in this project and update the DI registration in
/// <c>ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs</c>.
/// </summary>
public sealed class NullChunkStagingService : IChunkStagingService
{
    private readonly ILogger<NullChunkStagingService> _logger;

    public NullChunkStagingService(ILogger<NullChunkStagingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StageChunksAsync(
        IAsyncEnumerable<DocumentChunk> chunks,
        CancellationToken               ct = default)
    {
        int count = 0;
        await foreach (var _ in chunks.WithCancellation(ct))
            count++;

        _logger.LogWarning(
            "NullChunkStagingService: {ChunkCount} chunk(s) discarded — " +
            "DocumentChunkEmbedding table not yet created (us_019/task_003). " +
            "Replace this stub with DocumentChunkEmbeddingRepository.",
            count);
    }
}
