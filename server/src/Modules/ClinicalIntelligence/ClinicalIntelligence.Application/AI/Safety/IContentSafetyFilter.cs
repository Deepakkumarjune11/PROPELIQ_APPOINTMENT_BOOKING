namespace ClinicalIntelligence.Application.AI.Safety;

/// <summary>
/// Three-layer output safety filter applied to every AI response before it leaves the gateway
/// (US_031, AIR-O03, AIR-S04).
///
/// Layers (in evaluation order):
/// 1. PHI leakage detection — regex patterns for SSN, phone, email, DOB formats.
/// 2. Harmful content detection — keyword blocklist for self-harm, substance misuse, explicit violence.
/// 3. Medical advice hallucination — prescriptive-phrasing patterns, excluded for
///    feature contexts listed in <c>ContentSafetyOptions.ExcludedFeatureContextsForMedicalAdvice</c>.
///
/// Returns <see langword="null"/> when the content is safe; returns a
/// <see cref="ContentSafetyViolation"/> on the first matched pattern.
///
/// Thread-safety: stateless; safe for transient DI lifetime.
/// </summary>
public interface IContentSafetyFilter
{
    /// <summary>
    /// Evaluates <paramref name="content"/> against all configured safety patterns.
    /// </summary>
    /// <param name="content">AI-generated response text to evaluate.</param>
    /// <param name="featureContext">
    /// Feature context of the originating request (e.g. <c>"FactExtraction"</c>).
    /// Used to skip medical-advice patterns for contexts where directive phrasing is expected.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="null"/> when safe; a <see cref="ContentSafetyViolation"/> on the first violation.
    /// </returns>
    Task<ContentSafetyViolation?> EvaluateAsync(
        string            content,
        string            featureContext,
        CancellationToken ct = default);
}
