using ClinicalIntelligence.Application.AI.Models;

namespace ClinicalIntelligence.Application.AI;

/// <summary>
/// Extends <see cref="IAiGateway"/> with the legacy GPT-4 Turbo chat completion shim
/// for backward compatibility with US_020 callers (FactExtractionJob, CodeSuggestionJob).
///
/// US_030 hardens the gateway via <see cref="IAiGateway.ChatCompletionAsync(string,string,string,Guid,CancellationToken)"/>
/// which is the preferred call path for new code. This interface is retained so existing
/// callers that inject <c>IChatCompletionGateway</c> continue to compile unchanged.
///
/// AI requirements:
/// - AIR-O01: Token budget ≤ 8,000 (system + user message combined).
/// - AIR-O02: Circuit breaker open → callers route to <c>ManualReview</c>.
/// - AIR-S03: Every call logged (model, approx token count, document_id) — no PHI.
/// - AIR-S04: Azure OpenAI Content Safety filter applied at service level.
/// </summary>
public interface IChatCompletionGateway : IAiGateway
{
    /// <summary>
    /// Backward-compat shim — delegates to
    /// <see cref="IAiGateway.ChatCompletionAsync(string,string,string,Guid,CancellationToken)"/>
    /// with <c>featureContext = "FactExtraction"</c> and returns <see cref="GatewayResponse"/>.
    ///
    /// Existing callers use <c>.Content</c> on the returned value to get the raw JSON string.
    /// </summary>
    /// <param name="systemPrompt">System message defining the extraction schema.</param>
    /// <param name="userMessage">Assembled context (≤ 3,000 tokens per AIR-R04).</param>
    /// <param name="documentId">Used in AIR-S03 audit log — NOT included in the prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="GatewayResponse"/> — use <c>.Content</c> for the raw JSON string.</returns>
    Task<GatewayResponse> ChatCompletionAsync(
        string            systemPrompt,
        string            userMessage,
        Guid              documentId,
        CancellationToken ct = default);
}

