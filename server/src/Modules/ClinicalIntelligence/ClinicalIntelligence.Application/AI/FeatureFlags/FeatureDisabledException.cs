namespace ClinicalIntelligence.Application.AI.FeatureFlags;

/// <summary>
/// Exception thrown by <see cref="IAiGateway"/> when a feature flag is disabled (US_032, AC-4, AIR-Q04).
///
/// Callers that catch this exception should surface HTTP 503 with RFC 7807 problem-details JSON
/// (see <c>FeatureDisabledException</c> handler in <c>Program.cs</c>).
/// HTTP 503 is correct: the resource exists but is temporarily unavailable — NOT 404.
/// </summary>
public sealed class FeatureDisabledException(string featureName)
    : Exception($"AI feature '{featureName}' is currently disabled.")
{
    /// <summary>The name of the disabled AI feature context.</summary>
    public string FeatureName { get; } = featureName;
}
