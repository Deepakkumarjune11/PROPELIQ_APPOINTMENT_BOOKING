namespace ClinicalIntelligence.Application.AI;

/// <summary>
/// Custom AI gateway for embedding generation with token budget enforcement,
/// Redis caching, circuit breaking, and audit logging (Decision 3).
///
/// All embedding API calls MUST go through this interface — never call Azure OpenAI directly
/// from job or service code.
///
/// AI requirements:
/// - AIR-O01: Token budget ≤ 8,000 tokens per request.
/// - AIR-O02: Circuit breaker opens after 5 consecutive failures; fallback = <c>ManualReview</c>.
/// - AIR-O04: Cache embeddings in Redis keyed by SHA256(chunk_text), TTL = 7 days.
/// - AIR-S03: Log every call (model, chunk count, document ID) — no PII/PHI in log payload.
/// </summary>
public interface IAiGateway
{
    /// <summary>
    /// Generates 1536-dimensional embedding vectors for up to 15 text inputs (AIR-O01 batch limit).
    /// Cache-first: Redis hit returns immediately without calling Azure OpenAI.
    /// </summary>
    /// <param name="inputs">Chunk texts to embed. Length MUST be ≤ 15.</param>
    /// <param name="documentId">Used in AIR-S03 audit log entries.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Vectors in the same order as <paramref name="inputs"/>.</returns>
    /// <exception cref="AI.Exceptions.TokenBudgetExceededException">
    /// Thrown when estimated token count exceeds 8,000 (AIR-O01).
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="inputs"/> has more than 15 items.</exception>
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        Guid                  documentId,
        CancellationToken     ct = default);

    /// <summary>
    /// Returns <c>true</c> when the Polly circuit breaker is open (AIR-O02).
    /// Callers MUST check this before attempting embedding generation and route to
    /// <c>ManualReview</c> when <c>true</c>.
    /// </summary>
    bool IsCircuitOpen { get; }

    /// <summary>
    /// Executes a chat completion with managed identity auth (TR-006), output token budget
    /// enforcement (AIR-O01 / AC-2), context-appropriate system prompt prepending,
    /// exponential-backoff retry policy on 429/503 (AC-3), and structured observability
    /// logging (AC-4) — zero PHI in log output.
    ///
    /// Prompt injection is evaluated by <see cref="Sanitization.IPromptSanitizer"/> before
    /// the API call (US_029/AIR-S04). Blocked messages throw
    /// <see cref="Sanitization.PromptInjectionBlockedException"/>.
    /// </summary>
    /// <param name="featureContext">
    /// Feature context key used to select the system prompt from
    /// <see cref="AzureOpenAiOptions.SystemPrompts"/>, e.g. <c>"FactExtraction"</c>.
    /// </param>
    /// <param name="systemPrompt">Caller-supplied system message appended AFTER the context prompt.</param>
    /// <param name="userMessage">User / document content to complete against. MUST NOT contain raw API keys or credentials.</param>
    /// <param name="correlationId">Opaque ID written to the observability log for tracing — NOT included in the prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="GatewayResponse"/> containing the model output and token usage metadata.
    /// <see cref="GatewayResponse.IsTruncated"/> is <c>true</c> when <c>FinishReason = Length</c>.
    /// </returns>
    Task<GatewayResponse> ChatCompletionAsync(
        string            featureContext,
        string            systemPrompt,
        string            userMessage,
        Guid              correlationId,
        CancellationToken ct = default);
}
