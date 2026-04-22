using System.Text.Json;
using ClinicalIntelligence.Application.Documents;
using ClinicalIntelligence.Application.Documents.Dtos;
using ClinicalIntelligence.Application.Exceptions;
using ClinicalIntelligence.Application.Infrastructure;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Data;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IClinicalDocumentRepository"/> using <see cref="PropelIQDbContext"/>.
/// FileReference encryption is transparent via the ValueConverter registered in
/// <c>ClinicalDocumentConfiguration</c> (DR-015).
/// </summary>
public sealed class ClinicalDocumentRepository : IClinicalDocumentRepository
{
    private readonly PropelIQDbContext _db;

    public ClinicalDocumentRepository(PropelIQDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<Guid> CreateDocumentAsync(
        Guid              patientId,
        Guid?             encounterId,
        string            fileUri,
        string            originalFileName,
        long              fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        var now      = DateTime.UtcNow;
        var document = new ClinicalDocument
        {
            Id              = Guid.NewGuid(),
            PatientId       = patientId,
            EncounterId     = encounterId,
            FileReference   = fileUri,                         // ValueConverter encrypts this on save
            OriginalFileName = originalFileName,
            FileSizeBytes   = fileSizeBytes,
            ExtractionStatus = ExtractionStatus.Queued,
            UploadedAt      = now,
            UpdatedAt       = now,
            IsDeleted       = false,
        };

        _db.ClinicalDocuments.Add(document);

        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = patientId,
            ActorType      = AuditActorType.Patient,
            ActionType     = AuditActionType.DocumentUpload,
            TargetEntityId = document.Id,
            OccurredAt     = now,
            Details        = JsonSerializer.Serialize(new
            {
                action       = "DocumentUploaded",
                fileName     = originalFileName,
                fileSizeBytes,
            }),
        });

        await _db.SaveChangesAsync(cancellationToken);

        return document.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClinicalDocumentDto>> GetPatientDocumentsAsync(
        Guid              patientId,
        CancellationToken cancellationToken = default)
    {
        // HasQueryFilter(d => !d.IsDeleted) in the configuration already filters soft-deleted rows.
        return await _db.ClinicalDocuments
            .AsNoTracking()
            .Where(d => d.PatientId == patientId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new ClinicalDocumentDto(
                d.Id,
                d.OriginalFileName,
                d.FileSizeBytes,
                d.UploadedAt,
                d.ExtractionStatus.ToApiString(),
                d.EncounterId))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteDocumentAsync(
        Guid              documentId,
        Guid              patientId,
        CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters so an already soft-deleted record returns 404, not 403
        var document = await _db.ClinicalDocuments
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException($"Document {documentId} not found.");

        if (document.PatientId != patientId)
            throw new ForbiddenException("You do not have permission to delete this document.");

        if (document.IsDeleted)
            return; // Idempotent — already deleted, nothing to do

        document.SoftDelete();

        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = patientId,
            ActorType      = AuditActorType.Patient,
            ActionType     = AuditActionType.DocumentUpload,    // Closest available action type (DR-008)
            TargetEntityId = documentId,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(new
            {
                action   = "DocumentDeleted",
                fileName = document.OriginalFileName,
            }),
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateExtractionStatusAsync(
        Guid              documentId,
        ExtractionStatus  newStatus,
        CancellationToken cancellationToken = default)
    {
        await _db.ClinicalDocuments
            .IgnoreQueryFilters()
            .Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.ExtractionStatus, newStatus)
                       .SetProperty(d => d.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(string FileReference, Guid PatientId)?> GetDocumentForProcessingAsync(
        Guid              documentId,
        CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters so soft-deleted documents are still accessible for audit purposes.
        // FileReference is decrypted transparently by the EF Core ValueConverter (DR-015).
        var doc = await _db.ClinicalDocuments
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.Id == documentId)
            .Select(d => new { d.FileReference, d.PatientId })
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : (doc.FileReference, doc.PatientId);
    }

    /// <inheritdoc />
    public async Task FlagForManualReviewAsync(
        Guid              documentId,
        string            reason,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        await _db.ClinicalDocuments
            .IgnoreQueryFilters()
            .Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.ExtractionStatus, ExtractionStatus.ManualReview)
                       .SetProperty(d => d.UpdatedAt, now),
                cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = Guid.Empty,               // System actor — no human principal
            ActorType      = AuditActorType.System,
            ActionType     = AuditActionType.ClinicalDataModification,
            TargetEntityId = documentId,
            OccurredAt     = now,
            Details        = JsonSerializer.Serialize(new
            {
                action = "DocumentFlaggedForManualReview",
                reason,
            }),
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
