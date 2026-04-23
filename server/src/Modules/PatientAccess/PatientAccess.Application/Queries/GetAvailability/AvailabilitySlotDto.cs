namespace PatientAccess.Application.Queries.GetAvailability;

/// <summary>
/// Lightweight response DTO for a single available appointment slot.
/// ISO-8601 UTC string for <see cref="SlotDatetime"/> keeps the FE timezone-agnostic.
/// </summary>
/// <param name="Id">Appointment row identifier — serializes as <c>id</c>.</param>
/// <param name="Datetime">ISO-8601 UTC datetime string — serializes as <c>datetime</c>.</param>
/// <param name="Provider">Clinician name shown on the slot card.</param>
/// <param name="VisitType">&quot;in-person&quot; or &quot;telehealth&quot; — drives the visit-type icon.</param>
/// <param name="Location">Clinic address or &quot;Telehealth&quot;.</param>
/// <param name="DurationMinutes">Appointment duration in minutes.</param>
/// <param name="NoShowRisk">
///   Model-derived no-show probability (0–1). <c>null</c> when not yet scored.
///   Values &gt; 0.7 trigger the warning badge on the frontend.
/// </param>
/// <param name="RiskContributingFactors">
///   Human-readable contributing factor strings for tooltip display (AC-2).
/// </param>
/// <param name="IsPartialScoring">
///   <see langword="true"/> when patient-response signals were unavailable at search time.
/// </param>
public sealed record AvailabilitySlotDto(
    Guid     Id,
    string   Datetime,
    string   Provider,
    string   VisitType,
    string?  Location,
    int?     DurationMinutes,
    decimal? NoShowRisk,
    IReadOnlyList<string> RiskContributingFactors,
    bool IsPartialScoring);
