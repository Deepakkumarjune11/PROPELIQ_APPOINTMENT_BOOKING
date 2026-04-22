using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientAccess.Application.Infrastructure;

namespace PatientAccess.Application.Services;

/// <summary>
/// Evaluates a patient's insurance provider and member ID against the in-memory dummy
/// reference set loaded from <c>appsettings.json:InsuranceReference</c> (FR-009).
/// </summary>
/// <remarks>
/// Outcome rules:
/// <list type="bullet">
///   <item><c>Pending</c>  — provider is null/whitespace (no validation performed).</item>
///   <item><c>Fail</c>     — provider is not in the reference set.</item>
///   <item><c>PartialMatch</c> — provider matches but member ID is null/empty or does not start with a known prefix.</item>
///   <item><c>Pass</c>     — provider matches AND member ID starts with a known prefix.</item>
/// </list>
/// PHI safety: member ID is NEVER written to logs (HIPAA, DR-015, OWASP A02).
/// </remarks>
public sealed class InsuranceValidationService : IInsuranceValidationService
{
    private readonly InsuranceReferenceOptions _options;
    private readonly ILogger<InsuranceValidationService> _logger;

    public InsuranceValidationService(
        IOptions<InsuranceReferenceOptions> options,
        ILogger<InsuranceValidationService> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    public Task<InsuranceValidationResult> ValidateAsync(
        string? insuranceProvider,
        string? insuranceMemberId,
        CancellationToken ct = default)
    {
        // ── Guard: no provider supplied ─────────────────────────────────────
        if (string.IsNullOrWhiteSpace(insuranceProvider))
        {
            _logger.LogDebug("InsuranceValidation: no provider supplied — result=Pending");
            return Task.FromResult(InsuranceValidationResult.Pending);
        }

        // ── Look up provider in reference set (case-insensitive) ────────────
        var matched = _options.Providers.FirstOrDefault(
            p => p.ProviderName.Equals(insuranceProvider, StringComparison.OrdinalIgnoreCase));

        if (matched is null)
        {
            // Provider unknown — hard fail
            _logger.LogInformation(
                "InsuranceValidation: provider={Provider} result=Fail", insuranceProvider);
            return Task.FromResult(InsuranceValidationResult.Fail);
        }

        // ── Provider found — check member ID ─────────────────────────────────
        if (string.IsNullOrWhiteSpace(insuranceMemberId))
        {
            // Provider known but no member ID to verify
            _logger.LogInformation(
                "InsuranceValidation: provider={Provider} result=PartialMatch (no memberId)",
                insuranceProvider);
            return Task.FromResult(InsuranceValidationResult.PartialMatch);
        }

        // Empty prefix list (e.g., "Other") → any member ID is treated as PartialMatch
        if (matched.KnownMemberIdPrefixes.Count == 0)
        {
            _logger.LogInformation(
                "InsuranceValidation: provider={Provider} result=PartialMatch (no prefix rules)",
                insuranceProvider);
            return Task.FromResult(InsuranceValidationResult.PartialMatch);
        }

        var idMatches = matched.KnownMemberIdPrefixes.Any(
            prefix => insuranceMemberId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        // PHI guard: member ID value is deliberately excluded from log output
        var result = idMatches
            ? InsuranceValidationResult.Pass
            : InsuranceValidationResult.PartialMatch;

        _logger.LogInformation(
            "InsuranceValidation: provider={Provider} result={Result}", insuranceProvider, result);

        return Task.FromResult(result);
    }
}
