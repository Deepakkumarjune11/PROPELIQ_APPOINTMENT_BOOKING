namespace PatientAccess.Application.Services;

/// <summary>
/// A single provider entry in the insurance reference set.
/// Bound from <c>appsettings.json:InsuranceReference:Providers[]</c>.
/// </summary>
public sealed class InsuranceReferenceEntry
{
    /// <summary>Display / canonical name used for case-insensitive matching (e.g., "Blue Cross").</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Known member ID prefixes for this provider.
    /// An empty list means any supplied member ID yields <c>PartialMatch</c> (e.g., "Other").
    /// </summary>
    public List<string> KnownMemberIdPrefixes { get; set; } = [];
}
