using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

public class ClinicalDocument
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }

    /// <summary>
    /// Optional FK. Null for pre-visit historical documents uploaded without
    /// an associated encounter/appointment context.
    /// </summary>
    public Guid? EncounterId { get; set; }

    /// <summary>
    /// PHI column — blob storage URL or file system reference per DR-004.
    /// Encrypted at rest via .NET Data Protection API (DR-015).
    /// </summary>
    public string FileReference { get; set; } = string.Empty;

    /// <summary>Original client-provided filename for display (e.g. "discharge-summary.pdf").</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>File size in bytes — stored for display; validated against 25 MB limit (FR-010).</summary>
    public long FileSizeBytes { get; set; }

    public ExtractionStatus ExtractionStatus { get; set; } = ExtractionStatus.Pending;
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Soft-delete flag — physical file is retained for audit per DR-013.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp of soft-deletion; null while the document is active (DR-017).</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
    public ICollection<ExtractedFact> ExtractedFacts { get; set; } = [];

    // ── Domain methods ────────────────────────────────────────────────────────

    /// <summary>Advances the AI-extraction pipeline status.</summary>
    public void SetExtractionStatus(ExtractionStatus status)
    {
        ExtractionStatus = status;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the document as soft-deleted per DR-017 (GDPR-style deletion).
    /// Physical file is retained for audit; the record is hidden by the global query filter.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
