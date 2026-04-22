using ClinicalIntelligence.Application.Documents.Dtos;
using ClinicalIntelligence.Application.Documents.Jobs;
using ClinicalIntelligence.Application.Exceptions;
using ClinicalIntelligence.Application.Infrastructure;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Application.Documents.Commands.UploadDocument;

/// <summary>
/// Carries the validated upload payload from the controller to the handler.
/// The <see cref="FileStream"/> is the raw stream of the uploaded file (already validated).
/// </summary>
/// <param name="PatientId">JWT <c>sub</c> claim — identifies document owner.</param>
/// <param name="EncounterId">Optional encounter/appointment association.</param>
/// <param name="OriginalFileName">Original client-provided filename (sanitisation done in LocalFileStorageService).</param>
/// <param name="FileSizeBytes">File size in bytes — validated before this command is created.</param>
/// <param name="FileStream">The raw file content stream; must not be disposed before the handler completes.</param>
public sealed record UploadDocumentCommand(
    Guid    PatientId,
    Guid?   EncounterId,
    string  OriginalFileName,
    long    FileSizeBytes,
    Stream  FileStream) : IRequest<ClinicalDocumentDto>;

/// <summary>
/// Handles <see cref="UploadDocumentCommand"/> — the full US_018 upload pipeline:
/// <list type="number">
///   <item>Store file via <see cref="IFileStorageService"/> (phase-1: local disk).</item>
///   <item>Create <c>ClinicalDocument</c> record with status <c>Queued</c> (AC-3, AC-5).</item>
///   <item>Write AuditLog (AC-3, DR-008).</item>
///   <item>Enqueue <c>DocumentExtractionJob</c> via Hangfire (AC-5, TR-009).</item>
/// </list>
/// Server-side validation (AC-2, AC-4) is performed in the controller BEFORE the command is dispatched,
/// following the same controller-validates-then-dispatches pattern as PatientAccess.
/// </summary>
public sealed class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, ClinicalDocumentDto>
{
    private readonly IFileStorageService         _fileStorage;
    private readonly IClinicalDocumentRepository _repo;
    private readonly IBackgroundJobClient        _backgroundJobs;
    private readonly ILogger<UploadDocumentHandler> _logger;

    public UploadDocumentHandler(
        IFileStorageService            fileStorage,
        IClinicalDocumentRepository    repo,
        IBackgroundJobClient           backgroundJobs,
        ILogger<UploadDocumentHandler> logger)
    {
        _fileStorage    = fileStorage;
        _repo           = repo;
        _backgroundJobs = backgroundJobs;
        _logger         = logger;
    }

    public async Task<ClinicalDocumentDto> Handle(
        UploadDocumentCommand command,
        CancellationToken     cancellationToken)
    {
        // 1. Persist the file — returns the storage URI (relative path in phase-1)
        var fileUri = await _fileStorage.StoreAsync(
            command.FileStream,
            command.OriginalFileName,
            command.PatientId,
            cancellationToken);

        // 2. Create ClinicalDocument record (status = Queued) + write AuditLog
        var documentId = await _repo.CreateDocumentAsync(
            command.PatientId,
            command.EncounterId,
            fileUri,
            command.OriginalFileName,
            command.FileSizeBytes,
            cancellationToken);

        // 3. Enqueue extraction job (fires immediately in background; AC-5, TR-009)
        _backgroundJobs.Enqueue<DocumentExtractionJob>(
            "document-extraction",
            job => job.ExecuteAsync(documentId, CancellationToken.None));

        _logger.LogInformation(
            "Document uploaded: {DocumentId} for patient {PatientId}. Extraction job enqueued.",
            documentId, command.PatientId);

        return new ClinicalDocumentDto(
            documentId,
            command.OriginalFileName,
            command.FileSizeBytes,
            DateTime.UtcNow,
            ExtractionStatus.Queued.ToApiString(),
            command.EncounterId);
    }
}
