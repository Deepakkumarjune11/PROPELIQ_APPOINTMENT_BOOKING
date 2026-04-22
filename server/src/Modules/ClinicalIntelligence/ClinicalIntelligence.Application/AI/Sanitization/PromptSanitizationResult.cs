namespace ClinicalIntelligence.Application.AI.Sanitization;

/// <summary>
/// Disposition assigned to a user message by <see cref="IPromptSanitizer"/>.
/// </summary>
public enum SanitizationVerdict
{
    /// <summary>No injection patterns matched — forward to the model normally.</summary>
    Safe,

    /// <summary>
    /// Matches a review pattern. Log a warning and substitute the normalised input,
    /// but do not block the request.
    /// </summary>
    FlaggedForReview,

    /// <summary>
    /// Matches a block pattern. Reject the request immediately and throw
    /// <see cref="PromptInjectionBlockedException"/>.
    /// </summary>
    Blocked,
}

/// <summary>
/// Result returned by <see cref="IPromptSanitizer.Evaluate"/>.
/// </summary>
/// <param name="Verdict">Disposition of the evaluated input.</param>
/// <param name="MatchedPatternId">ID of the first matched pattern, or <c>null</c> when <c>Safe</c>.</param>
/// <param name="NormalizedInput">Unicode-NFC, URL-decoded, HTML-decoded form of the original input.</param>
public sealed record PromptSanitizationResult(
    SanitizationVerdict Verdict,
    string?             MatchedPatternId,
    string              NormalizedInput);
