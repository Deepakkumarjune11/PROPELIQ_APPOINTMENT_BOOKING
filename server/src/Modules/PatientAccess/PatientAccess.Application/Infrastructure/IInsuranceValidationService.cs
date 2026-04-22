namespace PatientAccess.Application.Infrastructure;

/// <summary>
/// Soft-validates patient insurance details against a reference service.
/// Implemented in task_003_be_insurance_validation_service.
/// </summary>
public interface IInsuranceValidationService
{
    /// <summary>
    /// Validates the supplied insurance credentials.
    /// </summary>
    /// <param name="insuranceProvider">Insurance provider name (may be null if not supplied).</param>
    /// <param name="insuranceMemberId">Member ID from the patient's insurance card (may be null).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///   <c>pass</c> — verified match; <c>partial-match</c> — partial data match;
    ///   <c>fail</c> — validation failed; <c>pending</c> — service unavailable.
    /// </returns>
    Task<InsuranceValidationResult> ValidateAsync(
        string? insuranceProvider,
        string? insuranceMemberId,
        CancellationToken ct = default);
}

/// <summary>Outcome of an insurance soft-validation call.</summary>
public enum InsuranceValidationResult
{
    Pass,
    PartialMatch,
    Fail,
    Pending
}
