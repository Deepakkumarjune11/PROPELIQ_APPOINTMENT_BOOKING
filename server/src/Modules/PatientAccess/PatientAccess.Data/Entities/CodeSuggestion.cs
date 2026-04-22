using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

public class CodeSuggestion
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public CodeType CodeType { get; set; }
    public string CodeValue { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the code (e.g., "Essential hypertension") per DR-007.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// AI extraction confidence score 0.0–1.0 (AIR-Q01).
    /// Zero-evidence codes are clamped below 0.50 as hallucination guard.
    /// </summary>
    public float ConfidenceScore { get; set; }

    /// <summary>
    /// Denormalised array of ExtractedFact IDs providing evidence for this suggestion (DR-007).
    /// Stored as PostgreSQL uuid[] column to avoid a join table on a read-heavy path.
    /// </summary>
    public Guid[] EvidenceFactIds { get; set; } = [];

    public bool StaffReviewed { get; set; }

    /// <summary>
    /// Outcome recorded by the clinical reviewer ("accepted" | "rejected").
    /// Nullable — only populated when StaffReviewed = true.
    /// </summary>
    public string? ReviewOutcome { get; set; }

    /// <summary>
    /// Staff justification text; required when ReviewOutcome = "rejected" (UC-005).
    /// </summary>
    public string? ReviewJustification { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Nullable — only set when StaffReviewed transitions to true.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Soft-delete flag (DR-017). Existing rows are soft-deleted on re-generation
    /// to support idempotent <c>CodeSuggestionJob</c> re-runs.
    /// </summary>
    public bool IsDeleted { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;

    /// <summary>
    /// Records a staff review decision on this code suggestion (UC-005, AC-7).
    ///
    /// Encapsulates the state transition so no external code can set individual
    /// review fields in an inconsistent state (e.g., StaffReviewed without ReviewedAt).
    ///
    /// Business rules:
    /// <list type="bullet">
    ///   <item>ReviewJustification is only stored when outcome is "rejected".</item>
    ///   <item>ReviewedAt is always set to UtcNow on first call.</item>
    /// </list>
    /// </summary>
    /// <param name="outcome">"accepted" or "rejected" (lower-case).</param>
    /// <param name="justification">Required when outcome is "rejected"; otherwise ignored.</param>
    public void Review(string outcome, string? justification)
    {
        StaffReviewed       = true;
        ReviewOutcome       = outcome;
        ReviewJustification = string.Equals(outcome, "rejected", StringComparison.OrdinalIgnoreCase)
            ? justification
            : null;
        ReviewedAt          = DateTime.UtcNow;
    }
}
