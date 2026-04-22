using PatientAccess.Application.Infrastructure;

namespace PatientAccess.Presentation.Services;

/// <summary>
/// Null-object fallback for <see cref="IInsuranceValidationService"/>.
/// Always returns <see cref="InsuranceValidationResult.Pending"/> so booking proceeds
/// when the real validation service (task_003) has not yet been registered.
/// Replaced by the real implementation once task_003_be_insurance_validation_service is complete.
/// </summary>
internal sealed class NullInsuranceValidationService : IInsuranceValidationService
{
    public Task<InsuranceValidationResult> ValidateAsync(
        string? insuranceProvider,
        string? insuranceMemberId,
        CancellationToken ct = default)
        => Task.FromResult(InsuranceValidationResult.Pending);
}
