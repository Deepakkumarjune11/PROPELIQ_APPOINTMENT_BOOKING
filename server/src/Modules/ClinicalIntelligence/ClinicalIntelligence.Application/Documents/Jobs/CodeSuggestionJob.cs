using System.Text.Json;
using ClinicalIntelligence.Application.AI;
using ClinicalIntelligence.Application.Documents.Models;
using ClinicalIntelligence.Application.Documents.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace ClinicalIntelligence.Application.Documents.Jobs;

/// <summary>
/// Hangfire background job that generates AI-assisted clinical code suggestions
/// (ICD-10 and CPT) from a patient's extracted facts (US_023/task_002).
///
/// Pipeline:
/// 1. <b>Circuit-open guard</b> — abort immediately if Polly circuit is open (AIR-O02).
/// 2. <b>Load facts</b> — retrieve all non-deleted extracted facts for the patient via
///    <see cref="ICodeSuggestionPersistenceService"/> (decrypted by the Data layer).
/// 3. <b>Build context</b> — assemble top-20 facts by confidence score into a prompt
///    context string via <see cref="ContextAssembler.BuildCodeContext"/> (AIR-R04).
/// 4. <b>GPT call</b> — send to <see cref="IChatCompletionGateway.ChatCompletionAsync"/>
///    with the code-suggestion prompt template (AIR-O01, AIR-S03).
/// 5. <b>Schema validate</b> — parse and validate JSON array; retry once on failure (AIR-Q04).
/// 6. <b>Hallucination guard</b> — reject any suggestion with zero evidence facts or
///    ConfidenceScore &lt; 0.50 (AIR-Q01).
/// 7. <b>Persist</b> — delegate to <see cref="ICodeSuggestionPersistenceService.PersistAsync"/>
///    for soft-delete + insert; emit AuditLog without PHI (AIR-S03).
///
/// Queue: <c>code-suggestion</c>.
/// Retry: 2 attempts (fact loading is cheap; hallucinatory re-runs cost tokens).
/// Concurrency: one job per patient at a time (300-second mutex timeout).
/// </summary>
[Queue("code-suggestion")]
[AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 30, 120 })]
[DisableConcurrentExecution(timeoutInSeconds: 300)]
public sealed class CodeSuggestionJob
{
    private const int MaxContextTokens = 3_000;

    private static readonly string SystemPrompt = LoadPromptFromTemplate();

    private readonly ICodeSuggestionPersistenceService  _persistence;
    private readonly IChatCompletionGateway             _chatGateway;
    private readonly IAiGateway                         _aiGateway;
    private readonly ContextAssembler                   _contextAssembler;
    private readonly ILogger<CodeSuggestionJob>         _logger;

    public CodeSuggestionJob(
        ICodeSuggestionPersistenceService   persistence,
        IChatCompletionGateway              chatGateway,
        IAiGateway                          aiGateway,
        ContextAssembler                    contextAssembler,
        ILogger<CodeSuggestionJob>          logger)
    {
        _persistence      = persistence;
        _chatGateway      = chatGateway;
        _aiGateway        = aiGateway;
        _contextAssembler = contextAssembler;
        _logger           = logger;
    }

    /// <summary>
    /// Entry point invoked by Hangfire after <see cref="ConflictDetectionJob"/> completes.
    /// </summary>
    /// <param name="patientId">The patient for whom code suggestions are generated.</param>
    /// <param name="ct">Hangfire-supplied cancellation token.</param>
    public async Task ExecuteAsync(Guid patientId, CancellationToken ct)
    {
        _logger.LogInformation(
            "CodeSuggestionJob: starting for patient {PatientId}.", patientId);

        // Step 1: Circuit-open guard (AIR-O02)
        if (_aiGateway.IsCircuitOpen)
        {
            _logger.LogWarning(
                "CodeSuggestionJob: AI circuit open for patient {PatientId}; aborting.", patientId);
            return;
        }

        try
        {
            // Step 2: Load decrypted facts from Data layer
            var facts = await _persistence.GetPlainTextFactsAsync(patientId, ct);

            if (facts.Count == 0)
            {
                _logger.LogWarning(
                    "CodeSuggestionJob: no facts available for patient {PatientId}; skipping.", patientId);
                return;
            }

            // Step 3: Build context (top-20 facts by confidence, ≤3000 token budget)
            var context = _contextAssembler.BuildCodeContext(facts, MaxContextTokens);

            // Step 4: GPT call — use Guid.Empty as documentId (patient-level job; AIR-S03)
            var userMessage  = context;
            var responseJson = (await _chatGateway.ChatCompletionAsync(
                SystemPrompt, userMessage, Guid.Empty, ct)).Content;

            // Step 5: Schema validate; retry once on failure (AIR-Q04)
            if (!TryParseSuggestions(responseJson, out var suggestions))
            {
                _logger.LogWarning(
                    "CodeSuggestionJob: schema validation failed on first attempt for patient {PatientId}; retrying.",
                    patientId);

                responseJson = (await _chatGateway.ChatCompletionAsync(
                    SystemPrompt, userMessage, Guid.Empty, ct)).Content;

                if (!TryParseSuggestions(responseJson, out suggestions))
                {
                    _logger.LogError(
                        "CodeSuggestionJob: schema validation failed twice for patient {PatientId}; aborting.",
                        patientId);
                    return;
                }
            }

            // Step 6: Hallucination guard — reject zero-evidence and low-confidence results (AIR-Q01)
            var validSuggestions = suggestions
                .Where(s => s.EvidenceFactIds.Count > 0 && s.ConfidenceScore >= 0.50f)
                .ToList();

            int rejected = suggestions.Count - validSuggestions.Count;
            if (rejected > 0)
                _logger.LogInformation(
                    "CodeSuggestionJob: hallucination guard rejected {Count} suggestion(s) for patient {PatientId}.",
                    rejected, patientId);

            // Step 7: Persist
            await _persistence.PersistAsync(patientId, validSuggestions, ct);

            _logger.LogInformation(
                "CodeSuggestionJob: persisted {Count} suggestion(s) for patient {PatientId}.",
                validSuggestions.Count, patientId);
        }
        catch (BrokenCircuitException)
        {
            // Polly circuit tripped mid-execution (AIR-O02) — do not rethrow; Hangfire must not retry
            _logger.LogWarning(
                "CodeSuggestionJob: circuit breaker opened mid-execution for patient {PatientId}; aborting.",
                patientId);
        }
    }

    // ── Schema validation ─────────────────────────────────────────────────────

    private static bool TryParseSuggestions(
        string json,
        out IReadOnlyList<CodeSuggestionResult> suggestions)
    {
        suggestions = Array.Empty<CodeSuggestionResult>();
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<List<CodeSuggestionGptDto>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null)
                return false;

            var results = new List<CodeSuggestionResult>(parsed.Count);

            foreach (var dto in parsed)
            {
                if (string.IsNullOrWhiteSpace(dto.CodeType)
                    || string.IsNullOrWhiteSpace(dto.Code)
                    || string.IsNullOrWhiteSpace(dto.Description)
                    || dto.ConfidenceScore is < 0f or > 1f)
                    return false;

                results.Add(new CodeSuggestionResult(
                    dto.CodeType,
                    dto.Code,
                    dto.Description,
                    dto.ConfidenceScore,
                    dto.EvidenceFactIds ?? []));
            }

            suggestions = results;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // ── Prompt loading ────────────────────────────────────────────────────────

    private static string LoadPromptFromTemplate()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".propel")))
                dir = dir.Parent;

            if (dir is null) return GetFallbackPrompt();

            var promptPath = Path.Combine(
                dir.FullName, ".propel", "context", "prompts", "code-suggestion.md");

            if (!File.Exists(promptPath)) return GetFallbackPrompt();

            var content = File.ReadAllText(promptPath).Trim();
            return string.IsNullOrWhiteSpace(content) ? GetFallbackPrompt() : content;
        }
        catch
        {
            return GetFallbackPrompt();
        }
    }

    private static string GetFallbackPrompt()
        => "You are a clinical coding assistant. Given a list of patient facts, return a JSON array of " +
           "code suggestions: [{\"codeType\":\"ICD-10\"|\"CPT\",\"code\":\"<code>\",\"description\":\"<desc>\"," +
           "\"confidenceScore\":0.0,\"evidenceFactIds\":[\"<uuid>\"]}]. Return [] if no codes are supported.";

    // ── Inner DTO for JSON deserialisation ────────────────────────────────────

    private sealed class CodeSuggestionGptDto
    {
        public string CodeType { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public float ConfidenceScore { get; init; }
        public List<Guid>? EvidenceFactIds { get; init; }
    }
}
