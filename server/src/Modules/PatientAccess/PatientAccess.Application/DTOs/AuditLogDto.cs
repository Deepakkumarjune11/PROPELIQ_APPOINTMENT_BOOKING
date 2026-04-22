namespace PatientAccess.Application.DTOs;

/// <summary>
/// Read projection for a single audit log entry (US_026, AC-3).
/// Excludes <c>Details</c> (legacy column) and <c>PreviousHash</c> (internal chain linkage).
/// <c>ChainHash</c> is exposed so clients can verify tamper-detection integrity (TR-018).
/// </summary>
public sealed record AuditLogDto(
    Guid     Id,
    string   ActorType,
    Guid     ActorId,
    string   ActionType,
    string   TargetEntityType,
    Guid     TargetEntityId,
    string?  IpAddress,
    string?  OldValues,
    string?  NewValues,
    DateTime CreatedAt,
    string   ChainHash);
