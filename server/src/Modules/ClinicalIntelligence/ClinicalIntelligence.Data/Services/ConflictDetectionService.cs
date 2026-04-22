using System.Text.Json;
using ClinicalIntelligence.Application.AI;
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
/// Production implementation of <see cref="IConflictDetectionService"/> (US_022, AIR-004).
///
/// Pipeline (7 stages):
/// 1. <b>Resolve patient</b> — derive PatientId from the triggering documentId (AIR-S02 scope guard).
/// 2. <b>Load 360-view</b> — fetch PatientView360 for the resolved patient.
/// 3. <b>Decrypt consolidated facts</b> — unprotect and deserialise ConsolidatedFacts blob (DR-015).
/// 4. <b>Detect conflicts</b> — group facts by FactType; compare pairwise via embedding cosine
///    distance (&lt; 0.70 = conflict, AIR-004); fall back to string-inequality when the AI circuit
///    is open (AIR-O02); batch embeddings ≤ 15 per request (AIR-O01).
/// 5. <b>Encrypt conflict flags</b> — serialise and encrypt each <see cref="ConflictFlag"/>
///    individually so <c>ConflictFlags.Length</c> gives the count without decryption (DR-015).
/// 6. <b>Persist</b> — bulk-update ConflictFlags and VerificationStatus via ExecuteUpdateAsync
///    (idempotent; detection is safe to re-run on retry).
/// 7. <b>AuditLog</b> — write ConflictDetectionCompleted entry without PHI values (AIR-S03).
/// </summary>
public sealed class ConflictDetectionService : IConflictDetectionService
{
    private const string View360Purpose    = "PropelIQ.PatientView360.ConsolidatedFacts";
    private const string ConflictPurpose   = "PropelIQ.PatientView360.ConflictFlags";
    private const float  ConflictThreshold = 0.70f;  // AIR-004: inverse of de-dup merge threshold 0.85
    private const int    EmbeddingBatch    = 15;      // AIR-O01: max inputs per embedding request

    private readonly PropelIQDbContext                  _db;
    private readonly IDataProtectionProvider            _dataProtectionProvider;
    private readonly IAiGateway                         _aiGateway;
    private readonly ILogger<ConflictDetectionService>  _logger;

    public ConflictDetectionService(
        PropelIQDbContext                  db,
        IDataProtectionProvider            dataProtectionProvider,
        IAiGateway                         aiGateway,
        ILogger<ConflictDetectionService>  logger)
    {
        _db                     = db;
        _dataProtectionProvider = dataProtectionProvider;
        _aiGateway              = aiGateway;
        _logger                 = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> DetectAndSaveAsync(Guid documentId, CancellationToken ct = default)
    {
        // ── Stage 1: resolve patient (AIR-S02 scope guard) ────────────────────
        var doc = await _db.ClinicalDocuments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (doc is null)
        {
            _logger.LogWarning(
                "ConflictDetectionService: document {DocumentId} not found; skipping.",
                documentId);
            return Guid.Empty;
        }

        var patientId = doc.PatientId;

        // ── Stage 2: load assembled view ──────────────────────────────────────
        var view360 = await _db.PatientViews360
            .FirstOrDefaultAsync(v => v.PatientId == patientId, ct);

        if (view360 is null)
        {
            _logger.LogWarning(
                "ConflictDetectionService: no 360-view for patient {PatientId}; skipping.",
                patientId);
            return patientId;
        }

        // ── Stage 3: decrypt consolidated facts (DR-015) ─────────────────────
        var factsProtector = _dataProtectionProvider.CreateProtector(View360Purpose);
        List<ConsolidatedFactEntry> facts;
        try
        {
            var json = factsProtector.Unprotect(view360.ConsolidatedFacts);
            facts    = JsonSerializer.Deserialize<List<ConsolidatedFactEntry>>(json) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ConflictDetectionService: failed to decrypt 360-view for patient {PatientId}; skipping.",
                patientId);
            return patientId;
        }

        // ── Stage 4: detect conflicts (AIR-004) ───────────────────────────────
        var conflicts = await DetectConflictsAsync(facts, ct);

        // ── Stage 5: encrypt each flag individually (DR-015) ─────────────────
        var conflictProtector = _dataProtectionProvider.CreateProtector(ConflictPurpose);
        var encryptedFlags    = conflicts
            .Select(c => conflictProtector.Protect(JsonSerializer.Serialize(c)))
            .ToArray();

        var newStatus = conflicts.Count > 0
            ? VerificationStatus.NeedsReview
            : VerificationStatus.Pending;

        // ── Stage 6: bulk-update (idempotent; bypasses concurrency token intentionally) ──
        await _db.PatientViews360
            .Where(v => v.PatientId == patientId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.ConflictFlags,       encryptedFlags)
                .SetProperty(v => v.VerificationStatus,  newStatus)
                .SetProperty(v => v.LastUpdated,         DateTime.UtcNow), ct);

        // ── Stage 7: AuditLog — no PHI values (AIR-S03) ──────────────────────
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
                action                = "ConflictDetectionCompleted",
                PatientId             = patientId,
                FactTypeGroupsChecked = facts.GroupBy(f => f.FactType).Count(),
                ConflictsFound        = conflicts.Count,
            }),
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ConflictDetectionService: patient {PatientId} — {ConflictCount} conflict(s) detected.",
            patientId, conflicts.Count);

        return patientId;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Groups facts by FactType and performs pairwise conflict detection within each group.
    /// Uses embedding cosine distance when the AI gateway is reachable (AIR-004);
    /// falls back to string-inequality when the circuit is open (AIR-O02).
    /// </summary>
    private async Task<List<ConflictFlag>> DetectConflictsAsync(
        List<ConsolidatedFactEntry> facts,
        CancellationToken           ct)
    {
        var conflicts = new List<ConflictFlag>();

        foreach (var group in facts.GroupBy(f => f.FactType))
        {
            var entries = group.ToList();
            if (entries.Count < 2) continue;  // single entry — no cross-doc conflict possible

            var values     = entries.Select(e => e.Value).ToList();
            float[][]? embeddings = null;

            if (!_aiGateway.IsCircuitOpen)
            {
                try
                {
                    embeddings = await GetEmbeddingsInBatchesAsync(values, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "ConflictDetectionService: embedding generation failed for FactType {FactType}; " +
                        "falling back to string-inequality comparison (AIR-O02).",
                        group.Key);
                }
            }
            else
            {
                _logger.LogWarning(
                    "ConflictDetectionService: AI circuit open — using string-inequality fallback " +
                    "for FactType {FactType} conflict detection (AIR-O02).",
                    group.Key);
            }

            // Pairwise: cosine < 0.70 OR string-inequality → conflict (AIR-004)
            for (int i = 0; i < entries.Count - 1; i++)
            for (int j = i + 1; j < entries.Count; j++)
            {
                bool isConflict = embeddings is not null
                    ? CosineSimilarity(embeddings[i], embeddings[j]) < ConflictThreshold
                    : !string.Equals(
                        NormalizeValue(entries[i].Value),
                        NormalizeValue(entries[j].Value),
                        StringComparison.OrdinalIgnoreCase);

                if (!isConflict) continue;

                conflicts.Add(new ConflictFlag(
                    Guid.NewGuid(),
                    group.Key,
                    new[] { BuildConflictSource(entries[i]), BuildConflictSource(entries[j]) }));
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Calls <see cref="IAiGateway.GenerateEmbeddingsAsync"/> in batches of
    /// <see cref="EmbeddingBatch"/> (≤ 15 per AIR-O01) and returns a flat array
    /// of embedding vectors in the same order as <paramref name="values"/>.
    /// </summary>
    private async Task<float[][]> GetEmbeddingsInBatchesAsync(
        IReadOnlyList<string> values,
        CancellationToken     ct)
    {
        var result = new float[values.Count][];

        for (int start = 0; start < values.Count; start += EmbeddingBatch)
        {
            var batch   = values.Skip(start).Take(EmbeddingBatch).ToList();
            var vectors = await _aiGateway.GenerateEmbeddingsAsync(batch, Guid.Empty, ct);

            for (int k = 0; k < vectors.Count; k++)
                result[start + k] = vectors[k];
        }

        return result;
    }

    private static ConflictSource BuildConflictSource(ConsolidatedFactEntry entry)
    {
        var src = entry.Sources.Count > 0 ? entry.Sources[0] : null;
        return new ConflictSource(
            src?.DocumentId ?? Guid.Empty,
            src?.DocumentName ?? string.Empty,
            entry.Value,
            entry.ConfidenceScore,
            src?.SourceCharOffset);
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0f, magA = 0f, magB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot  += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return (magA == 0f || magB == 0f) ? 0f : dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
    }

    private static string NormalizeValue(string value)
        => value.Trim().ToLowerInvariant();
}
