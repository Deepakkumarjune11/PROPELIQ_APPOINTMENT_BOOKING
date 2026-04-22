using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

public class ExtractedFact
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public FactType FactType { get; set; }

    /// <summary>
    /// PHI column — extracted clinical text (e.g., "Blood Pressure: 130/85").
    /// Stored as ciphertext produced by .NET Data Protection API (DR-015).
    /// Column type is <c>text</c> (unbounded) to accommodate arbitrary ciphertext length.
    /// </summary>
    public string FactText { get; set; } = string.Empty;

    /// <summary>
    /// AI extraction confidence score 0.0–1.0; facts below AIR-007 70% threshold
    /// require manual staff review.
    /// </summary>
    public float ConfidenceScore { get; set; }

    /// <summary>
    /// Character offset in the source document where this fact was extracted.
    /// Nullable — a fact without a resolved citation position is valid (AIR-006).
    /// </summary>
    public int? SourceCharOffset { get; set; }

    /// <summary>
    /// Length in characters of the extracted fact in the source document.
    /// Nullable — a fact without a resolved citation span is valid (AIR-006).
    /// </summary>
    public int? SourceCharLength { get; set; }

    public DateTimeOffset ExtractedAt { get; set; }

    /// <summary>
    /// Soft-delete flag per DR-017. Facts are never hard-deleted so the audit trail
    /// is preserved even when a document is re-processed (idempotent re-run support).
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp of the soft-delete operation. Null when <see cref="IsDeleted"/> is false.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation
    public ClinicalDocument Document { get; set; } = null!;

    /// <summary>
    /// Marks this fact as soft-deleted (DR-017). Idempotent — calling multiple times is safe.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
