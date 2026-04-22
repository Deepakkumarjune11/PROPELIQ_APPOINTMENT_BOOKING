using System.Text;
using ClinicalIntelligence.Application.Documents.Dtos;
using ClinicalIntelligence.Application.Documents.Models;
using Microsoft.Extensions.Logging;

namespace ClinicalIntelligence.Application.Documents.Services;

/// <summary>
/// Assembles the RAG context window from retrieved document chunks.
///
/// Implements two-phase pipeline:
/// 1. **Re-rank** (AIR-R03): Score each chunk by <c>cosine_similarity × (token_count / 512f)</c>
///    to prefer high-similarity chunks that also carry dense information. Sort descending.
/// 2. **Greedy window** (AIR-R04): Include chunks in ranked order until the cumulative
///    token count reaches 3,000; exclude any chunk that would exceed the limit.
///
/// Output format uses numbered citation anchors (<c>[1] ... [2] ...</c>) so GPT-4 can
/// produce <c>sourceCharOffset</c> / <c>sourceCharLength</c> references traceable back
/// to the assembled context string (AIR-006).
/// </summary>
public sealed class ContextAssembler
{
    private const int MaxContextTokens = 3_000;   // AIR-R04
    private const float TokenNormFactor = 512f;   // expected avg chunk token count

    private readonly ILogger<ContextAssembler> _logger;

    public ContextAssembler(ILogger<ContextAssembler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Re-ranks <paramref name="chunks"/> by relevance score and assembles a context string
    /// capped at <see cref="MaxContextTokens"/> tokens.
    /// </summary>
    /// <param name="chunks">
    /// Raw cosine-similarity results from <see cref="IDocumentSearchService.SearchByDocumentAsync"/>.
    /// Already filtered to similarity ≥ 0.7 (AIR-R02).
    /// </param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><c>Context</c>: assembled numbered context string for the GPT-4 prompt.</item>
    ///   <item><c>UsedChunks</c>: the chunks included (in ranked order) for source-citation mapping.</item>
    /// </list>
    /// </returns>
    public (string Context, IReadOnlyList<ChunkSearchResultDto> UsedChunks) Assemble(
        IReadOnlyList<ChunkSearchResultDto> chunks)
    {
        if (chunks.Count == 0)
            return (string.Empty, Array.Empty<ChunkSearchResultDto>());

        // AIR-R03: Re-rank by cosine_similarity × normalised token density
        var ranked = chunks
            .OrderByDescending(c => c.Similarity * (c.TokenCount / TokenNormFactor))
            .ToList();

        var sb          = new StringBuilder();
        var used        = new List<ChunkSearchResultDto>(ranked.Count);
        int totalTokens = 0;
        int citation    = 1;

        foreach (var chunk in ranked)
        {
            if (totalTokens + chunk.TokenCount > MaxContextTokens)
                continue;   // skip over-budget chunks; don't break — a smaller chunk may still fit

            sb.AppendLine($"[{citation++}] {chunk.ChunkText}");
            totalTokens += chunk.TokenCount;
            used.Add(chunk);
        }

        _logger.LogDebug(
            "ContextAssembler: assembled {ChunkCount} chunk(s), {TokenCount} token(s) from {InputCount} candidates.",
            used.Count, totalTokens, ranked.Count);

        return (sb.ToString().TrimEnd(), used);
    }

    /// <summary>
    /// Builds a plain-text fact context block for the code-suggestion GPT prompt (US_023).
    ///
    /// Selects the top-<c>20</c> facts by <c>ConfidenceScore</c> descending, formats each
    /// as a numbered entry with its fact ID so the GPT response can reference
    /// <c>evidenceFactIds</c> back to source facts.
    ///
    /// Token budget uses an estimate of ~4 characters per token.
    /// </summary>
    /// <param name="facts">
    /// Decrypted fact DTOs for a patient — never contain ciphertext at call time.
    /// </param>
    /// <param name="maxTokens">Token budget ceiling for this context block (default 3000).</param>
    /// <returns>Formatted context string to inject into the <c>{{PATIENT_FACTS_CONTEXT}}</c> placeholder.</returns>
    public string BuildCodeContext(IEnumerable<FactForAssemblyDto> facts, int maxTokens = MaxContextTokens)
    {
        const int MaxFacts         = 20;
        const float CharsPerToken  = 4f;   // ~4 chars/token estimate (GPT tokeniser approximation)

        var ranked = facts
            .OrderByDescending(f => f.ConfidenceScore)
            .Take(MaxFacts)
            .ToList();

        if (ranked.Count == 0)
            return string.Empty;

        var sb          = new StringBuilder();
        int totalChars  = 0;
        int budgetChars = (int)(maxTokens * CharsPerToken);
        int index       = 1;

        foreach (var fact in ranked)
        {
            var line = $"[{index}] factId={fact.Id} type={fact.FactType} confidence={fact.ConfidenceScore:F2} value={fact.PlainTextValue}";
            if (totalChars + line.Length > budgetChars)
            {
                _logger.LogDebug(
                    "BuildCodeContext: truncated at fact {Index}/{Total} to stay within {Budget}-token budget.",
                    index - 1, ranked.Count, maxTokens);
                break;
            }

            sb.AppendLine(line);
            totalChars += line.Length;
            index++;
        }

        _logger.LogDebug(
            "BuildCodeContext: included {Count} fact(s), ~{Chars} chars (~{Tokens} tokens).",
            index - 1, totalChars, totalChars / CharsPerToken);

        return sb.ToString().TrimEnd();
    }
}
