namespace ClinicalIntelligence.Application.AI.FeatureFlags;

/// <summary>
/// Manages runtime-configurable AI feature flags backed by Redis (US_032, AC-4, AC-5, TR-025).
///
/// Redis key schema: <c>ai:feature:{featureName}:enabled</c> (value: <c>"true"</c> or <c>"false"</c>).
///
/// Read behaviour:
///   Every <see cref="IsEnabledAsync"/> call reads directly from Redis — no in-process caching.
///   This provides instant propagation: a flag toggle set via <see cref="SetFlagAsync"/> is
///   visible to the next request on any instance within milliseconds, trivially satisfying
///   the TR-025 "within 30 seconds" requirement.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Returns <see langword="true"/> if the AI feature is currently enabled.
    /// Reads from Redis directly on every call — no local cache.
    /// Falls back to <c>AiFeatureFlagsOptions.Defaults</c> (default: <c>true</c>) when the
    /// Redis key is absent or Redis is temporarily unavailable (fail-open).
    /// </summary>
    /// <param name="featureName">Feature context name (e.g. <c>"FactExtraction"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> IsEnabledAsync(string featureName, CancellationToken ct = default);

    /// <summary>
    /// Persists a feature flag state to Redis. Propagation to all instances is instant.
    /// </summary>
    /// <param name="featureName">Feature context name.</param>
    /// <param name="enabled"><see langword="true"/> to enable; <see langword="false"/> to disable.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetFlagAsync(string featureName, bool enabled, CancellationToken ct = default);

    /// <summary>
    /// Returns the current state of all known feature flags.
    /// Each entry is resolved from Redis first, falling back to the configured default.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, bool>> GetAllFlagsAsync(CancellationToken ct = default);
}
