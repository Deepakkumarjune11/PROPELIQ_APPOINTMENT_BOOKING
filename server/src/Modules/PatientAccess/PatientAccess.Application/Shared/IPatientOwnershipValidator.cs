namespace PatientAccess.Application.Shared;

/// <summary>
/// Validates patient ownership of an appointment — reusable RBAC guard (US_015, OWASP A01).
/// Implemented in PatientAccess.Data to keep Application free of EF Core references.
/// Extracted as an interface so it can be shared across appointment cancel / reschedule / watchlist endpoints.
/// </summary>
public interface IPatientOwnershipValidator
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="appointmentId"/> exists and its
    /// <c>PatientId</c> matches <paramref name="patientId"/>; otherwise <see langword="false"/>.
    /// Returns <see langword="false"/> when the appointment does not exist (prevents timing attacks).
    /// </summary>
    Task<bool> IsOwnerAsync(
        Guid appointmentId,
        Guid patientId,
        CancellationToken ct = default);
}
