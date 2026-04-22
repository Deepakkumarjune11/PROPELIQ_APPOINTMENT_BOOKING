namespace ClinicalIntelligence.Application.AI.Sanitization;

/// <summary>
/// Thrown by <see cref="AzureOpenAiGateway"/> when <see cref="IPromptSanitizer.Evaluate"/>
/// returns <see cref="SanitizationVerdict.Blocked"/> (AIR-S04, OWASP A03).
///
/// The message is intentionally generic — no pattern details are exposed to callers
/// to prevent adversarial tuning of injection attempts.
/// </summary>
public sealed class PromptInjectionBlockedException : Exception
{
    /// <summary>Stable identifier of the matched block pattern (e.g. <c>INJ-001</c>).</summary>
    public string? PatternId { get; }

    public PromptInjectionBlockedException(string? patternId)
        : base("Request blocked by content policy.")
    {
        PatternId = patternId;
    }
}
