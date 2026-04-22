namespace ClinicalIntelligence.Application.AI.Sanitization;

/// <summary>
/// Evaluates raw user input through a multi-layer injection-detection pipeline
/// before it reaches Azure OpenAI (AIR-S04, OWASP A03).
///
/// Implementations MUST be thread-safe — registered as singleton in DI.
/// </summary>
public interface IPromptSanitizer
{
    /// <summary>
    /// Normalises <paramref name="input"/> and checks it against the configured
    /// block and review pattern sets.
    /// </summary>
    /// <param name="input">Raw user message text. Must not be null.</param>
    /// <returns>
    /// A <see cref="PromptSanitizationResult"/> whose <see cref="PromptSanitizationResult.Verdict"/>
    /// indicates whether the input is <c>Safe</c>, <c>FlaggedForReview</c>, or <c>Blocked</c>.
    /// </returns>
    PromptSanitizationResult Evaluate(string input);
}
