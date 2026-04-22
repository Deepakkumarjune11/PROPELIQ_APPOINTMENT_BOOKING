using ClinicalIntelligence.Application.AI;
using ClinicalIntelligence.Application.AI.ModelVersion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ClinicalIntelligence.Presentation.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IModelVersionService"/> (US_031, AC-5, AIR-O04).
///
/// Redis keys:
///   <c>ai:deployment:inference:current</c>  — name of the active Azure OpenAI deployment.
///   <c>ai:deployment:inference:previous</c> — name preserved during <see cref="ActivateDeploymentAsync"/>
///                                              for single-hop rollback via <see cref="RollbackAsync"/>.
///
/// Zero-downtime switching: a Redis key update propagates to all instances within milliseconds.
/// In-flight requests complete on the deployment name already resolved; only new requests after
/// the update see the rolled-back deployment name — satisfying AC-5 "within 60 seconds".
///
/// Thread-safety: singleton; StackExchange.Redis <see cref="IConnectionMultiplexer"/> is thread-safe.
/// </summary>
public sealed class RedisModelVersionService(
    IConnectionMultiplexer            redis,
    IOptions<AzureOpenAiOptions>      options,
    ILogger<RedisModelVersionService> logger) : IModelVersionService
{
    private const string CurrentKey  = "ai:deployment:inference:current";
    private const string PreviousKey = "ai:deployment:inference:previous";

    /// <inheritdoc />
    public async Task ActivateDeploymentAsync(string deploymentName, CancellationToken ct = default)
    {
        var db      = redis.GetDatabase();
        var current = await db.StringGetAsync(CurrentKey).ConfigureAwait(false);

        // Preserve current → previous before overwriting so RollbackAsync can restore it
        if (current.HasValue)
            await db.StringSetAsync(PreviousKey, current).ConfigureAwait(false);

        await db.StringSetAsync(CurrentKey, deploymentName).ConfigureAwait(false);

        logger.LogInformation(
            "AI deployment activated: {NewDeployment} (previous: {Previous})",
            deploymentName,
            current.HasValue ? (string)current! : options.Value.InferenceDeploymentName);
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken ct = default)
    {
        var db       = redis.GetDatabase();
        var previous = await db.StringGetAsync(PreviousKey).ConfigureAwait(false);

        if (!previous.HasValue)
        {
            // No stored previous — revert to config default
            await db.StringSetAsync(CurrentKey, options.Value.InferenceDeploymentName)
                .ConfigureAwait(false);
            logger.LogWarning(
                "AI deployment rollback: no previous deployment in Redis, reverting to config default {Default}",
                options.Value.InferenceDeploymentName);
            return;
        }

        var current = await db.StringGetAsync(CurrentKey).ConfigureAwait(false);

        // Swap current ↔ previous so a second rollback re-applies the forward change
        await db.StringSetAsync(PreviousKey, current).ConfigureAwait(false);
        await db.StringSetAsync(CurrentKey, previous).ConfigureAwait(false);

        logger.LogInformation(
            "AI deployment rolled back to: {RolledBack} (was: {Was})",
            (string)previous!,
            current.HasValue ? (string)current! : "unknown");
    }

    /// <inheritdoc />
    public async Task<string> GetActiveDeploymentAsync(CancellationToken ct = default)
    {
        var db      = redis.GetDatabase();
        var current = await db.StringGetAsync(CurrentKey).ConfigureAwait(false);

        return current.HasValue
            ? (string)current!
            : options.Value.InferenceDeploymentName; // config fallback when key absent
    }
}
