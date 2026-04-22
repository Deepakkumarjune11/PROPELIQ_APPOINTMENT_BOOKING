using ClinicalIntelligence.Application.Documents.Dtos;
using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Application.Infrastructure;

/// <summary>
/// Data-layer contract for clinical document operations (US_018).
/// Handlers inject this interface; the Data layer provides the EF Core implementation.
/// </summary>
public interface IClinicalDocumentRepository
{
    /// <summary>
    /// Creates a <c>ClinicalDocument</c> record, writes an AuditLog entry, saves changes,
    /// and returns the new document GUID.
    /// Encryption of <c>FileReference</c> is performed transparently by the EF ValueConverter (DR-015).
    /// </summary>
    Task<Guid> CreateDocumentAsync(
        Guid              patientId,
        Guid?             encounterId,
        string            fileUri,
        string            originalFileName,
        long              fileSizeBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the authenticated patient's active (non-deleted) documents, most recent first.
    /// Patient ownership enforced via the <paramref name="patientId"/> filter (OWASP A01).
    /// </summary>
    Task<IReadOnlyList<ClinicalDocumentDto>> GetPatientDocumentsAsync(
        Guid              patientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a document after verifying the caller owns it.
    /// Throws <see cref="Exceptions.NotFoundException"/> when document does not exist.
    /// Throws <see cref="Exceptions.ForbiddenException"/> when <paramref name="patientId"/> does not match the record.
    /// Physical file is retained for audit purposes per DR-013.
    /// </summary>
    Task DeleteDocumentAsync(
        Guid              documentId,
        Guid              patientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the extraction status of a document (called by <c>DocumentExtractionJob</c>).
    /// </summary>
    Task UpdateExtractionStatusAsync(
        Guid              documentId,
        ExtractionStatus  newStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the decrypted <c>FileReference</c> and <c>PatientId</c> for internal job use.
    /// The <c>FileReference</c> is automatically decrypted by the EF Core ValueConverter (DR-015).
    /// Returns <c>null</c> when the document does not exist.
    /// </summary>
    Task<(string FileReference, Guid PatientId)?> GetDocumentForProcessingAsync(
        Guid              documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets <c>ExtractionStatus</c> to <c>ManualReview</c> and writes an AuditLog entry
    /// <c>DocumentFlaggedForManualReview</c> with the supplied reason (e.g. "EmptyTextExtraction").
    /// Called when PDF text extraction yields no usable text (scanned image PDF).
    /// </summary>
    Task FlagForManualReviewAsync(
        Guid              documentId,
        string            reason,
        CancellationToken cancellationToken = default);
}
