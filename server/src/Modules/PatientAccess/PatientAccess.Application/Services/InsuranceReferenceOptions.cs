namespace PatientAccess.Application.Services;

/// <summary>
/// IOptions-bound configuration for the in-memory insurance reference set (FR-009).
/// Loaded from <c>appsettings.json</c> section <c>InsuranceReference</c>.
/// Allows environment-specific reference data without schema migrations (KISS / YAGNI).
/// </summary>
public sealed class InsuranceReferenceOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "InsuranceReference";

    /// <summary>List of known insurance providers and their member ID prefix patterns.</summary>
    public List<InsuranceReferenceEntry> Providers { get; set; } = [];
}
