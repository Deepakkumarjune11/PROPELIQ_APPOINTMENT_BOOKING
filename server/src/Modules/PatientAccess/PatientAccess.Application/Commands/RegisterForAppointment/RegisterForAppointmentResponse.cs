namespace PatientAccess.Application.Commands.RegisterForAppointment;

/// <summary>
/// Result of a successful appointment registration.
/// </summary>
/// <param name="PatientId">Newly created or existing patient record ID.</param>
/// <param name="AppointmentId">Booked appointment (slot) ID — equals the submitted slotId.</param>
/// <param name="InsuranceStatus">
///   Insurance soft-validation outcome: <c>pass</c>, <c>partial-match</c>, <c>fail</c>, or <c>pending</c>.
/// </param>
/// <param name="NoShowRiskScore">Persisted no-show risk score (0.0000–1.0000), or null if unscored.</param>
/// <param name="IsHighRisk"><see langword="true"/> when <paramref name="NoShowRiskScore"/> exceeds 0.70 (AC-1).</param>
/// <param name="ContributingFactors">Human-readable factor strings for the badge tooltip.</param>
public sealed record RegisterForAppointmentResponse(
    Guid   PatientId,
    Guid   AppointmentId,
    string InsuranceStatus,
    decimal? NoShowRiskScore,
    bool IsHighRisk,
    IReadOnlyList<string> ContributingFactors
);
