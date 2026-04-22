using ClinicalIntelligence.Application.Documents.Services;
using ClinicalIntelligence.Application.Infrastructure;
using Hangfire;
using Microsoft.Extensions.Logging;
using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Application.Documents.Jobs;

/// <summary>
/// Hangfire background job that implements the deterministic phase of the AI extraction
/// pipeline (US_019/task_001):
///
/// 1. <b>Status transition</b>: <c>Queued</c> → <c>Processing</c> (AC-4).
/// 2. <b>PDF text extraction</b>: <see cref="PdfTextExtractor"/> reads the file via
///    <see cref="IFileStorageService"/> and concatenates page text in reading order.
/// 3. <b>Empty-text guard</b>: scanned PDFs with no OCR layer are flagged
///    <c>ManualReview</c> with an AuditLog entry; job exits without retrying.
/// 4. <b>Chunking</b>: <see cref="DocumentChunker"/> splits the text into 512-token
///    windows (step=384, 25% overlap per AIR-R01); chunks are staged for embedding.
/// 5. <b>Job enqueue</b>: <see cref="EmbeddingGenerationJob"/> is enqueued for the AI
///    embedding phase (US_019/task_002).
///
/// Queue: <c>document-extraction</c> — isolated from appointment/booking jobs (TR-009).
/// Retry policy: up to 3 automatic attempts with exponential back-off.
/// </summary>
[Queue("document-extraction")]
[AutomaticRetry(Attempts = 3)]
public sealed class DocumentExtractionJob
{
    private readonly IClinicalDocumentRepository     _repo;
    private readonly PdfTextExtractor                _pdfExtractor;
    private readonly DocumentChunker                 _chunker;
    private readonly IChunkStagingService            _chunkStaging;
    private readonly IBackgroundJobClient            _jobs;
    private readonly ILogger<DocumentExtractionJob>  _logger;

    public DocumentExtractionJob(
        IClinicalDocumentRepository    repo,
        PdfTextExtractor               pdfExtractor,
        DocumentChunker                chunker,
        IChunkStagingService           chunkStaging,
        IBackgroundJobClient           jobs,
        ILogger<DocumentExtractionJob> logger)
    {
        _repo         = repo;
        _pdfExtractor = pdfExtractor;
        _chunker      = chunker;
        _chunkStaging = chunkStaging;
        _jobs         = jobs;
        _logger       = logger;
    }

    /// <summary>
    /// Entry point invoked by Hangfire after the upload transaction commits.
    /// </summary>
    /// <param name="documentId">The <c>ClinicalDocument.Id</c> to process.</param>
    /// <param name="cancellationToken">Hangfire supplies a token on graceful shutdown.</param>
    public async Task ExecuteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("DocumentExtractionJob starting for document {DocumentId}.", documentId);

        // Step 1: Queued → Processing so the FE badge updates immediately (AC-4).
        await _repo.UpdateExtractionStatusAsync(documentId, ExtractionStatus.Processing, cancellationToken);

        // Step 2: Resolve the storage URI. FileReference is decrypted transparently
        // by the EF Core ValueConverter (DR-015) — no manual Unprotect call needed.
        var docInfo = await _repo.GetDocumentForProcessingAsync(documentId, cancellationToken);
        if (docInfo is null)
        {
            _logger.LogError(
                "DocumentExtractionJob: document {DocumentId} not found — cannot extract.",
                documentId);
            return;
        }

        // Step 3: Extract text from the PDF.
        var text = await _pdfExtractor.ExtractTextAsync(docInfo.Value.FileReference, cancellationToken);

        // Step 4: Empty-text guard — scanned PDF without OCR layer.
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning(
                "DocumentExtractionJob: no text extracted from document {DocumentId}. Flagging for manual review.",
                documentId);

            await _repo.FlagForManualReviewAsync(documentId, "EmptyTextExtraction", cancellationToken);
            return;
        }

        // Step 5: Chunk the text and stage rows for the embedding pipeline.
        var chunks = _chunker.ChunkAsync(documentId, text, cancellationToken);
        await _chunkStaging.StageChunksAsync(chunks, cancellationToken);

        // Step 6: Enqueue the AI embedding job (US_019/task_002).
        _jobs.Enqueue<EmbeddingGenerationJob>(j => j.ExecuteAsync(documentId, CancellationToken.None));

        _logger.LogInformation(
            "DocumentExtractionJob complete: document {DocumentId} chunked and queued for embedding.",
            documentId);
    }
}

