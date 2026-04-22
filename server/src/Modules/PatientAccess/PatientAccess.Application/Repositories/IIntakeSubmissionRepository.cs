using PatientAccess.Domain.Enums;

namespace PatientAccess.Application.Repositories;

/// <summary>
/// Persistence contract for intake form submissions (FR-011).
/// Implemented in <c>PatientAccess.Data</c> to keep Application free of EF Core references.
/// </summary>
public interface IIntakeSubmissionRepository
{
    /// <summary>
    /// Returns <see langword="true"/> when the given patient exists (and has not been soft-deleted).
    /// </summary>
    Task<bool> PatientExistsAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new <c>IntakeResponse</c> row and appends an immutable audit entry (DR-008).
    /// Answers are encrypted at rest via the <c>IntakeResponseConfiguration</c> ValueConverter (DR-015).
    /// </summary>
    /// <param name="patientId">Owner of the intake record.</param>
    /// <param name="mode">Intake channel (manual / conversational).</param>
    /// <param name="answersJson">Serialised JSON answers payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated <c>IntakeResponse.Id</c>.</returns>
    Task<Guid> SubmitIntakeAsync(
        Guid patientId,
        IntakeMode mode,
        string answersJson,
        CancellationToken ct = default);
}
