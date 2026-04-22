using ClinicalIntelligence.Application.AI;
using ClinicalIntelligence.Application.Documents.Dtos;
using ClinicalIntelligence.Application.Infrastructure;
using Hangfire;
using Microsoft.Extensions.Logging;
using PatientAccess.Domain.Enums;
using Polly.CircuitBreaker;

namespace ClinicalIntelligence.Application.Documents.Jobs;

/// <summary>
/// Hangfire background job that generates 1536-dim embedding vectors for staged
/// <c>DocumentChunkEmbedding</c> rows and transitions the document to <c>Completed</c>
/// (US_019/task_002, AC-2).
///
/// Flow:
/// 1. Circuit-open guard — if the Polly circuit is open, flag document <c>ManualReview</c>
///    immediately (AIR-O02); no API call attempted.
/// 2. Load unembedded chunk rows for this document from <c>IEmbeddingChunkRepository</c>.
/// 3. Process in batches of 15 (≤ 8,000 tokens per batch per AIR-O01).
/// 4. Call <see cref="IAiGateway.GenerateEmbeddingsAsync"/> — includes Redis cache check,
///    token budget guard, and AIR-S03 audit logging.
/// 5. Bulk-persist embedding vectors via <c>IEmbeddingChunkRepository.UpdateEmbeddingsAsync</c>.
/// 6. Set <c>ExtractionStatus = Completed</c> + status update via repository.
///
/// On <see cref="BrokenCircuitException"/>: document flagged <c>ManualReview</c>; job does
/// not rethrow, preventing further Hangfire retries for the circuit-open scenario.
///
/// Queue: <c>document-extraction</c> — same isolated queue as the extraction job.
/// Retry policy: 3 attempts with delays of 5 s / 30 s / 60 s (handles transient 429s).
/// </summary>
[Queue("document-extraction")]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 5, 30, 60 })]
public sealed class EmbeddingGenerationJob
{
    private const int BatchSize = 15;   // 15 × 512 tokens = 7,680 < 8,000 (AIR-O01 safe margin)

    private readonly IAiGateway                      _aiGateway;
    private readonly IEmbeddingChunkRepository       _chunkRepo;
    private readonly IClinicalDocumentRepository     _repo;
    private readonly ILogger<EmbeddingGenerationJob> _logger;

    public EmbeddingGenerationJob(
        IAiGateway                      aiGateway,
        IEmbeddingChunkRepository       chunkRepo,
        IClinicalDocumentRepository     repo,
        ILogger<EmbeddingGenerationJob> logger)
    {
        _aiGateway = aiGateway;
        _chunkRepo = chunkRepo;
        _repo      = repo;
        _logger    = logger;
    }

    /// <summary>
    /// Entry point invoked by Hangfire after <see cref="DocumentExtractionJob"/> stages chunks.
    /// </summary>
    /// <param name="documentId">The <c>ClinicalDocument.Id</c> whose chunks need embedding.</param>
    /// <param name="cancellationToken">Hangfire supplies a token on graceful shutdown.</param>
    public async Task ExecuteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("EmbeddingGenerationJob starting for document {DocumentId}.", documentId);

        // Step 1: Circuit-open guard (AIR-O02) — skip API calls when provider is down
        if (_aiGateway.IsCircuitOpen)
        {
            _logger.LogWarning(
                "EmbeddingGenerationJob: AI gateway circuit open for document {DocumentId}; flagging ManualReview.",
                documentId);
            await _repo.FlagForManualReviewAsync(documentId, "CircuitOpen", cancellationToken);
            return;
        }

        // Step 2: Load unembedded chunk rows
        var chunks = await _chunkRepo.GetUnembeddedChunksAsync(documentId, cancellationToken);
        if (chunks.Count == 0)
        {
            _logger.LogInformation(
                "EmbeddingGenerationJob: no unembedded chunks found for document {DocumentId}.",
                documentId);
            return;
        }

        // Step 3 + 4: Batch-generate embeddings
        var updates = new List<EmbeddingChunkUpdateDto>(chunks.Count);
        foreach (var batch in chunks.Chunk(BatchSize))
        {
            try
            {
                var texts   = batch.Select(c => c.ChunkText).ToList();
                var vectors = await _aiGateway.GenerateEmbeddingsAsync(texts, documentId, cancellationToken);

                for (int i = 0; i < batch.Length; i++)
                    updates.Add(new EmbeddingChunkUpdateDto(batch[i].Id, vectors[i]));
            }
            catch (BrokenCircuitException)
            {
                // Circuit tripped mid-job — persist work done so far, flag document
                if (updates.Count > 0)
                    await _chunkRepo.UpdateEmbeddingsAsync(updates, cancellationToken);

                await _repo.FlagForManualReviewAsync(documentId, "CircuitBroken", cancellationToken);

                _logger.LogWarning(
                    "EmbeddingGenerationJob: circuit broken at batch for document {DocumentId}; flagging ManualReview.",
                    documentId);
                return;
            }
        }

        // Step 5: Bulk-persist all embedding vectors
        await _chunkRepo.UpdateEmbeddingsAsync(updates, cancellationToken);

        // Step 6: Transition document to Completed
        await _repo.UpdateExtractionStatusAsync(documentId, ExtractionStatus.Completed, cancellationToken);

        _logger.LogInformation(
            "EmbeddingGenerationJob complete: document {DocumentId} → {ChunkCount} vectors saved, status=Completed.",
            documentId, updates.Count);
    }
}

