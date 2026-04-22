namespace ClinicalIntelligence.Application.AI;

/// <summary>
/// Structured result from <see cref="IAiGateway.ChatCompletionAsync"/> (US_030, AC-2, AC-4).
///
/// <see cref="IsTruncated"/> is set when Azure OpenAI returned <c>FinishReason = Length</c>,
/// indicating the output token budget (<c>MaxOutputTokenCount</c>) was reached mid-response.
/// Callers MUST check <see cref="IsTruncated"/> and handle partial content appropriately.
///
/// No prompt or response content is logged — only metadata fields (AC-4 / TR-006 PHI protection).
/// </summary>
/// <param name="Content">Raw response text from the model. May be partial when <see cref="IsTruncated"/> is true.</param>
/// <param name="InputTokens">Reported input (prompt) token count from the Azure OpenAI usage object.</param>
/// <param name="OutputTokens">Reported output (completion) token count from the Azure OpenAI usage object.</param>
/// <param name="IsTruncated">True when <c>FinishReason == Length</c>; false on normal completion.</param>
/// <param name="FeatureContext">Feature context string used to select the system prompt, e.g. <c>"FactExtraction"</c>.</param>
public sealed record GatewayResponse(
    string Content,
    int    InputTokens,
    int    OutputTokens,
    bool   IsTruncated,
    string FeatureContext)
{
    /// <summary>Factory for a normal (non-truncated) completion.</summary>
    public static GatewayResponse Success(
        string content, int inputTokens, int outputTokens, string featureContext)
        => new(content, inputTokens, outputTokens, IsTruncated: false, featureContext);

    /// <summary>Factory for a truncated completion (<c>FinishReason = Length</c>).</summary>
    public static GatewayResponse Truncated(
        string content, int inputTokens, int outputTokens, string featureContext)
        => new(content, inputTokens, outputTokens, IsTruncated: true, featureContext);
}
