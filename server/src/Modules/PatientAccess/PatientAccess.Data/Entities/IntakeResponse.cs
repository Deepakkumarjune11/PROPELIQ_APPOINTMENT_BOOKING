using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

public class IntakeResponse
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public IntakeMode Mode { get; set; }

    /// <summary>
    /// Raw JSONB payload. PHI column — must be encrypted at rest per DR-015.
    /// Stored as a serialised JSON string; the application layer deserialises
    /// to the appropriate shape.
    /// </summary>
    public string Answers { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
}
