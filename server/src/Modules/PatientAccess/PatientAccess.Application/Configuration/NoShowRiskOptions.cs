namespace PatientAccess.Application.Configuration;

/// <summary>
/// Configurable signal weights for the no-show risk scoring engine (FR-006).
/// Loaded from <c>appsettings.json</c> section <c>NoShowRisk</c>.
/// Default values sum to 1.0 — validated on startup.
/// </summary>
public sealed class NoShowRiskOptions
{
    public const string SectionName = "NoShowRisk";

    /// <summary>Weight for the days-to-appointment signal. Default 0.40.</summary>
    public double DaysToAppointmentWeight { get; init; } = 0.40;

    /// <summary>Weight for the day-of-week signal. Default 0.20.</summary>
    public double DayOfWeekWeight { get; init; } = 0.20;

    /// <summary>Weight for the insurance validation status signal. Default 0.25.</summary>
    public double InsuranceStatusWeight { get; init; } = 0.25;

    /// <summary>Weight for the intake-completed signal. Default 0.15.</summary>
    public double IntakeCompletedWeight { get; init; } = 0.15;
}
