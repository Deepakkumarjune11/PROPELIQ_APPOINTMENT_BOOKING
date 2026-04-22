namespace ClinicalIntelligence.Application.AI.Safety;

/// <summary>
/// Discriminates the category of a content safety violation (US_031, AIR-O03).
/// </summary>
public enum SafetyViolationType
{
    /// <summary>Protected Health Information (PHI) detected in the AI response (HIPAA Safe Harbor).</summary>
    PhiLeakage,

    /// <summary>Harmful content detected — self-harm, substance misuse, or explicit violence.</summary>
    HarmfulContent,

    /// <summary>
    /// Medical advice hallucination — prescriptive diagnostic or treatment directive from the model,
    /// not applicable to <c>ConversationalIntake</c> feature context.
    /// </summary>
    MedicalAdviceHallucination,
}

/// <summary>
/// Immutable result returned by <see cref="IContentSafetyFilter"/> when a violation is detected
/// (US_031, AC-4, AIR-O03).
///
/// <see cref="ResponseHash"/> carries the SHA256 hex digest of the blocked content — NEVER the raw
/// content string — ensuring the audit record contains no PHI (OWASP A04, AIR-S03).
/// </summary>
/// <param name="ViolationType">Category of the detected violation.</param>
/// <param name="PatternId">Identifier of the matched safety pattern entry (e.g., <c>"PHI-001"</c>).</param>
/// <param name="ResponseHash">
/// SHA256 hex digest of the AI response content.
/// Safe to log and persist — no PHI is present in this field.
/// </param>
public sealed record ContentSafetyViolation(
    SafetyViolationType ViolationType,
    string              PatternId,
    string              ResponseHash);
