namespace ClinicalIntelligence.Application.AI.Safety;

/// <summary>
/// A single safety pattern entry — regex pattern plus audit-safe metadata (US_031, AIR-O03).
/// </summary>
/// <param name="Id">Unique pattern identifier, e.g. <c>"PHI-001"</c>. Safe to log.</param>
/// <param name="Pattern">Regular expression applied to the normalised AI response content.</param>
/// <param name="Description">Human-readable label for audit records. Must NOT contain PHI.</param>
public sealed record SafetyPatternEntry(string Id, string Pattern, string Description);

/// <summary>
/// Configuration POCO for the AI output content safety filter (US_031, AIR-O03, AIR-S04).
/// Bound from <c>appsettings.json → "ContentSafety"</c>.
///
/// Hot-reload: injected as <see cref="Microsoft.Extensions.Options.IOptionsMonitor{T}"/> in
/// <see cref="ContentSafetyFilter"/> — pattern changes take effect without application restart.
/// </summary>
public sealed class ContentSafetyOptions
{
    public const string SectionName = "ContentSafety";

    /// <summary>PHI leakage patterns (Layer 1): SSN, phone, email, DOB date formats.</summary>
    public List<SafetyPatternEntry> PhiPatterns { get; set; } = [];

    /// <summary>Harmful content patterns (Layer 2): self-harm instructions, illegal substance advice.</summary>
    public List<SafetyPatternEntry> HarmKeywords { get; set; } = [];

    /// <summary>
    /// Medical advice hallucination patterns (Layer 3): prescriptive directives ("you should take …").
    /// Not applied to feature contexts in <see cref="ExcludedFeatureContextsForMedicalAdvice"/>.
    /// </summary>
    public List<SafetyPatternEntry> MedicalAdvicePatterns { get; set; } = [];

    /// <summary>
    /// Feature contexts excluded from Layer 3 medical-advice checks.
    /// Default: <c>["ConversationalIntake"]</c> — that context legitimately uses directive phrasing.
    /// </summary>
    public List<string> ExcludedFeatureContextsForMedicalAdvice { get; set; } = ["ConversationalIntake"];

    /// <summary>
    /// Safe static message returned to callers when a violation is detected (AC-4).
    /// Never contains PHI. Shown in clinical staff UI — keep clinically appropriate.
    /// </summary>
    public string SafeResponseMessage { get; set; } =
        "I'm unable to provide that response. Please consult your clinical team or try again.";

    /// <summary>
    /// Supplemental harm keywords for multilingual support (Spanish, French, Portuguese transliterations).
    /// Appended to <see cref="HarmKeywords"/> at runtime. Configurable without redeploy.
    /// </summary>
    public List<string> AdditionalHarmKeywords { get; set; } = [];
}
