namespace PatientAccess.Application.Services;

/// <summary>
/// Immutable result from <see cref="INoShowRiskScoringService"/>.
/// </summary>
/// <param name="Score">Normalised risk probability in range 0.0000–1.0000 (4 decimal places).</param>
/// <param name="ContributingFactors">
/// Human-readable descriptions of each active signal, suitable for tooltip display (AC-2).
/// </param>
/// <param name="IsPartialScoring">
/// <see langword="true"/> when patient-response signals (insurance, intake) were not available.
/// Consumers should surface "Partial scoring — some signals unavailable" to the end user.
/// </param>
public sealed record NoShowRiskResult(
    decimal Score,
    IReadOnlyList<string> ContributingFactors,
    bool IsPartialScoring);
