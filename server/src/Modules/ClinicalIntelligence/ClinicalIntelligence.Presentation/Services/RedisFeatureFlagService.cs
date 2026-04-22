using ClinicalIntelligence.Application.AI.FeatureFlags;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ClinicalIntelligence.Presentation.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IFeatureFlagService"/> (US_032, AC-4, AC-5, TR-025).
///
/// Redis key schema: <c>ai:feature:{featureName}:enabled</c>
///   value <c>"true"</c>  → feature enabled
///   value <c>"false"</c> → feature disabled
///   key absent           → resolve from <see cref="AiFeatureFlagsOptions.Defaults"/> (fail-open)
///
/// Fail-open behaviour: when Redis is unavailable (exception on read) the service falls back
/// to the configured default, preventing a Redis outage from blocking production features.
///
/// Thread-safety: <see cref="IConnectionMultiplexer"/> is thread-safe; safe for singleton lifetime.
/// </summary>
public sealed class RedisFeatureFlagService(
    IConnectionMultiplexer                redis,
    IOptionsMonitor<AiFeatureFlagsOptions> options,
    ILogger<RedisFeatureFlagService>       logger) : IFeatureFlagService
{
    private static string FlagKey(string name) => $"ai:feature:{name}:enabled";

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken ct = default)
    {
        try
        {
            var db    = redis.GetDatabase();
            var value = await db.StringGetAsync(FlagKey(featureName)).ConfigureAwait(false);

            if (value.HasValue)
                return value.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);

            // Key absent — use configured default (fail-open)
            return GetDefault(featureName);
        }
        catch (Exception ex)
        {
            // Redis unavailable — fail-open using config default (prevents outage-induced feature block)
            logger.LogWarning(ex,
                "Redis unavailable reading feature flag '{FeatureName}' — using config default",
                featureName);
            return GetDefault(featureName);
        }
    }

    /// <inheritdoc />
    public async Task SetFlagAsync(string featureName, bool enabled, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(FlagKey(featureName), enabled ? "true" : "false")
            .ConfigureAwait(false);

        logger.LogInformation(
            "AI feature flag updated: {FeatureName} = {Enabled}", featureName, enabled);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, bool>> GetAllFlagsAsync(CancellationToken ct = default)
    {
        var defaults = options.CurrentValue.Defaults;
        var result   = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, defaultVal) in defaults)
        {
            try
            {
                var db    = redis.GetDatabase();
                var value = await db.StringGetAsync(FlagKey(name)).ConfigureAwait(false);
                result[name] = value.HasValue
                    ? value.ToString().Equals("true", StringComparison.OrdinalIgnoreCase)
                    : defaultVal;
            }
            catch
            {
                result[name] = defaultVal; // Redis error → use default
            }
        }

        return result;
    }

    private bool GetDefault(string featureName)
        => options.CurrentValue.Defaults.TryGetValue(featureName, out var val) ? val : true;
}
