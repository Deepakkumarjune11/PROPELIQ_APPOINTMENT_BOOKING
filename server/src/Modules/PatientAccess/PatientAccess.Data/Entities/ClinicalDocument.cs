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
    /// Must be encrypted at rest per DR-015.
    /// </summary>
    public string FileReference { get; set; } = string.Empty;

    public ExtractionStatus ExtractionStatus { get; set; } = ExtractionStatus.Pending;
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
    public ICollection<ExtractedFact> ExtractedFacts { get; set; } = [];
}
