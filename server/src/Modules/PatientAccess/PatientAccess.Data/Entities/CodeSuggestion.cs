using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

public class CodeSuggestion
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public CodeType CodeType { get; set; }
    public string CodeValue { get; set; } = string.Empty;

    /// <summary>
    /// Denormalised array of ExtractedFact IDs providing evidence for this suggestion (DR-007).
    /// Stored as PostgreSQL uuid[] column to avoid a join table on a read-heavy path.
    /// </summary>
    public Guid[] EvidenceFactIds { get; set; } = [];

    public bool StaffReviewed { get; set; }

    /// <summary>
    /// Outcome recorded by the clinical reviewer (e.g., "Accepted", "Rejected", "Modified").
    /// Nullable — only populated when StaffReviewed = true.
    /// </summary>
    public string? ReviewOutcome { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Nullable — only set when StaffReviewed transitions to true.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
}
