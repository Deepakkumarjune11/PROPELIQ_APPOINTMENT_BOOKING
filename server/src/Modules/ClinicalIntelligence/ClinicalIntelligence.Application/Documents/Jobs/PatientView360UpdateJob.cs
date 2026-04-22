using ClinicalIntelligence.Application.Documents.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ClinicalIntelligence.Application.Documents.Jobs;

/// <summary>
/// Hangfire background job that assembles (or refreshes) the unified 360-degree patient view
/// after AI fact extraction completes for a document (US_021, AC-1 through AC-4).
///
/// This job is a thin orchestrator: all DB access, de-duplication, and encryption logic lives
/// in <see cref="IPatientView360UpsertService"/> (implemented in the Data layer) to keep the
/// Application layer free of EF Core and Data Protection dependencies.
///
/// After a successful upsert, <see cref="ConflictDetectionJob"/> is enqueued on the
/// <c>conflict-detection</c> queue to detect cross-document clinical conflicts (US_022, AIR-004).
///
/// Queue: <c>view360-update</c>.
/// Retry: 3 attempts with Hangfire's default exponential back-off.
/// Concurrency: <see cref="DisableConcurrentExecutionAttribute"/> prevents two workers from
/// assembling the same patient's view simultaneously (60-second timeout).
/// </summary>
[Queue("view360-update")]
[AutomaticRetry(Attempts = 3)]
[DisableConcurrentExecution(timeoutInSeconds: 60)]
public sealed class PatientView360UpdateJob
{
    private readonly IPatientView360UpsertService        _upsertService;
    private readonly IBackgroundJobClient                _backgroundJobClient;
    private readonly ILogger<PatientView360UpdateJob>    _logger;

    public PatientView360UpdateJob(
        IPatientView360UpsertService      upsertService,
        IBackgroundJobClient              backgroundJobClient,
        ILogger<PatientView360UpdateJob>  logger)
    {
        _upsertService       = upsertService;
        _backgroundJobClient = backgroundJobClient;
        _logger              = logger;
    }

    /// <summary>
    /// Entry point invoked by Hangfire when <c>FactPersistenceService</c> completes a
    /// <c>Completed</c> extraction (US_020 AC-4).
    /// After assembly succeeds, enqueues <see cref="ConflictDetectionJob"/> (US_022).
    /// </summary>
    /// <param name="documentId">Source document whose facts triggered the 360-view refresh.</param>
    /// <param name="ct">Hangfire supplies a cancellation token on graceful shutdown.</param>
    public async Task ExecuteAsync(Guid documentId, CancellationToken ct)
    {
        _logger.LogInformation(
            "PatientView360UpdateJob: starting 360-view assembly for document {DocumentId}.",
            documentId);

        await _upsertService.UpsertAsync(documentId, ct);

        _logger.LogInformation(
            "PatientView360UpdateJob: completed 360-view assembly for document {DocumentId}.",
            documentId);

        // Chain conflict detection (US_022) — runs after this job succeeds
        _backgroundJobClient.Enqueue<ConflictDetectionJob>(
            j => j.ExecuteAsync(documentId, CancellationToken.None));
    }
}
