using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

public class PatientView360
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }

    /// <summary>
    /// De-duplicated consolidated clinical summary. PHI column — encrypted at rest per DR-015.
    /// Stored as JSONB; application layer deserialises to the appropriate view model shape.
    /// </summary>
    public string ConsolidatedFacts { get; set; } = string.Empty;

    /// <summary>
    /// Array of conflict flag descriptions detected across aggregated data sources (AIR-004).
    /// Stored as PostgreSQL text[] column.
    /// </summary>
    public string[] ConflictFlags { get; set; } = [];

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;

    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Optimistic concurrency token per DR-018. Incremented on every update;
    /// EF Core raises DbUpdateConcurrencyException when a stale version is saved.
    /// </summary>
    public int Version { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
}
