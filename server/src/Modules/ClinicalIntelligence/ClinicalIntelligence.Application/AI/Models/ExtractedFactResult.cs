namespace ClinicalIntelligence.Application.AI.Models;

/// <summary>
/// Represents a single clinical fact extracted from a document chunk by GPT-4 Turbo.
/// Mirrors the JSON output schema defined in the <c>clinical-fact-extraction.md</c>
/// prompt template (AIR-001, AIR-006).
///
/// Deserialized from the GPT-4 JSON response and validated against
/// <c>ExtractedFactSchema</c> before persistence (AIR-Q04).
/// </summary>
/// <param name="FactType">
/// Clinical category: <c>vitals</c>, <c>medications</c>, <c>history</c>,
/// <c>diagnoses</c>, or <c>procedures</c>.
/// </param>
/// <param name="Value">
/// The extracted text value. PHI — encrypted at persistence time;
/// stored in plain text only within the job execution context.
/// </param>
/// <param name="ConfidenceScore">
/// Model confidence in range <c>[0.0, 1.0]</c>. Facts below 0.70 are flagged
/// for staff manual review per AC-4 (US_020).
/// </param>
/// <param name="SourceCharOffset">
/// Zero-based character offset within the assembled context string where the
/// source text begins (AIR-006 character-level citation).
/// </param>
/// <param name="SourceCharLength">
/// Number of characters in the source span starting at <see cref="SourceCharOffset"/>
/// (AIR-006).
/// </param>
public record ExtractedFactResult(
    string FactType,
    string Value,
    float  ConfidenceScore,
    int    SourceCharOffset,
    int    SourceCharLength);
