using PatientAccess.Application.Infrastructure;

namespace PatientAccess.Application.Services;

/// <summary>
/// Deterministic rule-based no-show risk scoring engine (FR-006).
/// Safe to register as <c>Singleton</c> — no I/O, no mutable state.
/// </summary>
public interface INoShowRiskScoringService
{
    /// <summary>
    /// Calculates risk using scheduling signals only (slot datetime).
    /// No patient data required. <see cref="NoShowRiskResult.IsPartialScoring"/> is always <see langword="true"/>.
    /// Called from <c>GetAvailabilityQueryHandler</c> to annotate each slot in the availability response.
    /// </summary>
    /// <param name="slotDatetime">UTC datetime of the appointment slot.</param>
    NoShowRiskResult CalculateSchedulingRisk(DateTime slotDatetime);

    /// <summary>
    /// Calculates risk using all four signals including patient-response signals.
    /// <see cref="NoShowRiskResult.IsPartialScoring"/> is <see langword="false"/>.
    /// Called from <c>RegisterForAppointmentHandler</c> at booking commit time (task_003).
    /// </summary>
    /// <param name="slotDatetime">UTC datetime of the appointment slot.</param>
    /// <param name="insuranceStatus">Result of the insurance soft-validation check.</param>
    /// <param name="intakeCompleted">Whether the patient completed the intake form.</param>
    NoShowRiskResult CalculateFullRisk(
        DateTime slotDatetime,
        InsuranceValidationResult insuranceStatus,
        bool intakeCompleted);
}
