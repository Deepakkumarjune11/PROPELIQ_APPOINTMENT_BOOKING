namespace ClinicalIntelligence.Application.AI;

/// <summary>
/// Configuration POCO for the hardened Azure OpenAI gateway (US_030, AC-1, AC-2, TR-006).
/// Bound from <c>appsettings.json → "AzureOpenAi"</c>.
///
/// Auth: managed identity via <c>DefaultAzureCredential</c> — no API key required (OWASP A02).
/// </summary>
public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAiGateway";

    /// <summary>Azure OpenAI resource endpoint URI, e.g. https://my-resource.openai.azure.com/</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>GPT-4 Turbo deployment name in the Azure OpenAI resource (AC-1).</summary>
    public string InferenceDeploymentName { get; set; } = "gpt-4-turbo";

    /// <summary>text-embedding-3-small deployment name in the Azure OpenAI resource (AC-1).</summary>
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Maximum output tokens per chat completion request (AIR-O01 + AC-2).
    /// Gateway enforces <c>MaxOutputTokenCount</c> on every <c>ChatCompletionOptions</c> call.
    /// </summary>
    public int OutputTokenBudget { get; set; } = 4096;

    /// <summary>
    /// Message returned to callers when the AI service is unavailable and no cached response
    /// exists for the requested <c>featureContext</c> (US_030 / AC-5).
    /// Must be safe for display in clinical staff UI — no technical details.
    /// </summary>
    public string DegradationMessage { get; set; } =
        "AI assistance is temporarily unavailable. Please proceed manually or try again shortly.";

    /// <summary>
    /// TTL in seconds for the per-featureContext response cache used by the degradation handler
    /// (US_030 / AC-5). Default: 86400 (24 hours).
    /// After a successful GPT call the response is written to Redis under
    /// <c>ai_cache:{featureContext}</c> with this TTL.
    /// </summary>
    public int ResponseCacheTtlSeconds { get; set; } = 86_400;

    /// <summary>
    /// System prompts keyed by feature context string.
    /// Prepended to the caller's <c>systemPrompt</c> parameter before the Azure OpenAI call.
    /// </summary>
    public Dictionary<string, string> SystemPrompts { get; set; } = new()
    {
        ["FactExtraction"]       = "You are a clinical fact extraction assistant operating within a HIPAA-compliant healthcare platform. Extract structured clinical facts only. Do not infer beyond the document content.",
        ["CodeSuggestion"]       = "You are a medical coding assistant operating within a HIPAA-compliant healthcare platform. Suggest ICD-10 and CPT codes based only on documented facts provided.",
        ["ConversationalIntake"] = "You are a patient intake assistant. Ask clear, empathetic questions to gather intake information. Do not provide medical advice.",
    };

    /// <summary>
    /// Circuit breaker thresholds for the Polly v8 resilience pipeline (US_031, AIR-O02).
    /// Drives <see cref="ClinicalIntelligence.Application.AI.Availability.IAiAvailabilityState"/>
    /// via <c>OnOpened</c>/<c>OnClosed</c> callbacks.
    /// </summary>
    public AzureOpenAiCircuitBreakerOptions CircuitBreaker { get; set; } = new();
}
