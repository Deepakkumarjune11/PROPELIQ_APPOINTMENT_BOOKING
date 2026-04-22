namespace ClinicalIntelligence.Application.Documents.Models;

/// <summary>
/// Represents a single code suggestion parsed from the GPT response payload (US_023).
/// Produced by <c>CodeSuggestionJob</c> and persisted via <c>ICodeSuggestionPersistenceService</c>.
/// </summary>
public record CodeSuggestionResult(
    /// <summary>"ICD-10" or "CPT" as returned by the GPT response.</summary>
    string CodeType,

    /// <summary>The exact alphanumeric clinical code value, e.g. "E11.9" or "99213".</summary>
    string Code,

    /// <summary>Official short description of the code, max 100 characters.</summary>
    string Description,

    /// <summary>AI confidence score in range 0.0–1.0 (AIR-Q01).</summary>
    float ConfidenceScore,

    /// <summary>
    /// Fact IDs from <c>ExtractedFact</c> that provide evidence for this code.
    /// Codes with zero evidence facts are rejected as hallucinations (AIR-Q01).
    /// </summary>
    IReadOnlyList<Guid> EvidenceFactIds);
