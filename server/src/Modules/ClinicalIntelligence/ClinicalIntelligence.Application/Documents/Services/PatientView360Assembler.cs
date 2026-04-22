using ClinicalIntelligence.Application.AI;
using ClinicalIntelligence.Application.Documents.Models;
using Microsoft.Extensions.Logging;

namespace ClinicalIntelligence.Application.Documents.Services;

/// <summary>
/// De-duplicates pre-decrypted clinical facts per FactType group to produce the
/// consolidated 360-degree patient view (US_021, AIR-003).
///
/// Callers are responsible for decrypting PHI before passing facts in, so this
/// service operates on plain-text values and has no dependency on the Data layer
/// or the Data Protection API.
///
/// De-duplication strategy (per FactType group):
/// 1. <b>Single-document group</b>: pass through (no cross-document duplicates possible).
/// 2. <b>Multi-document group, circuit closed</b>: embed values via
///    <see cref="IAiGateway.GenerateEmbeddingsAsync"/> (batches ≤ 15); compute pairwise
///    cosine similarity; merge pairs with cosine ≥ 0.85 (AIR-003), keeping highest
///    <c>ConfidenceScore</c> and unioning all <c>Sources</c>.
/// 3. <b>Multi-document group, circuit open</b>: fall back to normalised
///    <c>OrdinalIgnoreCase</c> string equality (AIR-O02). Callers must log the fallback.
/// </summary>
public sealed class PatientView360Assembler
{
    private const float SemanticThreshold = 0.85f;  // AIR-003
    private const int   MaxEmbeddingBatch = 15;      // AIR-O01 gateway limit

    private readonly IAiGateway                       _aiGateway;
    private readonly ILogger<PatientView360Assembler> _logger;

    public PatientView360Assembler(IAiGateway aiGateway, ILogger<PatientView360Assembler> logger)
    {
        _aiGateway = aiGateway;
        _logger    = logger;
    }

    /// <summary>
    /// De-duplicates <paramref name="facts"/> (pre-decrypted) per FactType group.
    /// </summary>
    /// <param name="facts">
    /// Decrypted facts for all patient documents.
    /// Pre-filtered to non-deleted records by the caller (AIR-S02).
    /// </param>
    /// <param name="docNames">Map of DocumentId → OriginalFileName (display only; no PHI).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>De-duplicated consolidated fact entries ready for serialisation.</returns>
    public async Task<List<ConsolidatedFactEntry>> DeduplicateAsync(
        IReadOnlyList<FactForAssemblyDto>   facts,
        IReadOnlyDictionary<Guid, string>   docNames,
        CancellationToken                   ct)
    {
        if (facts.Count == 0)
            return [];

        var result = new List<ConsolidatedFactEntry>();

        // Group by FactType for independent de-duplication per category (AIR-003)
        var byType = facts.GroupBy(f => f.FactType);

        foreach (var group in byType)
        {
            var items = group.ToList();

            // Single document: no cross-document duplicates → pass through
            var distinctDocuments = items.Select(x => x.DocumentId).Distinct().Count();
            if (distinctDocuments <= 1 || items.Count == 1)
            {
                result.AddRange(items.Select(x => ToEntry(x, docNames)));
                continue;
            }

            if (_aiGateway.IsCircuitOpen)
            {
                _logger.LogWarning(
                    "PatientView360Assembler: circuit open for FactType={FactType}; " +
                    "falling back to string-equality de-duplication (AIR-O02).",
                    group.Key);
                result.AddRange(StringDeduplicate(items, docNames));
            }
            else
            {
                var semantic = await SemanticDeduplicateAsync(items, docNames, group.Key, ct);
                result.AddRange(semantic);
            }
        }

        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Embeds fact values in batches of ≤15 and merges pairs with cosine ≥ 0.85 using
    /// Union-Find for transitive grouping (AIR-003).
    /// </summary>
    private async Task<IReadOnlyList<ConsolidatedFactEntry>> SemanticDeduplicateAsync(
        IReadOnlyList<FactForAssemblyDto>   items,
        IReadOnlyDictionary<Guid, string>   docNames,
        string                              factType,
        CancellationToken                   ct)
    {
        var values  = items.Select(x => x.PlainTextValue).ToList();
        var allVecs = new List<float[]>(items.Count);

        for (int i = 0; i < values.Count; i += MaxEmbeddingBatch)
        {
            var batch   = values.GetRange(i, Math.Min(MaxEmbeddingBatch, values.Count - i));
            // Guid.Empty: not processing a single document — no per-document audit needed (AIR-S03)
            var vectors = await _aiGateway.GenerateEmbeddingsAsync(batch, Guid.Empty, ct);
            allVecs.AddRange(vectors);
        }

        // Union-Find with path compression
        var parent = Enumerable.Range(0, items.Count).ToArray();

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a != b) parent[a] = b;
        }

        for (int i = 0; i < items.Count; i++)
            for (int j = i + 1; j < items.Count; j++)
                if (CosineSimilarity(allVecs[i], allVecs[j]) >= SemanticThreshold)
                    Union(i, j);

        var groups = items
            .Select((item, idx) => (item, root: Find(idx)))
            .GroupBy(x => x.root)
            .Select(g =>
            {
                var best    = g.OrderByDescending(x => x.item.ConfidenceScore).First();
                var sources = g.Select(x => BuildSourceRef(x.item, docNames)).ToList();
                return new ConsolidatedFactEntry(factType, best.item.PlainTextValue, best.item.ConfidenceScore, sources);
            })
            .ToList();

        _logger.LogInformation(
            "PatientView360Assembler: FactType={FactType} {Input} facts → {Output} after semantic de-duplication.",
            factType, items.Count, groups.Count);

        return groups;
    }

    /// <summary>Normalised string-equality de-duplication fallback (AIR-O02 circuit-open path).</summary>
    private static IReadOnlyList<ConsolidatedFactEntry> StringDeduplicate(
        IReadOnlyList<FactForAssemblyDto>   items,
        IReadOnlyDictionary<Guid, string>   docNames)
        => items
            .GroupBy(x => x.PlainTextValue.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var best    = g.OrderByDescending(x => x.ConfidenceScore).First();
                var sources = g.Select(x => BuildSourceRef(x, docNames)).ToList();
                return new ConsolidatedFactEntry(best.FactType, best.PlainTextValue, best.ConfidenceScore, sources);
            })
            .ToList();

    private static ConsolidatedFactEntry ToEntry(
        FactForAssemblyDto                fact,
        IReadOnlyDictionary<Guid, string> docNames)
        => new(fact.FactType, fact.PlainTextValue, fact.ConfidenceScore, [BuildSourceRef(fact, docNames)]);

    private static FactSourceRef BuildSourceRef(
        FactForAssemblyDto                fact,
        IReadOnlyDictionary<Guid, string> docNames)
        => new(
            fact.DocumentId,
            docNames.TryGetValue(fact.DocumentId, out var name) ? name : fact.DocumentId.ToString(),
            fact.SourceCharOffset,
            fact.SourceCharLength);

    /// <summary>Dot-product cosine similarity — handles zero-norm vectors safely.</summary>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom < 1e-9f ? 0f : dot / denom;
    }
}
