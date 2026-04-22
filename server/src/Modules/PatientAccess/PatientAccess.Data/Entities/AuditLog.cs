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
    /// </summary>
    public Guid TargetEntityId { get; set; }

    /// <summary>
    /// Fully-qualified entity type name (e.g., "Patient", "Appointment").
    /// varchar(100) per ModelSnapshot.
    /// </summary>
    public string TargetEntityType { get; set; } = string.Empty;

    /// <summary>
    /// Optional detailed metadata (legacy column). Structured data is in OldValues/NewValues.
    /// Stored as JSONB; must not contain AuthCredentials per NFR-007.
    /// </summary>
    public string? Details { get; set; }

    public DateTime OccurredAt { get; set; }

    // ── Compliance fields added in US_026 ────────────────────────────────────

    /// <summary>
    /// Client IP address of the actor. Set to "system" for background-job actions.
    /// varchar(45) accommodates IPv4 (15) and IPv6 (39) plus mapped IPv4 (45).
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// JSONB snapshot of entity state before the operation. Null for CREATE actions.
    /// Must not contain AuthCredentials or raw passwords (NFR-007, OWASP A02).
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSONB snapshot of entity state after the operation. Null for DELETE actions.
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// ChainHash of the immediately preceding AuditLog row.
    /// Null only for the genesis entry (first row ever inserted).
    /// </summary>
    public string? PreviousHash { get; set; }

    /// <summary>
    /// SHA-256 of "{Id}|{ActorId}|{ActionType}|{TargetEntityType}|{TargetEntityId}|{OccurredAt:O}|{PreviousHash ?? "GENESIS"}".
    /// Enables tamper detection per TR-018.
    /// </summary>
    public string ChainHash { get; set; } = string.Empty;
}
