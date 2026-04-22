using PatientAccess.Domain.Enums;

namespace PatientAccess.Application.Infrastructure;

/// <summary>
/// Stages an immutable audit entry on the current DbContext change tracker (AC-5, US_026).
/// The caller's SaveChangesAsync commits the entry atomically with the main operation —
/// no SaveChanges is called inside this interface.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Stages an <c>AuditLog</c> entry without calling SaveChangesAsync.
    /// The calling service's SaveChangesAsync commits both the main entity and
    /// the audit entry in a single transaction (AC-5).
    /// </summary>
    /// <param name="actorType">Role-based actor category.</param>
    /// <param name="actorId">Staff/Admin Guid. Pass <see cref="Guid.Empty"/> for system actions.</param>
    /// <param name="actionType">CRUD or domain-event action type.</param>
    /// <param name="targetEntityType">Fully-qualified entity type name (e.g., "Patient").</param>
    /// <param name="targetEntityId">Primary key of the affected entity.</param>
    /// <param name="oldValues">Pre-operation snapshot. Pass null for CREATE. Must NOT contain AuthCredentials.</param>
    /// <param name="newValues">Post-operation snapshot. Pass null for DELETE. Must NOT contain AuthCredentials.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LogAsync(
        AuditActorType actorType,
        Guid actorId,
        AuditActionType actionType,
        string targetEntityType,
        Guid targetEntityId,
        object? oldValues,
        object? newValues,
        CancellationToken ct = default);
}
