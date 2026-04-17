using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

/// <summary>
/// Immutable append-only compliance record per DR-008.
/// Update and delete operations are blocked by an EF Core SaveChangesInterceptor.
/// No navigation properties — actor and target are referenced via scalar IDs to avoid
/// accidental entity tracking and cascade deletes.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the Staff or Admin principal performing the action. System actions use Guid.Empty.
    /// </summary>
    public Guid ActorId { get; set; }

    public AuditActorType ActorType { get; set; }
    public AuditActionType ActionType { get; set; }

    /// <summary>
    /// Polymorphic target identifier (Patient.Id, Appointment.Id, etc.).
    /// Entity type is identified by the ActionType.
    /// </summary>
    public Guid TargetEntityId { get; set; }

    /// <summary>
    /// Optional detailed metadata describing the action (e.g., before/after field values).
    /// Stored as JSONB; application layer deserialises to the appropriate audit detail shape.
    /// </summary>
    public string? Details { get; set; }

    public DateTime OccurredAt { get; set; }
}
