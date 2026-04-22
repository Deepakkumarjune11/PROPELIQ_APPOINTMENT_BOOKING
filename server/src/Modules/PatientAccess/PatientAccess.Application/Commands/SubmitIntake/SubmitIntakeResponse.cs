namespace PatientAccess.Application.Commands.SubmitIntake;

/// <summary>
/// Result returned to the caller after successful intake submission.
/// </summary>
/// <param name="IntakeResponseId">Unique identifier of the persisted <c>IntakeResponse</c> row.</param>
public sealed record SubmitIntakeResponse(Guid IntakeResponseId);
