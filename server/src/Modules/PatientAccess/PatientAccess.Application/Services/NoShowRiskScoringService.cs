using Microsoft.Extensions.Options;
using PatientAccess.Application.Configuration;
using PatientAccess.Application.Infrastructure;

namespace PatientAccess.Application.Services;

/// <summary>
/// Deterministic, rule-based implementation of <see cref="INoShowRiskScoringService"/> (FR-006).
/// Pure computation — no I/O. Safe to register as <c>Singleton</c>.
/// </summary>
public sealed class NoShowRiskScoringService : INoShowRiskScoringService
{
    private readonly NoShowRiskOptions _opts;

    public NoShowRiskScoringService(IOptions<NoShowRiskOptions> options)
    {
        _opts = options.Value;
    }

    // ── Public API ────────────────────────────────────────────────────────

    public NoShowRiskResult CalculateSchedulingRisk(DateTime slotDatetime)
    {
        var (daysContrib, daysWeight, daysDesc) = ScoreDaysToAppointment(slotDatetime);
        var (dowContrib, dowWeight, dowDesc) = ScoreDayOfWeek(slotDatetime);

        var activeWeight = daysWeight + dowWeight;
        var score = activeWeight > 0
            ? (daysContrib * daysWeight + dowContrib * dowWeight) / activeWeight
            : 0.0;

        return new NoShowRiskResult(
            Score: Math.Round((decimal)score, 4),
            ContributingFactors: new[] { daysDesc, dowDesc },
            IsPartialScoring: true);
    }

    public NoShowRiskResult CalculateFullRisk(
        DateTime slotDatetime,
        InsuranceValidationResult insuranceStatus,
        bool intakeCompleted)
    {
        var (daysContrib, daysWeight, daysDesc) = ScoreDaysToAppointment(slotDatetime);
        var (dowContrib, dowWeight, dowDesc) = ScoreDayOfWeek(slotDatetime);
        var (insContrib, insWeight, insDesc) = ScoreInsuranceStatus(insuranceStatus);
        var (intakeContrib, intakeWeight, intakeDesc) = ScoreIntakeCompleted(intakeCompleted);

        // Weights sum to 1.0, so score is already normalised
        var score = daysContrib * daysWeight
                  + dowContrib * dowWeight
                  + insContrib * insWeight
                  + intakeContrib * intakeWeight;

        return new NoShowRiskResult(
            Score: Math.Round((decimal)score, 4),
            ContributingFactors: new[] { daysDesc, dowDesc, insDesc, intakeDesc },
            IsPartialScoring: false);
    }

    // ── Private signal scorers ────────────────────────────────────────────

    /// <summary>Days-to-appointment signal.</summary>
    private (double contribution, double weight, string description) ScoreDaysToAppointment(DateTime slotDatetime)
    {
        var now = DateTime.UtcNow;
        var days = (slotDatetime.Date - now.Date).Days;

        var (contribution, level) = days switch
        {
            <= 1 => (1.0, "high"),
            <= 3 => (0.6, "elevated"),
            <= 7 => (0.3, "moderate"),
            _    => (0.1, "low")
        };

        var dayLabel = days <= 0 ? "today" : days == 1 ? "1 day" : $"{days} days";
        var description = $"Appointment in {dayLabel} — {level} risk";

        return (contribution, _opts.DaysToAppointmentWeight, description);
    }

    /// <summary>Day-of-week signal.</summary>
    private (double contribution, double weight, string description) ScoreDayOfWeek(DateTime slotDatetime)
    {
        var dow = slotDatetime.DayOfWeek;

        var (contribution, level) = dow switch
        {
            DayOfWeek.Saturday => (0.8, "high"),
            DayOfWeek.Monday   => (0.6, "elevated"),
            DayOfWeek.Friday   => (0.6, "elevated"),
            _                  => (0.2, "low")
        };

        var description = $"Booked on a {dow} — {level} risk day";
        return (contribution, _opts.DayOfWeekWeight, description);
    }

    /// <summary>Insurance validation status signal (patient-response signal).</summary>
    private (double contribution, double weight, string description) ScoreInsuranceStatus(
        InsuranceValidationResult status)
    {
        var (contribution, level) = status switch
        {
            InsuranceValidationResult.Fail         => (0.9, "high risk contribution"),
            InsuranceValidationResult.PartialMatch => (0.5, "moderate risk contribution"),
            InsuranceValidationResult.Pending      => (0.4, "moderate risk contribution"),
            InsuranceValidationResult.Pass         => (0.1, "low risk contribution"),
            _                                      => (0.4, "unknown")
        };

        var description = $"Insurance status: {status} — {level}";
        return (contribution, _opts.InsuranceStatusWeight, description);
    }

    /// <summary>Intake-completed signal (patient-response signal).</summary>
    private (double contribution, double weight, string description) ScoreIntakeCompleted(bool intakeCompleted)
    {
        var (contribution, description) = intakeCompleted
            ? (0.1, "Intake completed — low risk")
            : (0.7, "Intake not yet completed — elevated risk");

        return (contribution, _opts.IntakeCompletedWeight, description);
    }
}
