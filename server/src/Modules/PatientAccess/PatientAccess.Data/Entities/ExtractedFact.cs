using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

public class ExtractedFact
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public FactType FactType { get; set; }

    /// <summary>
    /// PHI column — extracted clinical text (e.g., "Blood Pressure: 130/85"). Encrypted at rest per DR-015.
    /// </summary>
    public string FactText { get; set; } = string.Empty;

    /// <summary>
    /// AI extraction confidence score 0.0–1.0; facts below AIR-007 70% threshold
    /// require manual staff review.
    /// </summary>
    public float ConfidenceScore { get; set; }

    /// <summary>
    /// Character offset in the source document where this fact was extracted.
    /// Used for character-level tracing per AIR-006.
    /// </summary>
    public int SourceCharOffset { get; set; }

    /// <summary>
    /// Length in characters of the extracted fact in the source document.
    /// Used for character-level tracing per AIR-006.
    /// </summary>
    public int SourceCharLength { get; set; }

    public DateTime ExtractedAt { get; set; }

    // Navigation
    public ClinicalDocument Document { get; set; } = null!;
}
