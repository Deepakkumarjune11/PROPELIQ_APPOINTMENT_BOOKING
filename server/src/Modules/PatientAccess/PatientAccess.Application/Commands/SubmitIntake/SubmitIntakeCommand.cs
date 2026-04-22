using MediatR;

namespace PatientAccess.Application.Commands.SubmitIntake;

/// <summary>
/// Submits the patient's intake answers for a booked appointment (AC-4, FR-011).
/// Inserts a new <c>IntakeResponse</c> row; each submission is an independent record
/// so clinical history retains all submissions (edge case: double-submit is intentional).
/// </summary>
/// <param name="PatientId">Patient who completed the intake form.</param>
/// <param name="Mode">Intake channel — "manual" or "conversational".</param>
/// <param name="Answers">Map of questionId → free-text answer. Empty payload is accepted (partial completion is valid).</param>
public sealed record SubmitIntakeCommand(
    Guid PatientId,
    string Mode,
    Dictionary<string, string> Answers
) : IRequest<SubmitIntakeResponse>;
