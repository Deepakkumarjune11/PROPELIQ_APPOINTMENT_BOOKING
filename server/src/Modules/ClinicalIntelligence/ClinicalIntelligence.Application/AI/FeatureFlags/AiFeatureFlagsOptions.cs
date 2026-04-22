namespace ClinicalIntelligence.Application.AI.FeatureFlags;

/// <summary>
/// Configuration POCO for AI feature flag defaults and critical-feature protection (US_032, AC-4, AC-5, TR-025).
/// Bound from <c>appsettings.json → "AiFeatureFlags"</c>.
///
/// Hot-reload: injected as <see cref="Microsoft.Extensions.Options.IOptionsMonitor{T}"/> in
/// <see cref="RedisFeatureFlagService"/> — default changes take effect without restart.
///
/// Default resolution order on each request:
///   1. Redis key <c>ai:feature:{name}:enabled</c> — authoritative, instant propagation.
///   2. <see cref="Defaults"/> entry — fallback when key is absent or Redis is unavailable.
///   3. Implicit <c>true</c> — for unknown feature names not listed in <see cref="Defaults"/>.
/// </summary>
public sealed class AiFeatureFlagsOptions
{
    public const string SectionName = "AiFeatureFlags";

    /// <summary>
    /// Default enabled state per feature context when the Redis key is absent.
    /// Defaults to <c>true</c> for all known features — fail-open behaviour prevents a
    /// Redis cold-start or key eviction from accidentally blocking features in production.
    /// </summary>
    public Dictionary<string, bool> Defaults { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ConversationalIntake"] = true,
        ["FactExtraction"]       = true,
        ["CodeSuggestion"]       = true,
        ["ConflictDetection"]    = true,
        ["View360"]              = true,
    };

    /// <summary>
    /// Feature contexts that MUST NOT be auto-disabled on an SLA breach.
    /// Default: <c>["ConversationalIntake"]</c> — patient-facing intake must never be
    /// auto-disabled even under high latency, as disabling it directly impacts patient access.
    /// </summary>
    public List<string> CriticalFeatures { get; set; } = ["ConversationalIntake"];
}
