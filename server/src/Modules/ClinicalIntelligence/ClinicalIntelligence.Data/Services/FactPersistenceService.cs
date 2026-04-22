using System.Text.Json;
using ClinicalIntelligence.Application.AI.Models;
using ClinicalIntelligence.Application.Documents.Jobs;
using ClinicalIntelligence.Application.Documents.Services;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatientAccess.Data;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Data.Services;

/// <summary>
/// Production implementation of <see cref="IFactPersistenceService"/> (US_020/task_002).
///
/// Responsibilities:
/// - Confidence threshold split (AIR-007): partitions facts into confident (≥ 0.70) and
///   low-confidence (&lt; 0.70) buckets.
/// - Idempotent cleanup: deletes existing <c>ExtractedFact</c> rows for the document before
///   inserting new rows so re-processed documents do not produce duplicate facts.
/// - PHI encryption (DR-015): encrypts <c>FactText</c> with .NET Data Protection API before
///   writing to the database.
/// - Status transition: sets <c>ClinicalDocument.ExtractionStatus</c> to <c>Completed</c> when
///   any confident facts are present; otherwise <c>ManualReview</c> (AIR-007).
/// - AuditLog: single <c>FactsExtracted</c> entry committed atomically with the fact rows.
/// - 360-view trigger: enqueues <c>PatientView360UpdateJob</c> (stub for US_021) after a
///   successful <c>Completed</c> transition.
/// </summary>
public sealed class FactPersistenceService : IFactPersistenceService
{
    private const float ConfidenceThreshold = 0.70f;   // AIR-007

    private readonly PropelIQDbContext              _db;
    private readonly IDataProtector                 _protector;
    private readonly IBackgroundJobClient           _jobs;
    private readonly ILogger<FactPersistenceService> _logger;

    public FactPersistenceService(
        PropelIQDbContext                db,
        IDataProtectionProvider          dataProtectionProvider,
        IBackgroundJobClient             jobs,
        ILogger<FactPersistenceService>  logger)
    {
        _db        = db;
        _protector = dataProtectionProvider.CreateProtector("PropelIQ.ExtractedFacts.FactText");
        _jobs      = jobs;
        _logger    = logger;
    }

    /// <inheritdoc />
    public async Task PersistAsync(
        Guid                               documentId,
        IReadOnlyList<ExtractedFactResult> facts,
        CancellationToken                  ct = default)
    {
        var confident = facts.Where(f => f.ConfidenceScore >= ConfidenceThreshold).ToList();
        var lowConf   = facts.Where(f => f.ConfidenceScore <  ConfidenceThreshold).ToList();

        // Status: Completed when at least one fact meets the threshold; ManualReview when none do (AIR-007)
        var newStatus = confident.Count > 0
            ? ExtractionStatus.Completed
            : ExtractionStatus.ManualReview;

        using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Idempotent cleanup: soft-delete existing facts for this document before re-inserting.
        // IgnoreQueryFilters so the HasQueryFilter(f => !f.IsDeleted) does not hide already-deleted rows.
        // Rows are retained for audit trail per DR-012 and DR-017.
        var softDeletedAt = DateTimeOffset.UtcNow;
        await _db.ExtractedFacts
            .IgnoreQueryFilters()
            .Where(f => f.DocumentId == documentId && !f.IsDeleted)
            .ExecuteUpdateAsync(
                s => s.SetProperty(f => f.IsDeleted, true)
                       .SetProperty(f => f.DeletedAt, softDeletedAt),
                ct);

        // Persist confident facts with encrypted PHI value (DR-015)
        if (confident.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;

            var entities = confident.Select(f => new ExtractedFact
            {
                Id               = Guid.NewGuid(),
                DocumentId       = documentId,
                FactType         = Enum.Parse<FactType>(f.FactType, ignoreCase: true),
                FactText         = _protector.Protect(f.Value),  // PHI encrypted before write (DR-015)
                ConfidenceScore  = f.ConfidenceScore,
                SourceCharOffset = f.SourceCharOffset,
                SourceCharLength = f.SourceCharLength,
                ExtractedAt      = now,
                IsDeleted        = false,
            }).ToList();

            _db.ExtractedFacts.AddRange(entities);
        }

        // Status transition — ExecuteUpdateAsync executes directly against the DB and participates
        // in the ambient transaction opened by BeginTransactionAsync above.
        await _db.ClinicalDocuments
            .IgnoreQueryFilters()
            .Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.ExtractionStatus, newStatus)
                       .SetProperty(d => d.UpdatedAt, DateTime.UtcNow),
                ct);

        // AuditLog written inside the transaction for atomic commit (DR-012)
        // No PHI in the payload — only counts and status (AIR-S03)
        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = Guid.Empty,            // System actor — no human principal
            ActorType      = AuditActorType.System,
            ActionType     = AuditActionType.ClinicalDataModification,
            TargetEntityId = documentId,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(new
            {
                action             = "FactsExtracted",
                confidentCount     = confident.Count,
                lowConfidenceCount = lowConf.Count,
                status             = newStatus.ToString(),
            }),
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "FactPersistenceService: document {DocumentId} persisted {ConfidentCount} fact(s) " +
            "(low-confidence discarded: {LowConfCount}). Status → {Status}.",
            documentId, confident.Count, lowConf.Count, newStatus);

        // Trigger 360-view update after successful completion (stub for US_021)
        if (newStatus == ExtractionStatus.Completed)
            _jobs.Enqueue<PatientView360UpdateJob>(
                j => j.ExecuteAsync(documentId, CancellationToken.None));
    }
}
