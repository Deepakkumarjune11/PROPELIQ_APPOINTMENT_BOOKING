using ClinicalIntelligence.Application.Documents.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ClinicalIntelligence.Application.Documents.Jobs;

/// <summary>
/// Hangfire background job that detects clinically meaningful conflicts within a patient's
/// assembled 360-view and stores encrypted conflict flags (US_022, AIR-004).
///
/// Triggered as a chain from <see cref="PatientView360UpdateJob"/> immediately after the
/// 360-view assembly succeeds. When conflicts are found, sets
/// <c>VerificationStatus = NeedsReview</c> so the patient appears in the verification queue.
/// After detection completes, enqueues <see cref="CodeSuggestionJob"/> to generate AI-assisted
/// code suggestions for the same patient (US_023).
///
/// Queue: <c>conflict-detection</c>.
/// Retry: 3 attempts with custom back-off delays (10s / 60s / 180s).
/// Concurrency: one job per patient at a time (<see cref="DisableConcurrentExecutionAttribute"/>
/// 60-second mutex timeout).
/// </summary>
[Queue("conflict-detection")]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 60, 180 })]
[DisableConcurrentExecution(timeoutInSeconds: 60)]
public sealed class ConflictDetectionJob
{
    private readonly IConflictDetectionService      _detectionService;
    private readonly IBackgroundJobClient           _backgroundJobClient;
    private readonly ILogger<ConflictDetectionJob>  _logger;

    public ConflictDetectionJob(
        IConflictDetectionService     detectionService,
        IBackgroundJobClient          backgroundJobClient,
        ILogger<ConflictDetectionJob> logger)
    {
        _detectionService    = detectionService;
        _backgroundJobClient = backgroundJobClient;
        _logger              = logger;
    }

    /// <summary>
    /// Runs conflict detection for the patient associated with <paramref name="documentId"/>,
    /// then enqueues <see cref="CodeSuggestionJob"/> for the resolved patient.
    /// </summary>
    /// <param name="documentId">Source document that triggered the 360-view update.</param>
    /// <param name="ct">Hangfire-supplied cancellation token.</param>
    public async Task ExecuteAsync(Guid documentId, CancellationToken ct)
    {
        _logger.LogInformation(
            "ConflictDetectionJob: starting conflict detection for document {DocumentId}.",
            documentId);

        var patientId = await _detectionService.DetectAndSaveAsync(documentId, ct);

        _logger.LogInformation(
            "ConflictDetectionJob: completed conflict detection for document {DocumentId}.",
            documentId);

        // Chain code suggestion generation after conflict detection (US_023)
        if (patientId != Guid.Empty)
        {
            _backgroundJobClient.Enqueue<CodeSuggestionJob>(
                j => j.ExecuteAsync(patientId, CancellationToken.None));

            _logger.LogInformation(
                "ConflictDetectionJob: enqueued CodeSuggestionJob for patient {PatientId}.",
                patientId);
        }
    }
}
