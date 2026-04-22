using System.Text.Json;
using ClinicalIntelligence.Application.AI;
using ClinicalIntelligence.Application.AI.Exceptions;
using ClinicalIntelligence.Application.AI.Models;
using ClinicalIntelligence.Application.Documents.Services;
using ClinicalIntelligence.Application.Infrastructure;
using Hangfire;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace ClinicalIntelligence.Application.Documents.Jobs;

/// <summary>
/// Hangfire background job that executes the full RAG extraction pipeline after
/// <see cref="EmbeddingGenerationJob"/> completes (US_020/task_001, AC-1 through AC-3).
///
/// Pipeline (6 steps):
/// 1. <b>Circuit-open guard</b> — flag <c>ManualReview</c> immediately if Polly is open (AIR-O02).
/// 2. <b>Retrieve</b> — embed extraction query, cosine similarity ≥ 0.7, top-5 chunks (AIR-R02).
///    Low-relevance guard: zero qualifying chunks → <c>ManualReview(LowRelevance)</c>; exit.
/// 3. <b>Re-rank + assemble</b> — <see cref="ContextAssembler"/> re-ranks by cosine × token density
///    and greedily fills ≤ 3,000-token context window (AIR-R03, AIR-R04).
/// 4. <b>GPT-4 Turbo call</b> — <see cref="IChatCompletionGateway.ChatCompletionAsync"/> with
///    structured JSON prompt; temperature=0; token budget ≤ 8,000 (AIR-O01, AIR-S03, AIR-S04).
/// 5. <b>Schema validate</b> — parse as <see cref="ExtractedFactResult"/> list; retry once on
///    failure; second failure → <c>ManualReview(SchemaValidationFailed)</c> (AIR-Q04).
/// 6. <b>Persist</b> — delegate to <see cref="IFactPersistenceService.PersistAsync"/> for
///    encrypted DB writes, status transition, and 360-view trigger.
///
/// Queue: <c>fact-extraction</c>. Retry policy: 3 attempts with 10 s / 60 s / 180 s delays.
/// </summary>
[Queue("fact-extraction")]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 60, 180 })]
public sealed class FactExtractionJob
{
    private const string ExtractionQuery =
        "Extract clinical facts: vitals, medications, history, diagnoses, procedures";

    // System prompt is static — read from the versioned template file at construction time.
    // Loaded via the embedded resource path relative to the assembly.
    private static readonly string SystemPrompt    = LoadPromptSection("## System Message");
    private static readonly string UserTemplate    = LoadPromptSection("## User Message Template");

    private readonly IChatCompletionGateway        _aiGateway;
    private readonly IDocumentSearchService        _searchService;
    private readonly ContextAssembler              _contextAssembler;
    private readonly IFactPersistenceService       _factPersistence;
    private readonly IClinicalDocumentRepository   _repo;
    private readonly ILogger<FactExtractionJob>    _logger;

    public FactExtractionJob(
        IChatCompletionGateway      aiGateway,
        IDocumentSearchService      searchService,
        ContextAssembler            contextAssembler,
        IFactPersistenceService     factPersistence,
        IClinicalDocumentRepository repo,
        ILogger<FactExtractionJob>  logger)
    {
        _aiGateway        = aiGateway;
        _searchService    = searchService;
        _contextAssembler = contextAssembler;
        _factPersistence  = factPersistence;
        _repo             = repo;
        _logger           = logger;
    }

    /// <summary>
    /// Entry point invoked by Hangfire after <see cref="EmbeddingGenerationJob"/> succeeds.
    /// </summary>
    /// <param name="documentId">The <c>ClinicalDocument.Id</c> to extract facts from.</param>
    /// <param name="cancellationToken">Hangfire supplies a token on graceful shutdown.</param>
    public async Task ExecuteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("FactExtractionJob starting for document {DocumentId}.", documentId);

        // Step 1: Circuit-open guard (AIR-O02)
        if (_aiGateway.IsCircuitOpen)
        {
            _logger.LogWarning(
                "FactExtractionJob: AI gateway circuit open for document {DocumentId}; flagging ManualReview.",
                documentId);
            await _repo.FlagForManualReviewAsync(documentId, "CircuitOpen", cancellationToken);
            return;
        }

        try
        {
            // Step 2: Retrieve — document-scoped cosine similarity ≥ 0.7, top-5 (AIR-R02, AIR-S02)
            var chunks = await _searchService.SearchByDocumentAsync(
                ExtractionQuery, documentId, cancellationToken);

            if (chunks.Count == 0)
            {
                _logger.LogWarning(
                    "FactExtractionJob: no qualifying chunks for document {DocumentId}; flagging ManualReview(LowRelevance).",
                    documentId);
                await _repo.FlagForManualReviewAsync(documentId, "LowRelevance", cancellationToken);
                return;
            }

            // Step 3: Re-rank + assemble ≤ 3,000-token context window (AIR-R03, AIR-R04)
            var (context, _) = _contextAssembler.Assemble(chunks);

            // Step 4: GPT-4 Turbo call — token budget ≤ 8,000 (AIR-O01)
            var userMessage  = UserTemplate.Replace("{context}", context);
            var responseJson = (await _aiGateway.ChatCompletionAsync(
                SystemPrompt, userMessage, documentId, cancellationToken)).Content;

            // Step 5: Schema validate (AIR-Q04); retry once on parse failure
            if (!TryParseFactSchema(responseJson, out var facts))
            {
                _logger.LogWarning(
                    "FactExtractionJob: schema validation failed on first attempt for document {DocumentId}; retrying.",
                    documentId);

                responseJson = (await _aiGateway.ChatCompletionAsync(
                    SystemPrompt, userMessage, documentId, cancellationToken)).Content;

                if (!TryParseFactSchema(responseJson, out facts))
                {
                    _logger.LogError(
                        "FactExtractionJob: schema validation failed twice for document {DocumentId}; flagging ManualReview.",
                        documentId);
                    await _repo.FlagForManualReviewAsync(
                        documentId, "SchemaValidationFailed", cancellationToken);
                    return;
                }
            }

            // Step 6: Delegate to persistence layer
            await _factPersistence.PersistAsync(documentId, facts, cancellationToken);

            _logger.LogInformation(
                "FactExtractionJob: extracted {FactCount} fact(s) for document {DocumentId}.",
                facts.Count, documentId);
        }
        catch (BrokenCircuitException)
        {
            // Polly circuit tripped mid-execution (AIR-O02) — do not rethrow; Hangfire must not retry
            _logger.LogWarning(
                "FactExtractionJob: circuit breaker opened mid-execution for document {DocumentId}; flagging ManualReview.",
                documentId);
            await _repo.FlagForManualReviewAsync(documentId, "CircuitOpen", cancellationToken);
        }
        catch (TokenBudgetExceededException ex)
        {
            // Context assembly produced a prompt exceeding 8,000 tokens — flag for human triage
            _logger.LogError(ex,
                "FactExtractionJob: token budget exceeded for document {DocumentId}.",
                documentId);
            await _repo.FlagForManualReviewAsync(documentId, "TokenBudgetExceeded", cancellationToken);
        }
    }

    // ── Schema validation ────────────────────────────────────────────────

    private static bool TryParseFactSchema(
        string                          json,
        out IReadOnlyList<ExtractedFactResult> facts)
    {
        facts = Array.Empty<ExtractedFactResult>();
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<List<ExtractedFactResult>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null)
                return false;

            // Validate each item has required fields (AIR-Q04)
            foreach (var fact in parsed)
            {
                if (string.IsNullOrWhiteSpace(fact.FactType)
                    || string.IsNullOrWhiteSpace(fact.Value)
                    || fact.ConfidenceScore is < 0f or > 1f)
                    return false;
            }

            facts = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // ── Prompt loading ───────────────────────────────────────────────────

    /// <summary>
    /// Reads the clinical-fact-extraction prompt template from the .propel/context/prompts/
    /// directory relative to the solution root, and extracts content after the given section header.
    /// Falls back to an embedded default when the file is not found (e.g. in unit test contexts).
    /// </summary>
    private static string LoadPromptSection(string sectionHeader)
    {
        try
        {
            // Walk up from the assembly location to find the .propel directory
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".propel")))
                dir = dir.Parent;

            if (dir is null) return GetFallbackPrompt(sectionHeader);

            var promptPath = Path.Combine(
                dir.FullName, ".propel", "context", "prompts", "clinical-fact-extraction.md");

            if (!File.Exists(promptPath)) return GetFallbackPrompt(sectionHeader);

            var lines       = File.ReadAllLines(promptPath);
            var sb          = new System.Text.StringBuilder();
            var inSection   = false;

            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("## ") && inSection) break;
                if (line.TrimStart() == sectionHeader) { inSection = true; continue; }
                if (inSection) sb.AppendLine(line);
            }

            var content = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(content) ? GetFallbackPrompt(sectionHeader) : content;
        }
        catch
        {
            return GetFallbackPrompt(sectionHeader);
        }
    }

    private static string GetFallbackPrompt(string section) => section switch
    {
        "## System Message" =>
            "You are a clinical data extraction specialist. Respond ONLY with a valid JSON array " +
            "of extracted facts: [{\"factType\":\"vitals|medications|history|diagnoses|procedures\"," +
            "\"value\":\"<text>\",\"confidenceScore\":0.0,\"sourceCharOffset\":0,\"sourceCharLength\":0}]. " +
            "Return [] if no clinical facts are found.",
        _ =>
            "Extract clinical facts from the following document context:\n\n{context}\n\n" +
            "Respond with a JSON array only.",
    };
}
