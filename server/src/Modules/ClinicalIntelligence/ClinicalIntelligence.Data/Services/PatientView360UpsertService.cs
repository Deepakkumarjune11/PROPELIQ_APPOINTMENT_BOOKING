using System.Text.Json;
using ClinicalIntelligence.Application.Documents.Models;
using ClinicalIntelligence.Application.Documents.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatientAccess.Data;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Data.Services;

/// <summary>
/// Production implementation of <see cref="IPatientView360UpsertService"/> (US_021/task_002).
///
/// Pipeline (6 stages):
/// 1. <b>Resolve patient</b> — derive <c>PatientId</c> from the triggering <c>documentId</c> (AIR-S02).
/// 2. <b>Gather facts</b> — collect all non-deleted facts for every active document belonging to
///    that patient (AIR-S02 scope guard).
/// 3. <b>Decrypt</b> — decrypt <c>FactText</c> values and project to <see cref="FactForAssemblyDto"/>
///    so the assembler never touches ciphertext.  Individual decrypt failures are swallowed and
///    logged so one corrupted fact does not abort the whole run.
/// 4. <b>De-duplicate</b> — delegate to <see cref="PatientView360Assembler"/> (semantic cosine ≥ 0.85
///    when circuit closed; OrdinalIgnoreCase string fallback when circuit open — AIR-003 / AIR-O02).
/// 5. <b>Encrypt + serialize</b> — serialise consolidated entries as JSON; encrypt the full blob
///    with a dedicated Data Protection purpose (DR-015).
/// 6. <b>Optimistic-concurrency upsert</b> — up to 3 retry attempts on
///    <see cref="DbUpdateConcurrencyException"/> (DR-018).
/// </summary>
public sealed class PatientView360UpsertService : IPatientView360UpsertService
{
    private const string FactTextPurpose    = "PropelIQ.ExtractedFacts.FactText";
    private const string View360Purpose     = "PropelIQ.PatientView360.ConsolidatedFacts";
    private const int    UpsertRetries      = 3;

    private readonly PropelIQDbContext                  _db;
    private readonly PatientView360Assembler            _assembler;
    private readonly IDataProtectionProvider            _dataProtectionProvider;
    private readonly ILogger<PatientView360UpsertService> _logger;

    public PatientView360UpsertService(
        PropelIQDbContext                     db,
        PatientView360Assembler               assembler,
        IDataProtectionProvider               dataProtectionProvider,
        ILogger<PatientView360UpsertService>  logger)
    {
        _db                     = db;
        _assembler              = assembler;
        _dataProtectionProvider = dataProtectionProvider;
        _logger                 = logger;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(Guid documentId, CancellationToken ct = default)
    {
        // ── Stage 1: resolve patient from triggering document ─────────────────
        var doc = await _db.ClinicalDocuments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new InvalidOperationException(
                $"PatientView360UpsertService: document {documentId} not found.");

        var patientId = doc.PatientId;

        // ── Stage 2: gather non-deleted facts across all active patient documents (AIR-S02) ──
        var patientDocIds = await _db.ClinicalDocuments
            .Where(d => d.PatientId == patientId && !d.IsDeleted)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var facts = await _db.ExtractedFacts
            .IgnoreQueryFilters()
            .Where(f => !f.IsDeleted && patientDocIds.Contains(f.DocumentId))
            .ToListAsync(ct);

        // Map document IDs → display names (OriginalFileName) for source citations
        var docNames = await _db.ClinicalDocuments
            .Where(d => patientDocIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.OriginalFileName, ct);

        // ── Stage 3: decrypt PHI ciphertext into plain-text DTOs ─────────────
        var factProtector = _dataProtectionProvider.CreateProtector(FactTextPurpose);

        var factDtos = facts
            .Select(f =>
            {
                var plainText = TryUnprotect(factProtector, f.FactText, f.Id);
                return new FactForAssemblyDto(
                    f.Id,
                    f.DocumentId,
                    f.FactType.ToString(),
                    plainText,
                    f.ConfidenceScore,
                    f.SourceCharOffset,
                    f.SourceCharLength);
            })
            .ToList();

        // ── Stage 4: de-duplicate via assembler (AIR-003 / AIR-O02) ──────────
        // The assembler logs "PatientView360AssembledWithFallback" internally when the
        // AI circuit is open and it falls back to string-equality de-duplication (AIR-O02).
        var consolidated = await _assembler.DeduplicateAsync(factDtos, docNames, ct);

        // ── Stage 5: serialise + encrypt consolidated blob (DR-015) ──────────
        var view360Protector = _dataProtectionProvider.CreateProtector(View360Purpose);
        var json             = JsonSerializer.Serialize(consolidated);
        var encryptedJson    = view360Protector.Protect(json);

        // ── Stage 6: optimistic-concurrency upsert (DR-018) ──────────────────
        await UpsertWithRetryAsync(patientId, encryptedJson, ct);

        // AuditLog — no PHI values (AIR-S03)
        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = Guid.Empty,
            ActorType      = AuditActorType.System,
            ActionType     = AuditActionType.ClinicalDataModification,
            TargetEntityId = patientId,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(new
            {
                action             = "PatientView360AssemblyCompleted",
                PatientId          = patientId,
                FactCount          = facts.Count,
                DeduplicatedCount  = consolidated.Count,
            }),
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PatientView360UpsertService: patient {PatientId} 360-view updated — " +
            "{FactCount} facts → {DeduplicatedCount} after de-duplication.",
            patientId, facts.Count, consolidated.Count);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task UpsertWithRetryAsync(Guid patientId, string encryptedJson, CancellationToken ct)
    {
        for (int attempt = 0; attempt < UpsertRetries; attempt++)
        {
            try
            {
                var view360 = await _db.PatientViews360
                    .FirstOrDefaultAsync(v => v.PatientId == patientId, ct);

                if (view360 is null)
                {
                    _db.PatientViews360.Add(new PatientView360
                    {
                        Id                 = Guid.NewGuid(),
                        PatientId          = patientId,
                        ConsolidatedFacts  = encryptedJson,
                        VerificationStatus = VerificationStatus.Pending,
                        LastUpdated        = DateTime.UtcNow,
                        Version            = 0,
                    });
                }
                else
                {
                    view360.ConsolidatedFacts  = encryptedJson;
                    view360.LastUpdated        = DateTime.UtcNow;
                    view360.Version++;
                }

                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < UpsertRetries - 1)
            {
                _db.ChangeTracker.Clear();  // detach stale tracked entity before retry (DR-018)
                _logger.LogWarning(
                    "PatientView360UpsertService: concurrency conflict for patient {PatientId} " +
                    "(attempt {Attempt}/{Max}). Retrying.",
                    patientId, attempt + 1, UpsertRetries);
            }
        }

        _logger.LogWarning(
            "PatientView360UpsertService: upsert abandoned for patient {PatientId} after {Max} retries.",
            patientId, UpsertRetries);
    }

    private string TryUnprotect(IDataProtector protector, string ciphertext, Guid factId)
    {
        try
        {
            return protector.Unprotect(ciphertext);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PatientView360UpsertService: failed to decrypt FactText for fact {FactId}. " +
                "Fact will be excluded from de-duplication.",
                factId);
            return string.Empty;
        }
    }
}
