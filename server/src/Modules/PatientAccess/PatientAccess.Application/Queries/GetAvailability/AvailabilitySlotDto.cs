namespace PatientAccess.Application.Queries.GetAvailability;

/// <summary>
/// Lightweight response DTO for a single available appointment slot.
/// ISO-8601 UTC string for <see cref="SlotDatetime"/> keeps the FE timezone-agnostic.
/// </summary>
/// <param name="SlotId">Appointment row identifier.</param>
/// <param name="SlotDatetime">ISO-8601 UTC datetime string, e.g. "2026-04-20T09:00:00Z".</param>
/// <param name="NoShowRisk">
///   Model-derived no-show probability (0–1). <c>null</c> when not yet scored.
///   Values &gt; 0.7 trigger the warning badge on the frontend.
/// </param>
/// <param name="RiskContributingFactors">
///   Human-readable contributing factor strings for tooltip display (AC-2).
///   Populated by <c>INoShowRiskScoringService.CalculateSchedulingRisk</c>.
/// </param>
/// <param name="IsPartialScoring">
///   <see langword="true"/> when patient-response signals were unavailable at search time.
///   Frontend renders "Partial scoring — some signals unavailable" in the tooltip footer.
/// </param>
public sealed record AvailabilitySlotDto(
    Guid     SlotId,
    string   SlotDatetime,
    decimal? NoShowRisk,
    IReadOnlyList<string> RiskContributingFactors,
    bool IsPartialScoring);
